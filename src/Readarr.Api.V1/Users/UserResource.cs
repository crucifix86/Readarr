using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Authentication;
using Readarr.Http.REST;

namespace Readarr.Api.V1.Users
{
    public class UserResource : RestResource
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public UserRole Role { get; set; }
        public string ApiKey { get; set; }
        public string Email { get; set; }
        public DateTime? CreatedAt { get; set; }
    }

    public static class UserResourceMapper
    {
        public static UserResource ToResource(this User model)
        {
            if (model == null)
            {
                return null;
            }

            return new UserResource
            {
                Id = model.Id,
                Username = model.Username,
                Role = model.Role,
                ApiKey = model.ApiKey,
                Email = model.Email,
                CreatedAt = model.CreatedAt
            };
        }

        public static List<UserResource> ToResource(this IEnumerable<User> models)
        {
            return models.Select(ToResource).ToList();
        }
    }
}
