using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Books;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Users;
using Readarr.Http;

namespace Readarr.Api.V1.Users
{
    // Per-user state scoped to the authenticated caller. Requires a per-user
    // ApiKey — the global config ApiKey has no user identity and is rejected
    // with 400 here.
    [V1ApiController("user/me")]
    public class UserMeController : Controller
    {
        private readonly IUserBookProgressRepository _progressRepo;
        private readonly IUserBookmarkRepository _bookmarkRepo;
        private readonly IUserFavoriteRepository _favoriteRepo;
        private readonly IBookService _bookService;
        private readonly IAuthorService _authorService;
        private readonly IMediaFileService _mediaFileService;

        public UserMeController(IUserBookProgressRepository progressRepo,
                                IUserBookmarkRepository bookmarkRepo,
                                IUserFavoriteRepository favoriteRepo,
                                IBookService bookService,
                                IAuthorService authorService,
                                IMediaFileService mediaFileService)
        {
            _progressRepo = progressRepo;
            _bookmarkRepo = bookmarkRepo;
            _favoriteRepo = favoriteRepo;
            _bookService = bookService;
            _authorService = authorService;
            _mediaFileService = mediaFileService;
        }

        public class ReaderLibraryItem
        {
            public int BookId { get; set; }
            public string Title { get; set; }
            public string AuthorName { get; set; }
            public int? FirstBookFileId { get; set; }
            public string FirstBookFileFormat { get; set; }
            public bool IsFavorite { get; set; }
            public double? ProgressPercent { get; set; }
        }

        [HttpGet("library")]
        public ActionResult<List<ReaderLibraryItem>> GetLibrary()
        {
            var userId = UserMeHelpers.CurrentUserId(HttpContext);

            if (userId == null)
            {
                return BadRequest(new { message = "A per-user ApiKey is required for /user/me endpoints" });
            }

            var books = _bookService.GetAllBooks();
            var authors = _authorService.GetAllAuthors().ToDictionary(a => a.Id);
            var favIds = new HashSet<int>(_favoriteRepo.ForUser(userId.Value).Select(f => f.BookId));
            var progressByFile = _progressRepo.ForUser(userId.Value)
                .Where(p => p.Percent.HasValue)
                .ToDictionary(p => p.BookFileId, p => p.Percent);

            var result = new List<ReaderLibraryItem>(books.Count);

            foreach (var book in books)
            {
                var files = _mediaFileService.GetFilesByBook(book.Id);
                if (files == null || files.Count == 0)
                {
                    continue;
                }

                var primary = files.First();
                string format = null;
                if (!string.IsNullOrEmpty(primary.Path))
                {
                    var ext = Path.GetExtension(primary.Path);
                    format = ext != null ? ext.TrimStart('.').ToLowerInvariant() : null;
                }

                string authorName = null;
                if (authors.TryGetValue(book.AuthorId, out var author))
                {
                    authorName = author.Metadata?.Value?.Name;
                }

                result.Add(new ReaderLibraryItem
                {
                    BookId = book.Id,
                    Title = book.Title,
                    AuthorName = authorName ?? "Unknown",
                    FirstBookFileId = primary.Id,
                    FirstBookFileFormat = format,
                    IsFavorite = favIds.Contains(book.Id),
                    ProgressPercent = progressByFile.TryGetValue(primary.Id, out var pct) ? pct : null
                });
            }

            return result.OrderBy(r => r.Title, StringComparer.OrdinalIgnoreCase).ToList();
        }

        [HttpGet("progress")]
        public ActionResult<List<UserBookProgressResource>> ListProgress()
        {
            var userId = UserMeHelpers.CurrentUserId(HttpContext);

            if (userId == null)
            {
                return BadRequest(new { message = "A per-user ApiKey is required for /user/me endpoints" });
            }

            return _progressRepo.ForUser(userId.Value)
                .Select(p => p.ToResource())
                .ToList();
        }

        [HttpGet("progress/{bookFileId:int}")]
        public ActionResult<UserBookProgressResource> GetProgress(int bookFileId)
        {
            var userId = UserMeHelpers.CurrentUserId(HttpContext);

            if (userId == null)
            {
                return BadRequest(new { message = "A per-user ApiKey is required for /user/me endpoints" });
            }

            var progress = _progressRepo.Find(userId.Value, bookFileId);

            if (progress == null)
            {
                return NoContent();
            }

            return progress.ToResource();
        }

        [HttpPut("progress/{bookFileId:int}")]
        public ActionResult<UserBookProgressResource> UpsertProgress(int bookFileId, [FromBody] UserBookProgressResource resource)
        {
            var userId = UserMeHelpers.CurrentUserId(HttpContext);

            if (userId == null)
            {
                return BadRequest(new { message = "A per-user ApiKey is required for /user/me endpoints" });
            }

            if (resource == null)
            {
                return BadRequest(new { message = "Request body required" });
            }

            var existing = _progressRepo.Find(userId.Value, bookFileId);

            if (existing == null)
            {
                var created = _progressRepo.Insert(new UserBookProgress
                {
                    UserId = userId.Value,
                    BookFileId = bookFileId,
                    Location = resource.Location,
                    Percent = resource.Percent,
                    UpdatedAt = DateTime.UtcNow
                });

                return created.ToResource();
            }

            existing.Location = resource.Location;
            existing.Percent = resource.Percent;
            existing.UpdatedAt = DateTime.UtcNow;
            _progressRepo.Update(existing);

            return existing.ToResource();
        }

        [HttpGet("bookmarks")]
        public ActionResult<List<UserBookmarkResource>> ListBookmarks([FromQuery] int bookFileId)
        {
            var userId = UserMeHelpers.CurrentUserId(HttpContext);

            if (userId == null)
            {
                return BadRequest(new { message = "A per-user ApiKey is required for /user/me endpoints" });
            }

            if (bookFileId <= 0)
            {
                return BadRequest(new { message = "bookFileId query param required" });
            }

            return _bookmarkRepo.ForUserAndBookFile(userId.Value, bookFileId)
                .Select(b => b.ToResource())
                .ToList();
        }

        [HttpPost("bookmarks")]
        public ActionResult<UserBookmarkResource> AddBookmark([FromBody] UserBookmarkResource resource)
        {
            var userId = UserMeHelpers.CurrentUserId(HttpContext);

            if (userId == null)
            {
                return BadRequest(new { message = "A per-user ApiKey is required for /user/me endpoints" });
            }

            if (resource == null || resource.BookFileId <= 0 || string.IsNullOrWhiteSpace(resource.Location))
            {
                return BadRequest(new { message = "bookFileId + location required" });
            }

            var created = _bookmarkRepo.Insert(new UserBookmark
            {
                UserId = userId.Value,
                BookFileId = resource.BookFileId,
                Location = resource.Location,
                Note = resource.Note,
                CreatedAt = DateTime.UtcNow
            });

            return created.ToResource();
        }

        [HttpDelete("bookmarks/{id:int}")]
        public ActionResult DeleteBookmark(int id)
        {
            var userId = UserMeHelpers.CurrentUserId(HttpContext);

            if (userId == null)
            {
                return BadRequest(new { message = "A per-user ApiKey is required for /user/me endpoints" });
            }

            var existing = _bookmarkRepo.Get(id);

            if (existing == null || existing.UserId != userId.Value)
            {
                return NotFound();
            }

            _bookmarkRepo.Delete(id);
            return Ok();
        }

        [HttpGet("favorites")]
        public ActionResult<List<UserFavoriteResource>> ListFavorites()
        {
            var userId = UserMeHelpers.CurrentUserId(HttpContext);

            if (userId == null)
            {
                return BadRequest(new { message = "A per-user ApiKey is required for /user/me endpoints" });
            }

            return _favoriteRepo.ForUser(userId.Value)
                .Select(f => f.ToResource())
                .ToList();
        }

        [HttpPost("favorites/{bookId:int}")]
        public ActionResult<UserFavoriteResource> AddFavorite(int bookId)
        {
            var userId = UserMeHelpers.CurrentUserId(HttpContext);

            if (userId == null)
            {
                return BadRequest(new { message = "A per-user ApiKey is required for /user/me endpoints" });
            }

            var existing = _favoriteRepo.Find(userId.Value, bookId);

            if (existing != null)
            {
                return existing.ToResource();
            }

            var created = _favoriteRepo.Insert(new UserFavorite
            {
                UserId = userId.Value,
                BookId = bookId,
                CreatedAt = DateTime.UtcNow
            });

            return created.ToResource();
        }

        [HttpDelete("favorites/{bookId:int}")]
        public ActionResult DeleteFavorite(int bookId)
        {
            var userId = UserMeHelpers.CurrentUserId(HttpContext);

            if (userId == null)
            {
                return BadRequest(new { message = "A per-user ApiKey is required for /user/me endpoints" });
            }

            var existing = _favoriteRepo.Find(userId.Value, bookId);

            if (existing == null)
            {
                return NotFound();
            }

            _favoriteRepo.Delete(existing.Id);
            return Ok();
        }
    }
}
