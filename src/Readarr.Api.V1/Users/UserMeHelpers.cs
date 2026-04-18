using Microsoft.AspNetCore.Http;
using Readarr.Http.Authentication;

namespace Readarr.Api.V1.Users
{
    internal static class UserMeHelpers
    {
        public static int? CurrentUserId(HttpContext context)
        {
            var claim = context?.User?.FindFirst(ApiKeyAuthenticationHandler.UserIdClaim);

            if (claim == null)
            {
                return null;
            }

            return int.TryParse(claim.Value, out var id) ? id : (int?)null;
        }
    }
}
