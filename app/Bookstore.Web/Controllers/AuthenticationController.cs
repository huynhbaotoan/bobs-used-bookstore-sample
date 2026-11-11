using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using System.Threading.Tasks;

namespace Bookstore.Web.Controllers
{
    public class AuthenticationController : Controller
    {
        private IWebHostEnvironment webHostEnvironment;

        public AuthenticationController(IWebHostEnvironment webHostEnvironment)
        {
            this.webHostEnvironment = webHostEnvironment;
        }

        [AllowAnonymous]
        public IActionResult Login(string redirectUri = null)
        {
            return Challenge(new AuthenticationProperties
            {
                // Referer is absent on direct navigation; fall back to the site root rather than NRE.
                RedirectUri = redirectUri ?? Request.GetTypedHeaders().Referer?.ToString() ?? Url.Action("Index", "Home")
            });
        }

        public async Task<IActionResult> LogOut()
        {
            return webHostEnvironment.IsDevelopment() ? await LocalSignOutAsync() : CognitoSignOut();
        }

        private async Task<IActionResult> LocalSignOutAsync()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            return RedirectToAction("Index", "Home");
        }

        private IActionResult CognitoSignOut()
        {
            return SignOut(CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme);
        }
    }
}
