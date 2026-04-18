using System.Linq;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Users
{
    public interface IUserBookProgressRepository : IBasicRepository<UserBookProgress>
    {
        UserBookProgress Find(int userId, int bookFileId);
    }

    public class UserBookProgressRepository : BasicRepository<UserBookProgress>, IUserBookProgressRepository
    {
        public UserBookProgressRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }

        public UserBookProgress Find(int userId, int bookFileId)
        {
            return Query(x => x.UserId == userId && x.BookFileId == bookFileId).SingleOrDefault();
        }
    }
}
