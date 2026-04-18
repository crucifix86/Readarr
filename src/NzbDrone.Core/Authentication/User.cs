using System;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Authentication
{
    public class User : ModelBase
    {
        public Guid Identifier { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public UserRole Role { get; set; }
        public string ApiKey { get; set; }
        public string Email { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}
