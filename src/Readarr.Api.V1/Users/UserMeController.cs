using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
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

        public UserMeController(IUserBookProgressRepository progressRepo,
                                IUserBookmarkRepository bookmarkRepo)
        {
            _progressRepo = progressRepo;
            _bookmarkRepo = bookmarkRepo;
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
    }
}
