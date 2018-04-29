using System.Threading.Tasks;
using AspNet.Security.OAuth.Validation;
using AspNet.Security.OpenIdConnect.Primitives;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using OpenIddict.Core;

using DotNetCoreAuthorizationServer.Data;
using DotNetCoreAuthorizationServer.Models;

namespace DotNetCoreAuthorizationServer.Controllers
{
    [Produces("application/json")]
    [Authorize, Route("api/[controller]")]
    public class UserInfoController : Controller
    {
        private readonly UserManager<AppIdentityUser> _userManager;

        public UserInfoController(UserManager<AppIdentityUser> userManager)
        {
            _userManager = userManager;
        }

        [Authorize(AuthenticationSchemes = OAuthValidationDefaults.AuthenticationScheme)]
        [HttpGet()]
        public async Task<IActionResult> Userinfo()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return BadRequest(new OpenIdConnectResponse
                {
                    Error = OpenIdConnectConstants.Errors.InvalidGrant,
                    ErrorDescription = "The user profile is no longer available."
                });
            }

            var claims = new JObject
            {
                // Note: the "sub" claim is a mandatory claim and must be included in the JSON response.
                [OpenIdConnectConstants.Claims.Subject] = await _userManager.GetUserIdAsync(user)
            };

            if (User.HasClaim(OpenIdConnectConstants.Claims.Scope, OpenIdConnectConstants.Scopes.Email))
            {
                claims[OpenIdConnectConstants.Claims.Email] = await _userManager.GetEmailAsync(user);
                claims[OpenIdConnectConstants.Claims.EmailVerified] = await _userManager.IsEmailConfirmedAsync(user);
            }

            if (User.HasClaim(OpenIdConnectConstants.Claims.Scope, OpenIddictConstants.Scopes.Roles))
            {
                claims["roles"] = JArray.FromObject(await _userManager.GetRolesAsync(user));
            }

            // Note: the complete list of standard claims supported by the OpenID Connect specification
            // can be found here: http://openid.net/specs/openid-connect-core-1_0.html#StandardClaims

            return Json(claims);
        }
    }
}