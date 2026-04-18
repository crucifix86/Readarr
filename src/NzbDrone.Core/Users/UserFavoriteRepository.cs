using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Users
{
    public interface IUserFavoriteRepository : IBasicRepository<UserFavorite>
    {
        UserFavorite Find(int userId, int bookId);
        List<UserFavorite> ForUser(int userId);
    }

    public class UserFavoriteRepository : BasicRepository<UserFavorite>, IUserFavoriteRepository
    {
        public UserFavoriteRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }

        public UserFavorite Find(int userId, int bookId)
        {
            return Query(x => x.UserId == userId && x.BookId == bookId).SingleOrDefault();
        }

        public List<UserFavorite> ForUser(int userId)
        {
            return Query(x => x.UserId == userId).ToList();
        }
    }
}
