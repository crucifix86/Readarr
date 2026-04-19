using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace NzbDrone.Core.Books.Reader
{
    // Minimal EPUB parser: opens the zip, locates the OPF via container.xml,
    // extracts manifest + spine + nav, and provides random access to the
    // contained files. Just enough for our reader; no schema validation.
    internal sealed class EpubArchive : IDisposable
    {
        private static readonly XNamespace Ns_Container = "urn:oasis:names:tc:opendocument:xmlns:container";
        private static readonly XNamespace Ns_Opf = "http://www.idpf.org/2007/opf";
        private static readonly XNamespace Ns_Dc = "http://purl.org/dc/elements/1.1/";
        private static readonly XNamespace Ns_Ncx = "http://www.daisy.org/z3986/2005/ncx/";
        private static readonly XNamespace Ns_Xhtml = "http://www.w3.org/1999/xhtml";

        private readonly ZipArchive _zip;
        private readonly Dictionary<string, ZipArchiveEntry> _entriesByPath;

        public string OpfDir { get; }
        public string Title { get; }
        public string Author { get; }
        public List<string> Spine { get; } = new List<string>();
        public Dictionary<string, string> Manifest { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public string TocNcxPath { get; }
        public string NavHtmlPath { get; }

        // Uncompressed size in bytes of each spine HTML file — a cheap proxy
        // for "does this page have real content". Calibre "split" epubs stash
        // chapter-title-only fragments of ~600B and the actual prose in
        // 5KB+ files; the client uses this to optionally skip the empty bits.
        public List<long> SpineSizes { get; } = new List<long>();

        public EpubArchive(string epubPath)
        {
            _zip = ZipFile.OpenRead(epubPath);
            _entriesByPath = _zip.Entries.ToDictionary(
                e => NormalizePath(e.FullName),
                e => e,
                StringComparer.OrdinalIgnoreCase);

            var containerEntry = GetEntry("META-INF/container.xml")
                ?? throw new InvalidDataException("EPUB missing META-INF/container.xml");

            string opfPath;
            using (var s = containerEntry.Open())
            {
                var doc = XDocument.Load(s);
                var rootfile = doc.Descendants(Ns_Container + "rootfile").FirstOrDefault()
                    ?? throw new InvalidDataException("EPUB container.xml has no rootfile");
                opfPath = NormalizePath(rootfile.Attribute("full-path")?.Value
                    ?? throw new InvalidDataException("rootfile missing full-path"));
            }

            OpfDir = GetDirectory(opfPath);

            var opfEntry = GetEntry(opfPath)
                ?? throw new InvalidDataException($"EPUB missing OPF at {opfPath}");

            XDocument opf;
            using (var s = opfEntry.Open())
            {
                opf = XDocument.Load(s);
            }

            // Metadata
            var meta = opf.Descendants(Ns_Opf + "metadata").FirstOrDefault();
            if (meta != null)
            {
                Title = meta.Elements(Ns_Dc + "title").FirstOrDefault()?.Value?.Trim();
                Author = meta.Elements(Ns_Dc + "creator").FirstOrDefault()?.Value?.Trim();
            }

            // Manifest: id -> absolute-within-zip path
            var manifestNode = opf.Descendants(Ns_Opf + "manifest").FirstOrDefault();
            var manifestById = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (manifestNode != null)
            {
                foreach (var item in manifestNode.Elements(Ns_Opf + "item"))
                {
                    var id = item.Attribute("id")?.Value;
                    var href = item.Attribute("href")?.Value;
                    if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(href))
                    {
                        continue;
                    }

                    var full = ResolveFromOpf(href);
                    manifestById[id] = full;
                    Manifest[full] = item.Attribute("media-type")?.Value ?? "application/octet-stream";

                    var props = item.Attribute("properties")?.Value ?? string.Empty;
                    if (props.Split(' ').Any(p => p.Equals("nav", StringComparison.OrdinalIgnoreCase))
                        && string.IsNullOrEmpty(NavHtmlPath))
                    {
                        NavHtmlPath = full;
                    }
                }
            }

            // Spine: ordered list of itemrefs -> manifest ids
            var spineNode = opf.Descendants(Ns_Opf + "spine").FirstOrDefault();
            if (spineNode != null)
            {
                var tocIdRef = spineNode.Attribute("toc")?.Value;
                if (!string.IsNullOrEmpty(tocIdRef) && manifestById.TryGetValue(tocIdRef, out var ncxPath))
                {
                    TocNcxPath = ncxPath;
                }

                foreach (var itemref in spineNode.Elements(Ns_Opf + "itemref"))
                {
                    var idref = itemref.Attribute("idref")?.Value;
                    if (!string.IsNullOrEmpty(idref) && manifestById.TryGetValue(idref, out var full))
                    {
                        Spine.Add(full);
                        var entry = GetEntry(full) ?? FindEntry(full);
                        SpineSizes.Add(entry?.Length ?? 0);
                    }
                }
            }
        }

        public ZipArchiveEntry GetEntry(string path)
        {
            var p = NormalizePath(path);
            return _entriesByPath.TryGetValue(p, out var e) ? e : null;
        }

        // Suffix-match fallback for Calibre-style ../ references.
        public ZipArchiveEntry FindEntry(string path)
        {
            var direct = GetEntry(path);
            if (direct != null)
            {
                return direct;
            }

            var p = NormalizePath(path);
            foreach (var kvp in _entriesByPath)
            {
                if (kvp.Key.EndsWith("/" + p, StringComparison.OrdinalIgnoreCase) ||
                    p.EndsWith("/" + kvp.Key, StringComparison.OrdinalIgnoreCase))
                {
                    return kvp.Value;
                }
            }

            return null;
        }

        public string ReadText(string path)
        {
            var entry = FindEntry(path);
            if (entry == null)
            {
                return null;
            }

            using var s = entry.Open();
            using var r = new StreamReader(s, Encoding.UTF8);
            return r.ReadToEnd();
        }

        public byte[] ReadBytes(string path)
        {
            var entry = FindEntry(path);
            if (entry == null)
            {
                return null;
            }

            using var ms = new MemoryStream();
            using var s = entry.Open();
            s.CopyTo(ms);
            return ms.ToArray();
        }

        public void Dispose() => _zip.Dispose();

        private string ResolveFromOpf(string href)
        {
            if (Uri.TryCreate(href, UriKind.Absolute, out _))
            {
                return href;
            }

            return NormalizePath(JoinPath(OpfDir, href));
        }

        public static string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            var p = path.Replace('\\', '/').TrimStart('/');
            var parts = new List<string>();
            foreach (var seg in p.Split('/'))
            {
                if (seg == "..")
                {
                    if (parts.Count > 0)
                    {
                        parts.RemoveAt(parts.Count - 1);
                    }
                }
                else if (seg != "." && !string.IsNullOrEmpty(seg))
                {
                    parts.Add(seg);
                }
            }

            return string.Join("/", parts);
        }

        private static string GetDirectory(string path)
        {
            var norm = NormalizePath(path);
            var idx = norm.LastIndexOf('/');
            return idx >= 0 ? norm.Substring(0, idx) : string.Empty;
        }

        private static string JoinPath(string dir, string rel)
        {
            if (string.IsNullOrEmpty(dir))
            {
                return rel;
            }

            return dir + "/" + rel;
        }

        public List<EpubChapter> ParseNav()
        {
            // Prefer EPUB3 nav.xhtml, fall back to NCX.
            if (!string.IsNullOrEmpty(NavHtmlPath))
            {
                var nav = ParseEpub3Nav(NavHtmlPath);
                if (nav != null && nav.Count > 0)
                {
                    return nav;
                }
            }

            if (!string.IsNullOrEmpty(TocNcxPath))
            {
                var ncx = ParseNcx(TocNcxPath);
                if (ncx != null && ncx.Count > 0)
                {
                    return ncx;
                }
            }

            return new List<EpubChapter>();
        }

        private List<EpubChapter> ParseEpub3Nav(string path)
        {
            var text = ReadText(path);
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }

            try
            {
                var doc = XDocument.Parse(text, LoadOptions.PreserveWhitespace);
                var nav = doc.Descendants(Ns_Xhtml + "nav").FirstOrDefault(n =>
                    (string)n.Attribute(XName.Get("type", "http://www.idpf.org/2007/ops")) == "toc")
                    ?? doc.Descendants(Ns_Xhtml + "nav").FirstOrDefault();
                if (nav == null)
                {
                    return null;
                }

                var ol = nav.Descendants(Ns_Xhtml + "ol").FirstOrDefault();
                return ol != null ? ParseNavOl(ol, path) : new List<EpubChapter>();
            }
            catch
            {
                return null;
            }
        }

        private List<EpubChapter> ParseNavOl(XElement ol, string navPath)
        {
            var chapters = new List<EpubChapter>();
            foreach (var li in ol.Elements(Ns_Xhtml + "li"))
            {
                var a = li.Element(Ns_Xhtml + "a");
                if (a == null)
                {
                    continue;
                }

                var href = a.Attribute("href")?.Value;
                if (string.IsNullOrEmpty(href))
                {
                    continue;
                }

                var (fileRef, anchor) = SplitAnchor(href);
                var targetPath = NormalizePath(JoinPath(GetDirectory(navPath), fileRef));
                var spineIndex = Spine.FindIndex(p => p.Equals(targetPath, StringComparison.OrdinalIgnoreCase));
                if (spineIndex < 0)
                {
                    spineIndex = Spine.FindIndex(p => p.EndsWith(fileRef, StringComparison.OrdinalIgnoreCase));
                }

                var chapter = new EpubChapter
                {
                    Title = (a.Value ?? string.Empty).Trim(),
                    Page = spineIndex >= 0 ? spineIndex : 0,
                    Part = string.IsNullOrEmpty(anchor) ? null : anchor
                };

                var childOl = li.Element(Ns_Xhtml + "ol");
                if (childOl != null)
                {
                    chapter.Children = ParseNavOl(childOl, navPath);
                }

                chapters.Add(chapter);
            }

            return chapters;
        }

        private List<EpubChapter> ParseNcx(string path)
        {
            var text = ReadText(path);
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }

            try
            {
                var doc = XDocument.Parse(text);
                var navMap = doc.Descendants(Ns_Ncx + "navMap").FirstOrDefault();
                return navMap != null ? ParseNavPoints(navMap.Elements(Ns_Ncx + "navPoint"), path) : new List<EpubChapter>();
            }
            catch
            {
                return null;
            }
        }

        private List<EpubChapter> ParseNavPoints(IEnumerable<XElement> points, string ncxPath)
        {
            var chapters = new List<EpubChapter>();
            foreach (var np in points)
            {
                var label = np.Element(Ns_Ncx + "navLabel")?.Element(Ns_Ncx + "text")?.Value?.Trim() ?? string.Empty;
                var src = np.Element(Ns_Ncx + "content")?.Attribute("src")?.Value;
                if (string.IsNullOrEmpty(src))
                {
                    continue;
                }

                var (fileRef, anchor) = SplitAnchor(src);
                var target = NormalizePath(JoinPath(GetDirectory(ncxPath), fileRef));
                var idx = Spine.FindIndex(p => p.Equals(target, StringComparison.OrdinalIgnoreCase));
                if (idx < 0)
                {
                    idx = Spine.FindIndex(p => p.EndsWith(fileRef, StringComparison.OrdinalIgnoreCase));
                }

                var chapter = new EpubChapter
                {
                    Title = label,
                    Page = idx >= 0 ? idx : 0,
                    Part = string.IsNullOrEmpty(anchor) ? null : anchor
                };

                var nested = np.Elements(Ns_Ncx + "navPoint").ToList();
                if (nested.Count > 0)
                {
                    chapter.Children = ParseNavPoints(nested, ncxPath);
                }

                chapters.Add(chapter);
            }

            return chapters;
        }

        private static (string file, string anchor) SplitAnchor(string href)
        {
            var i = href.IndexOf('#');
            if (i < 0)
            {
                return (href, null);
            }

            return (href.Substring(0, i), i < href.Length - 1 ? href.Substring(i + 1) : null);
        }
    }
}
