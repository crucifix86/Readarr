using System;
using NzbDrone.Core.Users;

namespace Readarr.Api.V1.Users
{
    public class UserFavoriteResource
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int BookId { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public static class UserFavoriteResourceMapper
    {
        public static UserFavoriteResource ToResource(this UserFavorite model)
        {
            if (model == null)
            {
                return null;
            }

            return new UserFavoriteResource
            {
                Id = model.Id,
                UserId = model.UserId,
                BookId = model.BookId,
                CreatedAt = model.CreatedAt
            };
        }
    }
}
