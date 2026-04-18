using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NzbDrone.Core.Authentication;
using NzbDrone.Core.Configuration;

namespace Readarr.Http.Authentication
{
    public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
    {
        public const string DefaultScheme = "API Key";

        public string Scheme => DefaultScheme;
        public string AuthenticationType = DefaultScheme;

        public string HeaderName { get; set; }
        public string QueryName { get; set; }
    }

    public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
    {
        public const string UserIdClaim = "ReadarrUserId";
        public const string UserRoleClaim = "ReadarrUserRole";

        private readonly string _apiKey;
        private readonly IUserService _userService;

        public ApiKeyAuthenticationHandler(IOptionsMonitor<ApiKeyAuthenticationOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            IConfigFileProvider config,
            IUserService userService)
            : base(options, logger, encoder)
        {
            _apiKey = config.ApiKey;
            _userService = userService;
        }

        private string ParseApiKey()
        {
            // Try query parameter
            if (Request.Query.TryGetValue(Options.QueryName, out var value))
            {
                return value.FirstOrDefault();
            }

            // No ApiKey query parameter found try headers
            if (Request.Headers.TryGetValue(Options.HeaderName, out var headerValue))
            {
                return headerValue.FirstOrDefault();
            }

            return Request.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", "");
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var providedApiKey = ParseApiKey();

            if (string.IsNullOrWhiteSpace(providedApiKey))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            if (_apiKey == providedApiKey)
            {
                return Task.FromResult(AuthenticateResult.Success(BuildTicket(null)));
            }

            var user = _userService.FindUserByApiKey(providedApiKey);

            if (user != null)
            {
                return Task.FromResult(AuthenticateResult.Success(BuildTicket(user)));
            }

            return Task.FromResult(AuthenticateResult.NoResult());
        }

        private AuthenticationTicket BuildTicket(User user)
        {
            var claims = new List<Claim> { new Claim("ApiKey", "true") };

            if (user != null)
            {
                claims.Add(new Claim(ClaimTypes.Name, user.Username));
                claims.Add(new Claim(UserIdClaim, user.Id.ToString()));
                claims.Add(new Claim(UserRoleClaim, user.Role.ToString()));
            }

            var identity = new ClaimsIdentity(claims, Options.AuthenticationType);
            var principal = new ClaimsPrincipal(identity);
            return new AuthenticationTicket(principal, Options.Scheme);
        }

        protected override Task HandleChallengeAsync(AuthenticationProperties properties)
        {
            Response.StatusCode = 401;
            return Task.CompletedTask;
        }

        protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
        {
            Response.StatusCode = 403;
            return Task.CompletedTask;
        }
    }
}
