using System;
using System.Collections.Generic;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Books.Reader;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.MediaFiles;
using Readarr.Http;

namespace Readarr.Api.V1.Users
{
    // Server-side EPUB rendering. We unzip + normalize per-request; the
    // frontend is a dumb HTML viewer. See EpubRenderService for details.
    //
    // All routes require a per-user ApiKey (the regular X-Api-Key scheme) —
    // the "resource" endpoint also accepts ?apikey=... so <img> tags work
    // without having to set headers from the browser.
    [V1ApiController("user/me/epub")]
    public class EpubReaderController : Controller
    {
        private readonly IEpubRenderService _epub;
        private readonly IMediaFileService _mediaFileService;

        public EpubReaderController(IEpubRenderService epub, IMediaFileService mediaFileService)
        {
            _epub = epub;
            _mediaFileService = mediaFileService;
        }

        public class EpubInfoResponse
        {
            public int BookFileId { get; set; }
            public string Title { get; set; }
            public string Author { get; set; }
            public int PageCount { get; set; }
        }

        public class EpubChapterResponse
        {
            public string Title { get; set; }
            public int Page { get; set; }
            public string Part { get; set; }
            public List<EpubChapterResponse> Children { get; set; }
        }

        [HttpGet("{bookFileId:int}/info")]
        public ActionResult<EpubInfoResponse> Info(int bookFileId)
        {
            var userId = UserMeHelpers.CurrentUserId(HttpContext);
            if (userId == null)
            {
                return BadRequest(new { message = "A per-user ApiKey is required for /user/me endpoints" });
            }

            BookFile file;
            try
            {
                file = _mediaFileService.Get(bookFileId);
            }
            catch (ModelNotFoundException)
            {
                return NotFound();
            }

            if (file == null || string.IsNullOrEmpty(file.Path))
            {
                return NotFound();
            }

            try
            {
                var info = _epub.GetInfo(file.Path);
                return new EpubInfoResponse
                {
                    BookFileId = bookFileId,
                    Title = info.Title,
                    Author = info.Author,
                    PageCount = info.PageCount
                };
            }
            catch (Exception e)
            {
                return Problem(detail: e.Message, statusCode: 500);
            }
        }

        [HttpGet("{bookFileId:int}/chapters")]
        public ActionResult<List<EpubChapterResponse>> Chapters(int bookFileId)
        {
            var userId = UserMeHelpers.CurrentUserId(HttpContext);
            if (userId == null)
            {
                return BadRequest(new { message = "A per-user ApiKey is required for /user/me endpoints" });
            }

            BookFile file;
            try
            {
                file = _mediaFileService.Get(bookFileId);
            }
            catch (ModelNotFoundException)
            {
                return NotFound();
            }

            if (file == null || string.IsNullOrEmpty(file.Path))
            {
                return NotFound();
            }

            try
            {
                var chapters = _epub.GetChapters(file.Path);
                return MapChapters(chapters);
            }
            catch (Exception e)
            {
                return Problem(detail: e.Message, statusCode: 500);
            }
        }

        [HttpGet("{bookFileId:int}/page/{page:int}")]
        public ActionResult Page(int bookFileId, int page)
        {
            var userId = UserMeHelpers.CurrentUserId(HttpContext);
            if (userId == null)
            {
                return BadRequest(new { message = "A per-user ApiKey is required for /user/me endpoints" });
            }

            BookFile file;
            try
            {
                file = _mediaFileService.Get(bookFileId);
            }
            catch (ModelNotFoundException)
            {
                return NotFound();
            }

            if (file == null || string.IsNullOrEmpty(file.Path))
            {
                return NotFound();
            }

            try
            {
                // Rewrite book-embedded img/src/url(...) to a proxy URL whose
                // {0} placeholder is the URL-encoded resource path.
                var resourceTemplate = $"{Request.PathBase}/api/v1/user/me/epub/{bookFileId}/resource?apikey={WebUtility.UrlEncode(GetApiKeyForRewriting())}&file={{0}}";
                var result = _epub.GetPage(file.Path, page, resourceTemplate);
                return Content(result.Html ?? string.Empty, "text/html");
            }
            catch (Exception e)
            {
                return Problem(detail: e.Message, statusCode: 500);
            }
        }

        [HttpGet("{bookFileId:int}/resource")]
        public ActionResult Resource(int bookFileId, [FromQuery] string file)
        {
            var userId = UserMeHelpers.CurrentUserId(HttpContext);
            if (userId == null)
            {
                return BadRequest(new { message = "A per-user ApiKey is required for /user/me endpoints" });
            }

            if (string.IsNullOrEmpty(file))
            {
                return BadRequest(new { message = "file query param required" });
            }

            BookFile bookFile;
            try
            {
                bookFile = _mediaFileService.Get(bookFileId);
            }
            catch (ModelNotFoundException)
            {
                return NotFound();
            }

            if (bookFile == null || string.IsNullOrEmpty(bookFile.Path))
            {
                return NotFound();
            }

            try
            {
                var res = _epub.GetResource(bookFile.Path, file);
                if (res == null)
                {
                    return NotFound();
                }

                return File(res.Content, res.ContentType);
            }
            catch (Exception e)
            {
                return Problem(detail: e.Message, statusCode: 500);
            }
        }

        private string GetApiKeyForRewriting()
        {
            // Prefer the key the browser actually sent; falls back to query.
            if (Request.Headers.TryGetValue("X-Api-Key", out var header))
            {
                return header.ToString();
            }

            return Request.Query["apikey"].ToString();
        }

        private static List<EpubChapterResponse> MapChapters(List<EpubChapter> source)
        {
            var result = new List<EpubChapterResponse>(source.Count);
            foreach (var c in source)
            {
                result.Add(new EpubChapterResponse
                {
                    Title = c.Title,
                    Page = c.Page,
                    Part = c.Part,
                    Children = c.Children != null && c.Children.Count > 0 ? MapChapters(c.Children) : null
                });
            }

            return result;
        }
    }
}
