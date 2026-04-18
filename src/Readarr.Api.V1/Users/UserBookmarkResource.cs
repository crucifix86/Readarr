using System;
using NzbDrone.Core.Users;

namespace Readarr.Api.V1.Users
{
    public class UserBookmarkResource
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int BookFileId { get; set; }
        public string Location { get; set; }
        public string Note { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public static class UserBookmarkResourceMapper
    {
        public static UserBookmarkResource ToResource(this UserBookmark model)
        {
            if (model == null)
            {
                return null;
            }

            return new UserBookmarkResource
            {
                Id = model.Id,
                UserId = model.UserId,
                BookFileId = model.BookFileId,
                Location = model.Location,
                Note = model.Note,
                CreatedAt = model.CreatedAt
            };
        }
    }
}
