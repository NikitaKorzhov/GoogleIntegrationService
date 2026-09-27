using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Mvc;

[Route("api/auth")]
[ApiController]
public class AuthController : ControllerBase
{
    /// <summary>
    /// Endpoint the user is redirected to in order to start signing in with Google.
    /// Example: GET /api/auth/google-login
    /// </summary>
    [HttpGet("google-login")]
    public IActionResult GoogleLogin()
    {
        var redirectUrl = Url.Action(nameof(GoogleCallback), "Auth");
        var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
        
        return Challenge(properties, GoogleDefaults.AuthenticationScheme);
    }

    /// <summary>
    /// Callback endpoint where Google returns tokens and user data.
    /// </summary>
    [HttpGet("google-callback")]
    public async Task<IActionResult> GoogleCallback()
    {
        var result = await HttpContext.AuthenticateAsync(GoogleDefaults.AuthenticationScheme);

        if (!result.Succeeded || result.Principal == null)
        {
            return BadRequest("Помилка автентифікації через Google.");
        }

        var email = result.Principal.FindFirstValue(ClaimTypes.Email);
        var name = result.Principal.FindFirstValue(ClaimTypes.Name);

        var accessToken = result.Properties.GetString("access_token");
        var refreshToken = result.Properties.GetString("refresh_token");

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);


        return Ok(new
        {
            Email = email,
            Name = name,
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            Message = "Успішна авторизація через Google!"
        });
    }
}