using System;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Users
{
    public class UserBookProgress : ModelBase
    {
        public int UserId { get; set; }
        public int BookFileId { get; set; }
        public string Location { get; set; }
        public double? Percent { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
