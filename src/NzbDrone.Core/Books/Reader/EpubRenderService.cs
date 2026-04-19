using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using ExCSS;
using HtmlAgilityPack;
using NzbDrone.Common.Disk;

namespace NzbDrone.Core.Books.Reader
{
    public interface IEpubRenderService
    {
        EpubInfo GetInfo(string epubPath);
        List<EpubChapter> GetChapters(string epubPath);
        EpubPage GetPage(string epubPath, int page, string resourceUrlTemplate);
        EpubResource GetResource(string epubPath, string file);
    }

    public class EpubRenderService : IEpubRenderService
    {
        private static readonly Regex SelfClosingScriptOrTitle =
            new Regex(@"<(script|title)([^>]*)/>", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex CssUrlRegex =
            new Regex(@"url\(\s*(['""]?)([^)'""]+)\1\s*\)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private readonly IDiskProvider _diskProvider;

        public EpubRenderService(IDiskProvider diskProvider)
        {
            _diskProvider = diskProvider;
        }

        public EpubInfo GetInfo(string epubPath)
        {
            EnsureFile(epubPath);
            using var archive = new EpubArchive(epubPath);
            return new EpubInfo
            {
                Title = archive.Title,
                Author = archive.Author,
                PageCount = archive.Spine.Count,
                PageSizes = new List<long>(archive.SpineSizes)
            };
        }

        public List<EpubChapter> GetChapters(string epubPath)
        {
            EnsureFile(epubPath);
            using var archive = new EpubArchive(epubPath);
            var chapters = archive.ParseNav();
            if (chapters.Count == 0)
            {
                // Fabricate one per spine item so the UI still has navigation.
                for (var i = 0; i < archive.Spine.Count; i++)
                {
                    chapters.Add(new EpubChapter
                    {
                        Title = $"Section {i + 1}",
                        Page = i
                    });
                }
            }

            return chapters;
        }

        public EpubPage GetPage(string epubPath, int page, string resourceUrlTemplate)
        {
            EnsureFile(epubPath);
            using var archive = new EpubArchive(epubPath);

            if (archive.Spine.Count == 0 || page < 0 || page >= archive.Spine.Count)
            {
                return new EpubPage { Html = "<div class=\"book-content\"></div>" };
            }

            var pagePath = archive.Spine[page];
            var raw = archive.ReadText(pagePath) ?? string.Empty;

            // HTMLAgilityPack can't handle self-closing <script/> or <title/>.
            raw = SelfClosingScriptOrTitle.Replace(
                raw,
                m => $"<{m.Groups[1].Value}{m.Groups[2].Value}></{m.Groups[1].Value}>");

            var doc = new HtmlDocument
            {
                OptionFixNestedTags = true,
                OptionWriteEmptyNodes = true,
                OptionAutoCloseOnEnd = true
            };
            doc.LoadHtml(raw);

            var body = doc.DocumentNode.SelectSingleNode("//body");
            var bodyClass = body?.GetAttributeValue("class", null);

            // Inline <link rel="stylesheet"> and <style>
            var cssBuilder = new StringBuilder();

            var stylesheetLinks = doc.DocumentNode.SelectNodes("//link[@rel='stylesheet']");
            if (stylesheetLinks != null)
            {
                foreach (var link in stylesheetLinks)
                {
                    var href = link.GetAttributeValue("href", null);
                    if (string.IsNullOrEmpty(href))
                    {
                        continue;
                    }

                    var resolved = ResolveRelative(pagePath, href);
                    var cssText = archive.ReadText(resolved);
                    if (cssText != null)
                    {
                        cssBuilder.AppendLine(ScopeCss(cssText, resolved, resourceUrlTemplate));
                    }

                    link.Remove();
                }
            }

            var styleNodes = doc.DocumentNode.SelectNodes("//style");
            if (styleNodes != null)
            {
                foreach (var node in styleNodes)
                {
                    cssBuilder.AppendLine(ScopeCss(node.InnerText, pagePath, resourceUrlTemplate));
                    node.Remove();
                }
            }

            RewriteImageSrcs(doc.DocumentNode, pagePath, resourceUrlTemplate);
            RewriteAnchors(doc.DocumentNode);

            var bodyInner = body != null ? body.InnerHtml : doc.DocumentNode.InnerHtml;
            var wrapperClass = "book-content";
            if (!string.IsNullOrEmpty(bodyClass))
            {
                wrapperClass += " " + HtmlEntity.Entitize(bodyClass);
            }

            var sb = new StringBuilder();
            if (cssBuilder.Length > 0)
            {
                sb.Append("<style>").Append(cssBuilder).Append("</style>");
            }

            sb.Append("<div class=\"").Append(wrapperClass).Append("\">").Append(bodyInner).Append("</div>");

            return new EpubPage { Html = sb.ToString() };
        }

        public EpubResource GetResource(string epubPath, string file)
        {
            EnsureFile(epubPath);
            using var archive = new EpubArchive(epubPath);
            var path = EpubArchive.NormalizePath(file);
            var bytes = archive.ReadBytes(path);
            if (bytes == null)
            {
                return null;
            }

            string contentType = null;
            if (archive.Manifest.TryGetValue(path, out var manifestType))
            {
                contentType = manifestType;
            }

            if (string.IsNullOrEmpty(contentType))
            {
                contentType = GuessContentType(path);
            }

            return new EpubResource
            {
                Content = bytes,
                ContentType = contentType
            };
        }

        private void EnsureFile(string epubPath)
        {
            if (!_diskProvider.FileExists(epubPath))
            {
                throw new FileNotFoundException($"Epub not found: {epubPath}");
            }
        }

        private static string ResolveRelative(string fromPath, string href)
        {
            var hash = href.IndexOf('#');
            if (hash >= 0)
            {
                href = href.Substring(0, hash);
            }

            var dir = Path.GetDirectoryName((fromPath ?? string.Empty).Replace('\\', '/')) ?? string.Empty;
            return EpubArchive.NormalizePath((dir.TrimEnd('/') + "/" + href).TrimStart('/'));
        }

        private static void RewriteImageSrcs(HtmlNode root, string pagePath, string resourceUrlTemplate)
        {
            var imgs = root.SelectNodes(".//img[@src] | .//image[@*]");
            if (imgs == null)
            {
                return;
            }

            foreach (var img in imgs)
            {
                var src = img.GetAttributeValue("src", null)
                          ?? img.GetAttributeValue("xlink:href", null);
                if (string.IsNullOrEmpty(src) || src.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var resolved = ResolveRelative(pagePath, src);
                var proxy = string.Format(CultureInfo.InvariantCulture, resourceUrlTemplate, Uri.EscapeDataString(resolved));
                img.SetAttributeValue("src", proxy);
                if (img.Attributes.Contains("xlink:href"))
                {
                    img.SetAttributeValue("xlink:href", proxy);
                }
            }
        }

        private static void RewriteAnchors(HtmlNode root)
        {
            var anchors = root.SelectNodes(".//a[@href]");
            if (anchors == null)
            {
                return;
            }

            foreach (var a in anchors)
            {
                var href = a.GetAttributeValue("href", null);
                if (string.IsNullOrEmpty(href))
                {
                    continue;
                }

                if (Regex.IsMatch(href, @"^(https?:|mailto:|tel:)", RegexOptions.IgnoreCase))
                {
                    a.SetAttributeValue("target", "_blank");
                    a.SetAttributeValue("rel", "noopener noreferrer");
                    continue;
                }

                var hash = href.IndexOf('#');
                var fileRef = hash >= 0 ? href.Substring(0, hash) : href;
                var anchor = hash >= 0 ? href.Substring(hash + 1) : string.Empty;

                a.SetAttributeValue("data-epub-href", fileRef);
                if (!string.IsNullOrEmpty(anchor))
                {
                    a.SetAttributeValue("data-epub-anchor", anchor);
                }

                a.SetAttributeValue("href", "javascript:void(0)");
            }
        }

        private static string ScopeCss(string cssText, string cssPath, string resourceUrlTemplate)
        {
            if (string.IsNullOrWhiteSpace(cssText))
            {
                return string.Empty;
            }

            // Rewrite url(...) to proxy URLs first so ExCSS doesn't choke.
            cssText = CssUrlRegex.Replace(
                cssText,
                m =>
                {
                    var val = m.Groups[2].Value;
                    if (val.StartsWith("data:", StringComparison.OrdinalIgnoreCase) || val.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    {
                        return m.Value;
                    }

                    var resolved = ResolveRelative(cssPath, val);
                    var proxy = string.Format(CultureInfo.InvariantCulture, resourceUrlTemplate, Uri.EscapeDataString(resolved));
                    return $"url('{proxy}')";
                });

            try
            {
                var parser = new StylesheetParser();
                var sheet = parser.Parse(cssText);
                var output = new StringBuilder();
                foreach (var rule in sheet.Children)
                {
                    AppendScopedRule(rule, output);
                }

                return output.ToString();
            }
            catch
            {
                // Worst case: ship URL-rewritten CSS unscoped.
                return cssText;
            }
        }

        private static void AppendScopedRule(IStylesheetNode node, StringBuilder output)
        {
            if (node is IStyleRule style)
            {
                output.Append(ScopeSelector(style.SelectorText)).Append(" { ").Append(style.Style.CssText).Append(" }\n");
                return;
            }

            if (node is IMediaRule media)
            {
                output.Append("@media ").Append(media.Media.MediaText).Append(" {\n");
                foreach (var child in media.Children)
                {
                    AppendScopedRule(child, output);
                }

                output.Append("}\n");
                return;
            }

            // @font-face, @keyframes, etc — emit as-is.
            if (node is IRule rule)
            {
                output.Append(rule.Text).Append('\n');
            }
        }

        private static string ScopeSelector(string selector)
        {
            if (string.IsNullOrWhiteSpace(selector))
            {
                return ".book-content";
            }

            var parts = selector.Split(',');
            for (var i = 0; i < parts.Length; i++)
            {
                var t = parts[i].Trim();
                if (string.IsNullOrEmpty(t))
                {
                    continue;
                }

                if (Regex.IsMatch(t, @"^(body|html)\b", RegexOptions.IgnoreCase))
                {
                    t = Regex.Replace(t, @"^(body|html)", ".book-content", RegexOptions.IgnoreCase);
                }
                else
                {
                    t = ".book-content " + t;
                }

                parts[i] = t;
            }

            return string.Join(", ", parts);
        }

        private static string GuessContentType(string path)
        {
            var ext = Path.GetExtension(path ?? string.Empty).ToLowerInvariant();
            return ext switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".svg" => "image/svg+xml",
                ".webp" => "image/webp",
                ".css" => "text/css",
                ".woff" => "font/woff",
                ".woff2" => "font/woff2",
                ".ttf" => "font/ttf",
                ".otf" => "font/otf",
                _ => "application/octet-stream"
            };
        }
    }
}
