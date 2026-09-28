using System.Security.Claims;
using GoogleIntegrationService.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Mvc;

[Route("api/auth")]
[ApiController]
public class AuthController : ControllerBase
{
    private const string ReturnUrlKey = "returnUrl";

    private readonly IUserAccountService _userAccountService;
    private readonly TokenService _tokenService;
    private readonly IConfiguration _configuration;

    public AuthController(IUserAccountService userAccountService, TokenService tokenService, IConfiguration configuration)
    {
        _userAccountService = userAccountService;
        _tokenService = tokenService;
        _configuration = configuration;
    }

    /// <summary>
    /// Endpoint the user is redirected to in order to start signing in with Google.
    /// Example: GET /api/auth/google-login?returnUrl=http://localhost:4200/checkout
    /// </summary>
    [HttpGet("google-login")]
    public IActionResult GoogleLogin([FromQuery] string? returnUrl)
    {
        var callbackUrl = Url.Action(nameof(GoogleCallback), "Auth");
        var properties = new AuthenticationProperties { RedirectUri = callbackUrl };

        if (!string.IsNullOrWhiteSpace(returnUrl))
        {
            properties.Items[ReturnUrlKey] = returnUrl;
        }

        return Challenge(properties, GoogleDefaults.AuthenticationScheme);
    }

    /// <summary>
    /// Callback endpoint where Google returns tokens and user data. Issues the app's own
    /// JWT and redirects back to the caller-supplied returnUrl with the token attached.
    /// </summary>
    [HttpGet("google-callback")]
    public async Task<IActionResult> GoogleCallback()
    {
        var result = await HttpContext.AuthenticateAsync(GoogleDefaults.AuthenticationScheme);

        if (!result.Succeeded || result.Principal == null)
        {
            return BadRequest("Google authentication failed.");
        }

        var googleId = result.Principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = result.Principal.FindFirstValue(ClaimTypes.Email);

        if (string.IsNullOrWhiteSpace(googleId))
        {
            return BadRequest("Google account is missing an id.");
        }

        var accessToken = result.Properties.GetString("access_token");
        var refreshToken = result.Properties.GetString("refresh_token");

        // Google id is used as the username: look up the existing user or create a new one.
        await _userAccountService.UpsertGoogleUserAsync(googleId, email, accessToken, refreshToken);

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        var jwtToken = _tokenService.CreateToken(googleId);

        result.Properties.Items.TryGetValue(ReturnUrlKey, out var returnUrl);
        var target = IsAllowedReturnUrl(returnUrl) ? returnUrl! : DefaultReturnUrl;

        var separator = target.Contains('?') ? '&' : '?';
        return Redirect($"{target}{separator}token={Uri.EscapeDataString(jwtToken)}");
    }

    private string DefaultReturnUrl => _configuration["Frontend:DefaultReturnUrl"] ?? "/";

    /// <summary>
    /// Only allows redirecting to an absolute http/https URL whose host is explicitly
    /// listed in Frontend:AllowedReturnHosts — prevents an Open Redirect via a
    /// crafted returnUrl pointing at an attacker-controlled host.
    /// </summary>
    private bool IsAllowedReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
            return false;

        if (!Uri.TryCreate(returnUrl, UriKind.Absolute, out var uri))
            return false;

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return false;

        var allowedHosts = _configuration.GetSection("Frontend:AllowedReturnHosts").Get<string[]>()
            ?? Array.Empty<string>();

        return allowedHosts.Any(host => string.Equals(host, uri.Host, StringComparison.OrdinalIgnoreCase));
    }
}
