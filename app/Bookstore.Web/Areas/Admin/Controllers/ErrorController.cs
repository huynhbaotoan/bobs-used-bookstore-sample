using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Bookstore.Web.Areas.Admin.Controllers
{
    [AllowAnonymous]
    public class ErrorController : AdminAreaControllerBase
    {
        [Route("/Error/Index/{code:int}")]
        public IActionResult Index(int code)
        {
            var exception = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
            ViewData["Path"] = exception?.Path;
            ViewData["StatusCode"] = code;
            return View();
        }

        [Route("/error")]
        public IActionResult Support()
        {
            var exception = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
            ViewData["Path"] = exception?.Path;
            var error = Problem();

            ViewData["StatusCode"] = error.StatusCode;

            // ~/Views/Error/Index.cshtml does not exist (there is no non-area Error controller),
            // so the explicit path threw a view-not-found 500. The error page for this action
            // lives in the Admin area alongside the controller.
            return View("~/Areas/Admin/Views/Error/Index.cshtml");
        }
    }
}