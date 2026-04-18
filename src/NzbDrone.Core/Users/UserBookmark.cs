using System;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Users
{
    public class UserBookmark : ModelBase
    {
        public int UserId { get; set; }
        public int BookFileId { get; set; }
        public string Location { get; set; }
        public string Note { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
