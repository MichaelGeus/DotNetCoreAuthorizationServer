﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using AspNet.Security.OpenIdConnect.Extensions;
using AspNet.Security.OpenIdConnect.Primitives;
using AspNet.Security.OpenIdConnect.Server;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using OpenIddict.Core;

using DotNetCoreAuthorizationServer.Data;

namespace DotNetCoreAuthorizationServer.Controllers
{
    public class AuthorizationController : Controller
    {
        private readonly IOptions<IdentityOptions> _identityOptions;
        private readonly SignInManager<AppIdentityUser> _signInManager;
        private readonly UserManager<AppIdentityUser> _userManager;


        public AuthorizationController(
            IOptions<IdentityOptions> identityOptions,
            SignInManager<AppIdentityUser> signInManager,
            UserManager<AppIdentityUser> userManager)
        {
            _identityOptions = identityOptions;
            _signInManager = signInManager;
            _userManager = userManager;
        }

        #region Password, authorization code and refresh token flows
        // Note: to support non-interactive flows like password,
        // you must provide your own token endpoint action:

        [HttpPost("~/connect/token"), Produces("application/json")]
        public async Task<IActionResult> Exchange(OpenIdConnectRequest request)
        {
            Debug.Assert(request.IsTokenRequest(),
                "The OpenIddict binder for ASP.NET Core MVC is not registered. " +
                "Make sure services.AddOpenIddict().AddMvcBinders() is correctly called.");

            if (request.IsPasswordGrantType())
            {
                var user = await _userManager.FindByNameAsync(request.Username);
                if (user == null)
                {
                    return BadRequest(new OpenIdConnectResponse
                    {
                        Error = OpenIdConnectConstants.Errors.InvalidGrant,
                        ErrorDescription = "The username/password couple is invalid."
                    });
                }

                // Ensure the user is allowed to sign in.
                if (!await _signInManager.CanSignInAsync(user))
                {
                    return BadRequest(new OpenIdConnectResponse
                    {
                        Error = OpenIdConnectConstants.Errors.InvalidGrant,
                        ErrorDescription = "The specified user is not allowed to sign in."
                    });
                }

                // Reject the token request if two-factor authentication has been enabled by the user.
                if (_userManager.SupportsUserTwoFactor && await _userManager.GetTwoFactorEnabledAsync(user))
                {
                    return BadRequest(new OpenIdConnectResponse
                    {
                        Error = OpenIdConnectConstants.Errors.InvalidGrant,
                        ErrorDescription = "The specified user is not allowed to sign in."
                    });
                }

                // Ensure the user is not already locked out.
                if (_userManager.SupportsUserLockout && await _userManager.IsLockedOutAsync(user))
                {
                    return BadRequest(new OpenIdConnectResponse
                    {
                        Error = OpenIdConnectConstants.Errors.InvalidGrant,
                        ErrorDescription = "The username/password couple is invalid."
                    });
                }

                // Ensure the password is valid.
                if (!await _userManager.CheckPasswordAsync(user, request.Password))
                {
                    if (_userManager.SupportsUserLockout)
                    {
                        await _userManager.AccessFailedAsync(user);
                    }

                    return BadRequest(new OpenIdConnectResponse
                    {
                        Error = OpenIdConnectConstants.Errors.InvalidGrant,
                        ErrorDescription = "The username/password couple is invalid."
                    });
                }

                if (_userManager.SupportsUserLockout)
                {
                    await _userManager.ResetAccessFailedCountAsync(user);
                }

                // Create a new authentication ticket.
                var ticket = await CreateTicketAsync(request, user);

                return SignIn(ticket.Principal, ticket.Properties, ticket.AuthenticationScheme);
            }

            else if (request.IsAuthorizationCodeGrantType() || request.IsRefreshTokenGrantType())
            {
                // Retrieve the claims principal stored in the authorization code/refresh token.
                var info = await HttpContext.AuthenticateAsync(OpenIdConnectServerDefaults.AuthenticationScheme);

                // Retrieve the user profile corresponding to the authorization code/refresh token.
                // Note: if you want to automatically invalidate the authorization code/refresh token
                // when the user password/roles change, use the following line instead:
                // var user = _signInManager.ValidateSecurityStampAsync(info.Principal);
                var user = await _userManager.GetUserAsync(info.Principal);
                if (user == null)
                {
                    return BadRequest(new OpenIdConnectResponse
                    {
                        Error = OpenIdConnectConstants.Errors.InvalidGrant,
                        ErrorDescription = "The token is no longer valid."
                    });
                }

                // Ensure the user is still allowed to sign in.
                if (!await _signInManager.CanSignInAsync(user))
                {
                    return BadRequest(new OpenIdConnectResponse
                    {
                        Error = OpenIdConnectConstants.Errors.InvalidGrant,
                        ErrorDescription = "The user is no longer allowed to sign in."
                    });
                }

                // Create a new authentication ticket, but reuse the properties stored in the
                // authorization code/refresh token, including the scopes originally granted.
                var ticket = await CreateTicketAsync(request, user, info.Properties);

                return SignIn(ticket.Principal, ticket.Properties, ticket.AuthenticationScheme);
            }

            return BadRequest(new OpenIdConnectResponse
            {
                Error = OpenIdConnectConstants.Errors.UnsupportedGrantType,
                ErrorDescription = "The specified grant type is not supported."
            });
        }
        #endregion

        private async Task<AuthenticationTicket> CreateTicketAsync(
            OpenIdConnectRequest request, AppIdentityUser user,
            Microsoft.AspNetCore.Authentication.AuthenticationProperties properties = null)
        {
            // Create a new ClaimsPrincipal containing the claims that
            // will be used to create an id_token, a token or a code.
            var principal = await _signInManager.CreateUserPrincipalAsync(user);

            // Create a new authentication ticket holding the user identity.
            var ticket = new AuthenticationTicket(principal, properties,
                OpenIdConnectServerDefaults.AuthenticationScheme);

            if (!request.IsAuthorizationCodeGrantType() && !request.IsRefreshTokenGrantType())
            {
                // Set the list of scopes granted to the client application.
                // Note: the offline_access scope must be granted
                // to allow OpenIddict to return a refresh token.
                ticket.SetScopes(new[]
                {
                    OpenIdConnectConstants.Scopes.OpenId,
                    OpenIdConnectConstants.Scopes.Email,
                    OpenIdConnectConstants.Scopes.Profile,
                    OpenIdConnectConstants.Scopes.OfflineAccess,
                    OpenIddictConstants.Scopes.Roles
                }.Intersect(request.GetScopes()));
            }

            ticket.SetResources("resource_server");

            // Note: by default, claims are NOT automatically included in the access and identity tokens.
            // To allow OpenIddict to serialize them, you must attach them a destination, that specifies
            // whether they should be included in access tokens, in identity tokens or in both.

            foreach (var claim in ticket.Principal.Claims)
            {
                // Never include the security stamp in the access and identity tokens, as it's a secret value.
                if (claim.Type == _identityOptions.Value.ClaimsIdentity.SecurityStampClaimType)
                {
                    continue;
                }

                var destinations = new List<string>
                {
                    OpenIdConnectConstants.Destinations.AccessToken
                };

                // Only add the iterated claim to the id_token if the corresponding scope was granted to the client application.
                // The other claims will only be added to the access_token, which is encrypted when using the default format.
                if ((claim.Type == OpenIdConnectConstants.Claims.Name && ticket.HasScope(OpenIdConnectConstants.Scopes.Profile)) ||
                    (claim.Type == OpenIdConnectConstants.Claims.Email && ticket.HasScope(OpenIdConnectConstants.Scopes.Email)) ||
                    (claim.Type == OpenIdConnectConstants.Claims.Role && ticket.HasScope(OpenIddictConstants.Claims.Roles)))
                {
                    destinations.Add(OpenIdConnectConstants.Destinations.IdentityToken);
                }

                claim.SetDestinations(destinations);
            }

            return ticket;
        }
    }
}