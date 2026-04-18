using System.Collections.Generic;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Authentication;
using NzbDrone.Http.REST.Attributes;
using Readarr.Http;
using Readarr.Http.Authentication;
using Readarr.Http.REST;

namespace Readarr.Api.V1.Users
{
    [V1ApiController]
    public class UserController : RestController<UserResource>
    {
        private readonly IUserService _userService;

        public UserController(IUserService userService)
        {
            _userService = userService;

            SharedValidator.RuleFor(u => u.Username).NotEmpty();
            PostValidator.RuleFor(u => u.Password).NotEmpty();
        }

        protected override UserResource GetResourceById(int id)
        {
            RequireAdmin();
            var user = _userService.FindUser(id);

            if (user == null)
            {
                throw new NzbDrone.Core.Datastore.ModelNotFoundException(typeof(User), id);
            }

            return user.ToResource();
        }

        [HttpGet]
        public List<UserResource> GetAll()
        {
            RequireAdmin();
            return _userService.All().ToResource();
        }

        [RestPostById]
        public ActionResult<UserResource> Create([FromBody] UserResource resource)
        {
            RequireAdmin();
            var created = _userService.Add(resource.Username, resource.Password, resource.Role, resource.Email);
            return Created(created.Id);
        }

        [RestPutById]
        public ActionResult<UserResource> Update([FromBody] UserResource resource)
        {
            RequireAdmin();
            var user = _userService.FindUser(resource.Id);

            if (user == null)
            {
                throw new NzbDrone.Core.Datastore.ModelNotFoundException(typeof(User), resource.Id);
            }

            user.Username = resource.Username.ToLowerInvariant();
            user.Role = resource.Role;
            user.Email = resource.Email;

            _userService.Update(user);

            if (resource.Password.IsNotNullOrWhiteSpace())
            {
                _userService.ChangePassword(user.Id, resource.Password);
            }

            return Accepted(user.Id);
        }

        [RestDeleteById]
        public void DeleteUser(int id)
        {
            RequireAdmin();
            _userService.Delete(id);
        }

        [HttpPost("{id:int}/regenerateApiKey")]
        public ActionResult<UserResource> RegenerateApiKey(int id)
        {
            RequireAdmin();
            _userService.RegenerateApiKey(id);
            return _userService.FindUser(id).ToResource();
        }

        private void RequireAdmin()
        {
            var principal = HttpContext?.User;

            if (principal == null)
            {
                return;
            }

            var roleClaim = principal.FindFirst(ApiKeyAuthenticationHandler.UserRoleClaim);

            // Global-apikey and UI-auth requests carry no per-user role claim — treat as admin (legacy behavior).
            if (roleClaim == null)
            {
                return;
            }

            if (roleClaim.Value != UserRole.Admin.ToString())
            {
                throw new Readarr.Http.REST.MethodNotAllowedException("Admin role required");
            }
        }
    }
}
