using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Users
{
    public interface IUserBookmarkRepository : IBasicRepository<UserBookmark>
    {
        List<UserBookmark> ForUserAndBookFile(int userId, int bookFileId);
    }

    public class UserBookmarkRepository : BasicRepository<UserBookmark>, IUserBookmarkRepository
    {
        public UserBookmarkRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }

        public List<UserBookmark> ForUserAndBookFile(int userId, int bookFileId)
        {
            return Query(x => x.UserId == userId && x.BookFileId == bookFileId).ToList();
        }
    }
}
