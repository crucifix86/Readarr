using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Common.Disk;
using NzbDrone.Core.Books;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Users;
using Readarr.Api.V1.Users;
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
        private readonly IEditionService _editionService;
        private readonly IMediaFileService _mediaFileService;
        private readonly IDiskProvider _diskProvider;
        private readonly IUserFavoriteRepository _favoriteRepo;
        private readonly IUserBookProgressRepository _progressRepo;

        public OpdsController(
            IAuthorService authorService,
            IBookService bookService,
            IEditionService editionService,
            IMediaFileService mediaFileService,
            IDiskProvider diskProvider,
            IUserFavoriteRepository favoriteRepo,
            IUserBookProgressRepository progressRepo)
        {
            _authorService = authorService;
            _bookService = bookService;
            _editionService = editionService;
            _mediaFileService = mediaFileService;
            _diskProvider = diskProvider;
            _favoriteRepo = favoriteRepo;
            _progressRepo = progressRepo;
        }

        [HttpGet("")]
        public IActionResult Root()
        {
            var entries = new List<XElement>
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

            // Only surface the per-user feeds when there's actually a per-user
            // identity on the request. Global ApiKey reads of /opds get the
            // plain library view so non-reader clients don't see dead links.
            if (UserMeHelpers.CurrentUserId(HttpContext).HasValue)
            {
                entries.Add(OpdsFeedBuilder.NavEntry(
                    "opds:me:reading",
                    "Currently Reading",
                    "/opds/me/reading",
                    "Books you're partway through"));
                entries.Add(OpdsFeedBuilder.NavEntry(
                    "opds:me:favorites",
                    "Favorites",
                    "/opds/me/favorites",
                    "Books you've starred"));
            }

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

        [HttpGet("me/favorites")]
        public IActionResult MyFavorites()
        {
            var userId = UserMeHelpers.CurrentUserId(HttpContext);
            if (userId == null)
            {
                return AtomResult(EmptyPersonalFeed(
                    "opds:me:favorites",
                    "Favorites",
                    "/opds/me/favorites",
                    "Log in with a per-user ApiKey to see your favorites."));
            }

            var favoriteBookIds = _favoriteRepo.ForUser(userId.Value)
                .Select(f => f.BookId)
                .ToHashSet();

            var entries = BooksToEntries(favoriteBookIds);

            var doc = OpdsFeedBuilder.Feed(
                id: "opds:me:favorites",
                title: "Favorites",
                selfHref: "/opds/me/favorites",
                startHref: "/opds",
                feedType: OpdsFeedBuilder.AcqType,
                entries: entries,
                upHref: "/opds");

            return AtomResult(doc);
        }

        [HttpGet("me/reading")]
        public IActionResult MyReading()
        {
            var userId = UserMeHelpers.CurrentUserId(HttpContext);
            if (userId == null)
            {
                return AtomResult(EmptyPersonalFeed(
                    "opds:me:reading",
                    "Currently Reading",
                    "/opds/me/reading",
                    "Log in with a per-user ApiKey to see your reading list."));
            }

            var inProgressFileIds = _progressRepo.ForUser(userId.Value)
                .Where(p => p.Percent.HasValue && p.Percent.Value > 0 && p.Percent.Value < 0.99)
                .Select(p => p.BookFileId)
                .ToHashSet();

            if (inProgressFileIds.Count == 0)
            {
                var empty = OpdsFeedBuilder.Feed(
                    id: "opds:me:reading",
                    title: "Currently Reading",
                    selfHref: "/opds/me/reading",
                    startHref: "/opds",
                    feedType: OpdsFeedBuilder.AcqType,
                    entries: Array.Empty<XElement>(),
                    upHref: "/opds");
                return AtomResult(empty);
            }

            // Resolve bookfile -> edition -> book to build entries scoped to in-progress books only.
            var bookIds = new HashSet<int>();
            foreach (var fileId in inProgressFileIds)
            {
                BookFile bf;
                try
                {
                    bf = _mediaFileService.Get(fileId);
                }
                catch
                {
                    continue;
                }

                if (bf == null || bf.EditionId <= 0)
                {
                    continue;
                }

                Edition edition;
                try
                {
                    edition = _editionService.GetEdition(bf.EditionId);
                }
                catch
                {
                    continue;
                }

                if (edition != null && edition.BookId > 0)
                {
                    bookIds.Add(edition.BookId);
                }
            }

            var entries = BooksToEntries(bookIds);

            var doc = OpdsFeedBuilder.Feed(
                id: "opds:me:reading",
                title: "Currently Reading",
                selfHref: "/opds/me/reading",
                startHref: "/opds",
                feedType: OpdsFeedBuilder.AcqType,
                entries: entries,
                upHref: "/opds");

            return AtomResult(doc);
        }

        private List<XElement> BooksToEntries(ISet<int> bookIdFilter)
        {
            var entries = new List<XElement>();
            if (bookIdFilter.Count == 0)
            {
                return entries;
            }

            foreach (var bookId in bookIdFilter)
            {
                Book book;
                try
                {
                    book = _bookService.GetBook(bookId);
                }
                catch
                {
                    continue;
                }

                if (book == null)
                {
                    continue;
                }

                var files = _mediaFileService.GetFilesByBook(book.Id);
                if (files == null || files.Count == 0)
                {
                    continue;
                }

                var primary = files.First();
                CoreAuthor author = null;
                try
                {
                    author = _authorService.GetAuthor(book.AuthorId);
                }
                catch
                {
                    /* author gone; fall back to unknown */
                }

                var authorName = author?.Metadata?.Value?.Name ?? "Unknown";

                entries.Add(OpdsFeedBuilder.BookEntry(
                    id: $"opds:book:{book.Id}:file:{primary.Id}",
                    title: book.Title,
                    authorName: authorName,
                    downloadHref: $"/opds/download/{primary.Id}",
                    downloadMime: OpdsFeedBuilder.MimeFor(primary.Path),
                    published: book.ReleaseDate,
                    fileSize: primary.Size));
            }

            return entries;
        }

        private static XDocument EmptyPersonalFeed(string id, string title, string selfHref, string summary)
        {
            // Emit a single "nav" entry with helper text so OPDS clients show
            // a line explaining why the feed is empty instead of a blank list.
            return OpdsFeedBuilder.Feed(
                id: id,
                title: title,
                selfHref: selfHref,
                startHref: "/opds",
                feedType: OpdsFeedBuilder.AcqType,
                entries: new[]
                {
                    OpdsFeedBuilder.NavEntry(
                        id + ":hint",
                        title,
                        selfHref,
                        summary)
                },
                upHref: "/opds");
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
