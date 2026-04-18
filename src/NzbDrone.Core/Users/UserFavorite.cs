using System;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Users
{
    public class UserFavorite : ModelBase
    {
        public int UserId { get; set; }
        public int BookId { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
