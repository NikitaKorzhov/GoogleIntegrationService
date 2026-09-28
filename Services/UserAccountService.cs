using GoogleIntegrationService.Domain;
using GoogleIntegrationService.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GoogleIntegrationService.Services;

public interface IUserAccountService
{
    /// <summary>
    /// Looks up an AppUser by Google id (stored as UserName) and either updates it
    /// with the latest Google login data or creates a new one.
    /// </summary>
    Task<AppUser> UpsertGoogleUserAsync(
        string googleId,
        string? email,
        string? googleToken,
        string? googleRefreshToken,
        CancellationToken cancellationToken = default);
}

public class UserAccountService : IUserAccountService
{
    private readonly AppDbContext _db;

    public UserAccountService(AppDbContext db) => _db = db;

    public async Task<AppUser> UpsertGoogleUserAsync(
        string googleId,
        string? email,
        string? googleToken,
        string? googleRefreshToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(googleId))
            throw new ArgumentException("Google id is required.", nameof(googleId));

        var user = await _db.AppUsers
            .FirstOrDefaultAsync(u => u.UserName == googleId, cancellationToken);
        var isNew = user is null;

        if (user is null)
        {
            user = new AppUser
            {
                Id = Guid.NewGuid(),
                UserName = googleId
            };
            _db.AppUsers.Add(user);
        }

        user.Email = email;
        user.GoogleToken = googleToken ?? string.Empty;
        user.GoogleRefreshToken = googleRefreshToken;

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (isNew && IsUniqueConstraintViolation(ex, nameof(AppUser.UserName)))
        {
            // Another concurrent login for the same Google account inserted its row first.
            // Detach our failed insert and update the row that now exists instead.
            _db.Entry(user).State = EntityState.Detached;

            var existing = await _db.AppUsers.FirstOrDefaultAsync(u => u.UserName == googleId, cancellationToken)
                ?? throw new InvalidOperationException(
                    $"Expected a concurrently-inserted AppUser for Google id '{googleId}' but found none.");

            existing.Email = email;
            existing.GoogleToken = googleToken ?? string.Empty;
            existing.GoogleRefreshToken = googleRefreshToken;
            await _db.SaveChangesAsync(cancellationToken);
            return existing;
        }

        return user;
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex, string columnName) =>
        ex.InnerException is SqliteException { SqliteErrorCode: 19 } sqliteEx
        && sqliteEx.Message.Contains(columnName, StringComparison.OrdinalIgnoreCase);
}
