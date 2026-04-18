using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Common.Disk;
using NzbDrone.Core.Books;
using NzbDrone.Core.MediaFiles;
using CoreAuthor = NzbDrone.Core.Books.Author;
using IOPath = System.IO.Path;

namespace Readarr.Api.V1.Opds
{
    // OPDS 1.2 catalog endpoint.
    // Serves the Readarr library as an Atom-based feed so any OPDS-aware
    // ebook reader (Moon+ Reader, KyBook, FBReader, KOReader, Calibre
    // Companion, etc.) can browse + download directly.
    //
    // Auth: uses the same API key as the main Readarr API. Pass via
    // X-Api-Key header or ?apikey=... query param.
    [ApiController]
    [Authorize(AuthenticationSchemes = "API")]
    [Route("opds")]
    [Produces("application/atom+xml")]
    public class OpdsController : Controller
    {
        private readonly IAuthorService _authorService;
        private readonly IBookService _bookService;
        private readonly IMediaFileService _mediaFileService;
        private readonly IDiskProvider _diskProvider;

        public OpdsController(
            IAuthorService authorService,
            IBookService bookService,
            IMediaFileService mediaFileService,
            IDiskProvider diskProvider)
        {
            _authorService = authorService;
            _bookService = bookService;
            _mediaFileService = mediaFileService;
            _diskProvider = diskProvider;
        }

        [HttpGet("")]
        public IActionResult Root()
        {
            var entries = new[]
            {
                OpdsFeedBuilder.NavEntry(
                    "opds:authors",
                    "Authors",
                    "/opds/authors",
                    "Browse by author"),
                OpdsFeedBuilder.NavEntry(
                    "opds:recent",
                    "Recently Added",
                    "/opds/books/recent",
                    "Recently added books")
            };

            var doc = OpdsFeedBuilder.Feed(
                id: "opds:readarr:root",
                title: "Readarr Library",
                selfHref: "/opds",
                startHref: "/opds",
                feedType: OpdsFeedBuilder.NavType,
                entries: entries,
                searchHref: "/opds/search.xml");

            return AtomResult(doc);
        }

        [HttpGet("authors")]
        public IActionResult Authors()
        {
            var authors = _authorService.GetAllAuthors()
                .OrderBy(a => a.Metadata?.Value?.Name ?? "", StringComparer.OrdinalIgnoreCase);

            var entries = authors.Select(a => OpdsFeedBuilder.NavEntry(
                id: $"opds:author:{a.Id}",
                title: a.Metadata?.Value?.Name ?? $"Author {a.Id}",
                href: $"/opds/author/{a.Id}",
                summary: null));

            var doc = OpdsFeedBuilder.Feed(
                id: "opds:authors",
                title: "All Authors",
                selfHref: "/opds/authors",
                startHref: "/opds",
                feedType: OpdsFeedBuilder.NavType,
                entries: entries,
                upHref: "/opds");

            return AtomResult(doc);
        }

        [HttpGet("author/{authorId:int}")]
        public IActionResult Author(int authorId)
        {
            CoreAuthor author;
            try
            {
                author = _authorService.GetAuthor(authorId);
            }
            catch
            {
                return NotFound();
            }

            if (author == null)
            {
                return NotFound();
            }

            var books = _bookService.GetBooksByAuthor(authorId)
                .OrderBy(b => b.ReleaseDate ?? DateTime.MinValue)
                .ToList();

            var authorName = author.Metadata?.Value?.Name ?? $"Author {authorId}";

            var entries = new List<XElement>();
            foreach (var book in books)
            {
                var files = _mediaFileService.GetFilesByBook(book.Id);
                if (files == null || files.Count == 0)
                {
                    continue;
                }

                // One OPDS entry per book, first file as acquisition link.
                var primary = files.First();
                entries.Add(OpdsFeedBuilder.BookEntry(
                    id: $"opds:book:{book.Id}",
                    title: book.Title,
                    authorName: authorName,
                    downloadHref: $"/opds/download/{primary.Id}",
                    downloadMime: OpdsFeedBuilder.MimeFor(primary.Path),
                    published: book.ReleaseDate,
                    fileSize: primary.Size));
            }

            var doc = OpdsFeedBuilder.Feed(
                id: $"opds:author:{authorId}",
                title: authorName,
                selfHref: $"/opds/author/{authorId}",
                startHref: "/opds",
                feedType: OpdsFeedBuilder.AcqType,
                entries: entries,
                upHref: "/opds/authors");

            return AtomResult(doc);
        }

        [HttpGet("books/recent")]
        public IActionResult Recent([FromQuery] int limit = 50)
        {
            // IAuthorService/IBookService don't expose a "recently added" directly.
            // Walk all authors -> books -> files, sort by DateAdded, take top N.
            var authors = _authorService.GetAllAuthors();
            var rows = new List<(Book book, BookFile file, CoreAuthor author)>();
            foreach (var a in authors)
            {
                foreach (var b in _bookService.GetBooksByAuthor(a.Id))
                {
                    var files = _mediaFileService.GetFilesByBook(b.Id);
                    if (files == null)
                    {
                        continue;
                    }

                    foreach (var f in files)
                    {
                        rows.Add((b, f, a));
                    }
                }
            }

            var entries = rows
                .OrderByDescending(r => r.file.DateAdded)
                .Take(Math.Max(1, Math.Min(limit, 200)))
                .Select(r => OpdsFeedBuilder.BookEntry(
                    id: $"opds:book:{r.book.Id}:file:{r.file.Id}",
                    title: r.book.Title,
                    authorName: r.author.Metadata?.Value?.Name ?? "Unknown",
                    downloadHref: $"/opds/download/{r.file.Id}",
                    downloadMime: OpdsFeedBuilder.MimeFor(r.file.Path),
                    published: r.book.ReleaseDate,
                    fileSize: r.file.Size));

            var doc = OpdsFeedBuilder.Feed(
                id: "opds:recent",
                title: "Recently Added",
                selfHref: "/opds/books/recent",
                startHref: "/opds",
                feedType: OpdsFeedBuilder.AcqType,
                entries: entries,
                upHref: "/opds");

            return AtomResult(doc);
        }

        [HttpGet("search.xml")]
        [Produces("application/opensearchdescription+xml")]
        public IActionResult SearchDescription()
        {
            var ns = XNamespace.Get("http://a9.com/-/spec/opensearch/1.1/");
            var doc = new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                new XElement(ns + "OpenSearchDescription",
                    new XElement(ns + "ShortName", "Readarr"),
                    new XElement(ns + "Description", "Search the Readarr library"),
                    new XElement(ns + "InputEncoding", "UTF-8"),
                    new XElement(ns + "OutputEncoding", "UTF-8"),
                    new XElement(ns + "Url",
                        new XAttribute("type", OpdsFeedBuilder.AcqType),
                        new XAttribute("template", "/opds/search?query={searchTerms}"))));

            return new ContentResult
            {
                Content = doc.Declaration + "\n" + doc.ToString(),
                ContentType = "application/opensearchdescription+xml",
                StatusCode = 200
            };
        }

        [HttpGet("search")]
        public IActionResult Search([FromQuery] string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return Root();
            }

            var q = query.Trim();
            var authors = _authorService.GetAllAuthors();

            var rows = new List<(Book book, BookFile file, CoreAuthor author)>();
            foreach (var a in authors)
            {
                var authorName = a.Metadata?.Value?.Name ?? "";
                var authorMatch = authorName.Contains(q, StringComparison.OrdinalIgnoreCase);

                foreach (var b in _bookService.GetBooksByAuthor(a.Id))
                {
                    if (!authorMatch && !(b.Title ?? "").Contains(q, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var files = _mediaFileService.GetFilesByBook(b.Id);
                    if (files == null)
                    {
                        continue;
                    }

                    foreach (var f in files)
                    {
                        rows.Add((b, f, a));
                    }
                }
            }

            var entries = rows
                .Take(100)
                .Select(r => OpdsFeedBuilder.BookEntry(
                    id: $"opds:book:{r.book.Id}:file:{r.file.Id}",
                    title: r.book.Title,
                    authorName: r.author.Metadata?.Value?.Name ?? "Unknown",
                    downloadHref: $"/opds/download/{r.file.Id}",
                    downloadMime: OpdsFeedBuilder.MimeFor(r.file.Path),
                    published: r.book.ReleaseDate,
                    fileSize: r.file.Size));

            var doc = OpdsFeedBuilder.Feed(
                id: $"opds:search:{q}",
                title: $"Search: {q}",
                selfHref: $"/opds/search?query={Uri.EscapeDataString(q)}",
                startHref: "/opds",
                feedType: OpdsFeedBuilder.AcqType,
                entries: entries,
                upHref: "/opds");

            return AtomResult(doc);
        }

        [HttpGet("download/{bookFileId:int}")]
        public IActionResult Download(int bookFileId)
        {
            var file = _mediaFileService.Get(bookFileId);
            if (file == null || string.IsNullOrEmpty(file.Path))
            {
                return NotFound();
            }

            if (!_diskProvider.FileExists(file.Path))
            {
                return NotFound();
            }

            var stream = _diskProvider.OpenReadStream(file.Path);
            var mime = OpdsFeedBuilder.MimeFor(file.Path);
            var filename = IOPath.GetFileName(file.Path);
            return File(stream, mime, filename);
        }

        private IActionResult AtomResult(XDocument doc)
        {
            var xml = doc.Declaration + "\n" + doc.ToString();
            return new ContentResult
            {
                Content = xml,
                ContentType = "application/atom+xml",
                StatusCode = 200
            };
        }
    }
}
