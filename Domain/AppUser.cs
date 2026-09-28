namespace GoogleIntegrationService.Domain;

public class AppUser
{
    public Guid Id { get; set; }
    public string? Email { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string GoogleToken { get; set; } = string.Empty;
    public string? GoogleRefreshToken { get; set; }
}
