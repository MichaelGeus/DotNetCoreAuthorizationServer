using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

using DotNetCoreAuthorizationServer.Data;
using DotNetCoreAuthorizationServer.Models;

namespace DotNetCoreAuthorizationServer.Controllers
{
    [Produces("application/json")]
    [Authorize, Route("api/[controller]")]
    public class AccountController : Controller
    {
        private readonly UserManager<AppIdentityUser> _userManager;

        public AccountController(UserManager<AppIdentityUser> userManager)
        {
            _userManager = userManager;
        }

        //
        // POST: /Account/Register
        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> Register([FromBody] RegisterRequest model)
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.FindByNameAsync(model.Email);
                if (user != null)
                {
                    return StatusCode(StatusCodes.Status409Conflict);
                }

                user = new AppIdentityUser { UserName = model.Email, Email = model.Email };
                var result = await _userManager.CreateAsync(user, model.Password);
                if (result.Succeeded)
                    return Ok();

                AddErrors(result);
            }

            return BadRequest(ModelState);
        }

        #region Helpers

        private void AddErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }

        #endregion
    }
}