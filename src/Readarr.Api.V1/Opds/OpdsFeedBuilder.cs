using System;
using System.Collections.Generic;
using System.Xml.Linq;

namespace Readarr.Api.V1.Opds
{
    // Minimal OPDS 1.2 Atom feed builder.
    // Spec: https://specs.opds.io/opds-1.2
    public static class OpdsFeedBuilder
    {
        public const string NavType = "application/atom+xml;profile=opds-catalog;kind=navigation";
        public const string AcqType = "application/atom+xml;profile=opds-catalog;kind=acquisition";

        public static readonly XNamespace Atom = "http://www.w3.org/2005/Atom";
        public static readonly XNamespace Opds = "http://opds-spec.org/2010/catalog";
        public static readonly XNamespace Dc = "http://purl.org/dc/terms/";

        public static XDocument Feed(
            string id,
            string title,
            string selfHref,
            string startHref,
            string feedType,
            IEnumerable<XElement> entries,
            string upHref = null,
            string searchHref = null)
        {
            var links = new List<XElement>
            {
                Link("self", selfHref, feedType),
                Link("start", startHref, NavType)
            };

            if (!string.IsNullOrEmpty(upHref))
            {
                links.Add(Link("up", upHref, NavType));
            }

            if (!string.IsNullOrEmpty(searchHref))
            {
                links.Add(new XElement(Atom + "link",
                    new XAttribute("rel", "search"),
                    new XAttribute("type", "application/opensearchdescription+xml"),
                    new XAttribute("href", searchHref)));
            }

            var feed = new XElement(Atom + "feed",
                new XAttribute(XNamespace.Xmlns + "opds", Opds.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "dc", Dc.NamespaceName),
                new XElement(Atom + "id", id),
                new XElement(Atom + "title", title),
                new XElement(Atom + "updated", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")),
                new XElement(Atom + "author",
                    new XElement(Atom + "name", "Readarr")));

            foreach (var link in links)
            {
                feed.Add(link);
            }

            foreach (var entry in entries)
            {
                feed.Add(entry);
            }

            return new XDocument(new XDeclaration("1.0", "utf-8", null), feed);
        }

        public static XElement Link(string rel, string href, string type)
        {
            return new XElement(Atom + "link",
                new XAttribute("rel", rel),
                new XAttribute("href", href),
                new XAttribute("type", type));
        }

        // A navigation entry that points to another OPDS catalog feed.
        public static XElement NavEntry(string id, string title, string href, string summary = null)
        {
            var entry = new XElement(Atom + "entry",
                new XElement(Atom + "id", id),
                new XElement(Atom + "title", title),
                new XElement(Atom + "updated", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")),
                Link("subsection", href, NavType));

            if (!string.IsNullOrEmpty(summary))
            {
                entry.Add(new XElement(Atom + "content", new XAttribute("type", "text"), summary));
            }

            return entry;
        }

        // An acquisition entry representing a downloadable book.
        public static XElement BookEntry(
            string id,
            string title,
            string authorName,
            string downloadHref,
            string downloadMime,
            string coverHref = null,
            string summary = null,
            DateTime? published = null,
            long? fileSize = null)
        {
            var entry = new XElement(Atom + "entry",
                new XElement(Atom + "id", id),
                new XElement(Atom + "title", title),
                new XElement(Atom + "updated", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")),
                new XElement(Atom + "author",
                    new XElement(Atom + "name", authorName ?? "Unknown")));

            if (published.HasValue)
            {
                entry.Add(new XElement(Atom + "published",
                    published.Value.ToString("yyyy-MM-ddTHH:mm:ssZ")));
            }

            if (!string.IsNullOrEmpty(summary))
            {
                entry.Add(new XElement(Atom + "content", new XAttribute("type", "text"), summary));
            }

            // The download link (acquisition)
            var acqLink = new XElement(Atom + "link",
                new XAttribute("rel", "http://opds-spec.org/acquisition"),
                new XAttribute("href", downloadHref),
                new XAttribute("type", downloadMime));

            if (fileSize.HasValue)
            {
                acqLink.Add(new XAttribute(Dc + "extent", fileSize.Value.ToString()));
            }

            entry.Add(acqLink);

            if (!string.IsNullOrEmpty(coverHref))
            {
                entry.Add(new XElement(Atom + "link",
                    new XAttribute("rel", "http://opds-spec.org/image"),
                    new XAttribute("href", coverHref),
                    new XAttribute("type", "image/jpeg")));
                entry.Add(new XElement(Atom + "link",
                    new XAttribute("rel", "http://opds-spec.org/image/thumbnail"),
                    new XAttribute("href", coverHref),
                    new XAttribute("type", "image/jpeg")));
            }

            return entry;
        }

        // Map book-file extension to OPDS MIME type.
        public static string MimeFor(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return "application/octet-stream";
            }

            var ext = global::System.IO.Path.GetExtension(path).ToLowerInvariant();
            return ext switch
            {
                ".epub" => "application/epub+zip",
                ".pdf" => "application/pdf",
                ".mobi" => "application/x-mobipocket-ebook",
                ".azw" => "application/vnd.amazon.ebook",
                ".azw3" => "application/vnd.amazon.ebook",
                ".cbz" => "application/vnd.comicbook+zip",
                ".cbr" => "application/vnd.comicbook-rar",
                ".txt" => "text/plain",
                _ => "application/octet-stream"
            };
        }
    }
}
