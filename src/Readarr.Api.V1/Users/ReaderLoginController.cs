using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Authentication;

namespace Readarr.Api.V1.Users
{
    // Login endpoint for the /reader portal. Lives outside the /api/v1/* tree so
    // it can be AllowAnonymous without poking holes in the admin ApiKey scheme.
    [AllowAnonymous]
    [ApiController]
    [Route("reader/api")]
    public class ReaderLoginController : Controller
    {
        private readonly IUserService _userService;

        public ReaderLoginController(IUserService userService)
        {
            _userService = userService;
        }

        public class LoginRequest
        {
            public string Username { get; set; }
            public string Password { get; set; }
        }

        public class LoginResponse
        {
            public int Id { get; set; }
            public string Username { get; set; }
            public string Role { get; set; }
            public string ApiKey { get; set; }
        }

        [HttpPost("login")]
        public ActionResult<LoginResponse> Login([FromBody] LoginRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new { message = "username and password required" });
            }

            var user = _userService.FindUser(request.Username, request.Password);

            if (user == null)
            {
                return Unauthorized(new { message = "Invalid username or password" });
            }

            if (string.IsNullOrEmpty(user.ApiKey))
            {
                // Migration 044 should have populated this; regenerate defensively if something skipped it.
                _userService.RegenerateApiKey(user.Id);
                user = _userService.FindUser(user.Id);
            }

            return new LoginResponse
            {
                Id = user.Id,
                Username = user.Username,
                Role = user.Role.ToString(),
                ApiKey = user.ApiKey
            };
        }
    }
}
