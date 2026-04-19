using System.Collections.Generic;

namespace NzbDrone.Core.Books.Reader
{
    public class EpubInfo
    {
        public string Title { get; set; }
        public string Author { get; set; }
        public int PageCount { get; set; }
    }

    public class EpubChapter
    {
        public string Title { get; set; }

        // Zero-based page (spine item) index the chapter starts at.
        public int Page { get; set; }

        // Anchor fragment (without '#') within Page, if any.
        public string Part { get; set; }

        public List<EpubChapter> Children { get; set; } = new List<EpubChapter>();
    }

    public class EpubPage
    {
        // Body-only HTML fragment, already wrapped in
        // <div class="book-content ...">...</div> with CSS scoped and
        // img/src rewritten to the resource proxy endpoint.
        public string Html { get; set; }
    }

    public class EpubResource
    {
        public byte[] Content { get; set; }
        public string ContentType { get; set; }
    }
}
