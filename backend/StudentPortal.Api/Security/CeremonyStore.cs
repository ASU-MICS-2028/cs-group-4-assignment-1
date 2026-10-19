using Microsoft.EntityFrameworkCore;
using StudentPortal.Api.Data;

namespace StudentPortal.Api.Security;

// Keeps the WebAuthn options (and so the challenge) on the server between the
// "options" and "verify" requests. Each ceremony can be consumed once, within five minutes.
public sealed class CeremonyStore(PortalDbContext db, TimeProvider clock)
{
    public const string CookieName = "__Host-portal-ceremony";
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    public async Task StartAsync(HttpResponse response, string kind, string optionsJson, string? accountId = null, Guid? enrollmentCodeId = null)
    {
        var token = Tokens.NewToken();
        var now = clock.GetUtcNow();

        db.WebAuthnCeremonies.Add(new WebAuthnCeremony
        {
            TokenHash = Tokens.Hash(token),
            Kind = kind,
            AccountId = accountId,
            EnrollmentCodeId = enrollmentCodeId,
            OptionsJson = optionsJson,
            CreatedAt = now,
            ExpiresAt = now.Add(Lifetime),
        });
        await db.SaveChangesAsync();

        response.Cookies.Append(CookieName, token, Tokens.CookieOptions(now.Add(Lifetime)));
    }

    public async Task<WebAuthnCeremony?> ConsumeAsync(HttpRequest request, HttpResponse response, string kind)
    {
        response.Cookies.Delete(CookieName, Tokens.CookieOptions());

        if (!request.Cookies.TryGetValue(CookieName, out var token) || string.IsNullOrEmpty(token))
            return null;

        var hash = Tokens.Hash(token);
        var now = clock.GetUtcNow();

        // Marking it consumed in the same statement that checks it means a replayed
        // response can never be verified against the same challenge twice.
        var claimed = await db.WebAuthnCeremonies
            .Where(c => c.TokenHash == hash && c.Kind == kind && c.ConsumedAt == null && c.ExpiresAt > now)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.ConsumedAt, (DateTimeOffset?)now));

        if (claimed != 1)
            return null;

        return await db.WebAuthnCeremonies.AsNoTracking().SingleAsync(c => c.TokenHash == hash);
    }
}
