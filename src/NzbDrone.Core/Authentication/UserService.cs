using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Common.Disk;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Common.Extensions;

namespace NzbDrone.Core.Authentication
{
    public interface IUserService
    {
        User Add(string username, string password);
        User Add(string username, string password, UserRole role, string email);
        User Update(User user);
        User Upsert(string username, string password);
        User FindUser();
        User FindUser(string username, string password);
        User FindUser(Guid identifier);
        User FindUser(int id);
        User FindUserByApiKey(string apiKey);
        List<User> All();
        void Delete(int id);
        string RegenerateApiKey(int id);
        void ChangePassword(int id, string password);
    }

    public class UserService : IUserService
    {
        private readonly IUserRepository _repo;
        private readonly IAppFolderInfo _appFolderInfo;
        private readonly IDiskProvider _diskProvider;

        public UserService(IUserRepository repo, IAppFolderInfo appFolderInfo, IDiskProvider diskProvider)
        {
            _repo = repo;
            _appFolderInfo = appFolderInfo;
            _diskProvider = diskProvider;
        }

        public User Add(string username, string password)
        {
            return Add(username, password, UserRole.Admin, null);
        }

        public User Add(string username, string password, UserRole role, string email)
        {
            return _repo.Insert(new User
            {
                Identifier = Guid.NewGuid(),
                Username = username.ToLowerInvariant(),
                Password = password.SHA256Hash(),
                Role = role,
                ApiKey = GenerateApiKey(),
                Email = email,
                CreatedAt = DateTime.UtcNow
            });
        }

        public User Update(User user)
        {
            return _repo.Update(user);
        }

        public User Upsert(string username, string password)
        {
            var user = FindUser();

            if (user == null)
            {
                return Add(username, password);
            }

            if (user.Password != password)
            {
                user.Password = password.SHA256Hash();
            }

            user.Username = username.ToLowerInvariant();

            return Update(user);
        }

        public User FindUser()
        {
            return _repo.All().OrderBy(u => u.Id).FirstOrDefault();
        }

        public User FindUser(string username, string password)
        {
            if (username.IsNullOrWhiteSpace() || password.IsNullOrWhiteSpace())
            {
                return null;
            }

            var user = _repo.FindUser(username.ToLowerInvariant());

            if (user == null)
            {
                return null;
            }

            if (user.Password == password.SHA256Hash())
            {
                return user;
            }

            return null;
        }

        public User FindUser(Guid identifier)
        {
            return _repo.FindUser(identifier);
        }

        public User FindUser(int id)
        {
            return _repo.Get(id);
        }

        public User FindUserByApiKey(string apiKey)
        {
            if (apiKey.IsNullOrWhiteSpace())
            {
                return null;
            }

            return _repo.FindByApiKey(apiKey);
        }

        public List<User> All()
        {
            return _repo.All().OrderBy(u => u.Id).ToList();
        }

        public void Delete(int id)
        {
            _repo.Delete(id);
        }

        public string RegenerateApiKey(int id)
        {
            var user = _repo.Get(id);
            user.ApiKey = GenerateApiKey();
            _repo.Update(user);
            return user.ApiKey;
        }

        public void ChangePassword(int id, string password)
        {
            var user = _repo.Get(id);
            user.Password = password.SHA256Hash();
            _repo.Update(user);
        }

        private static string GenerateApiKey()
        {
            return Guid.NewGuid().ToString("N");
        }
    }
}
