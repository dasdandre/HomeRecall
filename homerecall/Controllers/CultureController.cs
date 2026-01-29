using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace HomeRecall.Controllers;

[Route("api/[controller]")]
[ApiController]
public class CultureController : ControllerBase
{
    [HttpGet("Set")]
    public IActionResult SetCulture(string culture, string redirectUri)
    {
        if (culture != null)
        {
            if (culture == "auto")
            {
                 Response.Cookies.Delete(CookieRequestCultureProvider.DefaultCookieName);
            }
            else
            {
                HttpContext.Response.Cookies.Append(
                    CookieRequestCultureProvider.DefaultCookieName,
                    CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
                    new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1) }
                );
            }
        }

        return LocalRedirect(redirectUri);
    }
}