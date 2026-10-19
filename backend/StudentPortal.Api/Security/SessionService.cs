using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StudentPortal.Api.Data;

namespace StudentPortal.Api.Security;

// Server-side sessions. The cookie holds a random token; the sessions table decides
// whether it is still valid, so logout and timeouts take effect immediately.
public sealed class SessionService(PortalDbContext db, TimeProvider clock, IOptions<PortalOptions> options)
{
    public const string CookieName = "__Host-portal-session";

    public async Task CreateAsync(HttpResponse response, Account account)
    {
        var token = Tokens.NewToken();
        var now = clock.GetUtcNow();
        var expiresAt = now.AddHours(options.Value.SessionAbsoluteHours);

        db.Sessions.Add(new Session
        {
            TokenHash = Tokens.Hash(token),
            AccountId = account.Id,
            CreatedAt = now,
            LastSeenAt = now,
            ExpiresAt = expiresAt,
        });
        await db.SaveChangesAsync();

        response.Cookies.Append(CookieName, token, Tokens.CookieOptions(expiresAt));
    }

    public async Task<Session?> ValidateAsync(string token)
    {
        var hash = Tokens.Hash(token);
        var now = clock.GetUtcNow();
        var session = await db.Sessions.Include(s => s.Account).SingleOrDefaultAsync(s => s.TokenHash == hash);

        if (session is null || session.RevokedAt is not null || session.ExpiresAt <= now)
            return null;

        if (now - session.LastSeenAt > TimeSpan.FromMinutes(options.Value.SessionIdleMinutes))
        {
            session.RevokedAt = now;
            await db.SaveChangesAsync();
            return null;
        }

        if (now - session.LastSeenAt > TimeSpan.FromMinutes(1))
        {
            session.LastSeenAt = now;
            await db.SaveChangesAsync();
        }

        return session;
    }

    public async Task RevokeAsync(Guid sessionId, HttpResponse response)
    {
        var now = clock.GetUtcNow();
        await db.Sessions
            .Where(s => s.Id == sessionId && s.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, (DateTimeOffset?)now));

        response.Cookies.Delete(CookieName, Tokens.CookieOptions());
    }
}
