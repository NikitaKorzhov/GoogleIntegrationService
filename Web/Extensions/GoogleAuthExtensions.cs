using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;

namespace GoogleIntegrationService.Web.Extensions;

public static class GoogleAuthExtensions
{
    public static IServiceCollection AddGoogleAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var googleSection = configuration.GetSection("Authentication:Google");

        services.AddAuthentication()
            .AddCookie() // Needed for the temporary session during the OAuth redirect
            .AddGoogle(options =>
            {
                // The project's DefaultSignInScheme belongs to JWT (see AddJwtAuthentication),
                // so we explicitly set the cookie scheme for the Google handler.
                options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;

                options.ClientId = googleSection["ClientId"] ?? throw new InvalidOperationException("Google ClientId is missing");
                options.ClientSecret = googleSection["ClientSecret"] ?? throw new InvalidOperationException("Google ClientSecret is missing");

                // Add the scope needed if you require access to the YouTube API:
                options.Scope.Add("https://www.googleapis.com/auth/youtube.readonly");

                // Request offline access to obtain a refresh token
                options.Events.OnRedirectToAuthorizationEndpoint = context =>
                {
                    context.Response.Redirect(context.RedirectUri + "&access_type=offline&prompt=consent");
                    return Task.CompletedTask;
                };

                // Store the access_token and refresh_token in the context properties
                options.Events.OnCreatingTicket = context =>
                {
                    context.Properties.SetString("access_token", context.AccessToken);
                    context.Properties.SetString("refresh_token", context.RefreshToken);
                    return Task.CompletedTask;
                };
            });

        return services;
    }
}