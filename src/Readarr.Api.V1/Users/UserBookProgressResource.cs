using System;
using NzbDrone.Core.Users;

namespace Readarr.Api.V1.Users
{
    public class UserBookProgressResource
    {
        public int UserId { get; set; }
        public int BookFileId { get; set; }
        public string Location { get; set; }
        public double? Percent { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public static class UserBookProgressResourceMapper
    {
        public static UserBookProgressResource ToResource(this UserBookProgress model)
        {
            if (model == null)
            {
                return null;
            }

            return new UserBookProgressResource
            {
                UserId = model.UserId,
                BookFileId = model.BookFileId,
                Location = model.Location,
                Percent = model.Percent,
                UpdatedAt = model.UpdatedAt
            };
        }
    }
}
