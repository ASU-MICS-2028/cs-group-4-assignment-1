using StudentPortal.Api.Data;

namespace StudentPortal.Api.Security;

public static class AuditOutcome
{
    public const string Success = "success";
    public const string Failure = "failure";
    public const string Denied = "denied";
}

// Accountability: who did what, to which record, and whether it was allowed.
// Never pass enrollment codes, tokens, keys or WebAuthn payloads in here.
public sealed class AuditLog(PortalDbContext db, IHttpContextAccessor http, TimeProvider clock)
{
    public async Task RecordAsync(string eventName, string outcome, string? actorId = null, string? target = null, string? detail = null)
    {
        db.AuditEvents.Add(new AuditEvent
        {
            OccurredAt = clock.GetUtcNow(),
            ActorId = actorId,
            Event = eventName,
            Outcome = outcome,
            Target = Truncate(target, 120),
            Detail = Truncate(detail, 300),
            IpAddress = http.HttpContext?.Connection.RemoteIpAddress?.ToString(),
        });
        await db.SaveChangesAsync();
    }

    private static string? Truncate(string? value, int length) =>
        value is null || value.Length <= length ? value : value[..length];
}
