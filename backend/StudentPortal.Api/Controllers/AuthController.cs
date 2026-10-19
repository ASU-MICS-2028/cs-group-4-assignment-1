using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using StudentPortal.Api.Data;
using StudentPortal.Api.Security;

namespace StudentPortal.Api.Controllers;

public sealed record MeResponse(string AccountId, string DisplayName, string Role);

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    PortalDbContext db,
    Fido2 fido2,
    CeremonyStore ceremonies,
    SessionService sessions,
    AuditLog audit,
    TimeProvider clock) : ControllerBase
{
    // Fresh challenge for every sign-in attempt. The empty allow-list lets the browser
    // offer any passkey for this site, so the server does not reveal which accounts exist.
    [HttpPost("options")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimits.Authentication)]
    public async Task<IActionResult> Options()
    {
        var options = fido2.GetAssertionOptions(new GetAssertionOptionsParams
        {
            AllowedCredentials = [],
            UserVerification = UserVerificationRequirement.Required,
        });

        await ceremonies.StartAsync(Response, CeremonyKinds.Authentication, options.ToJson());
        return Content(options.ToJson(), "application/json");
    }

    // The library checks the challenge, origin, RP ID hash, user-presence and user-verification
    // flags, the signature against the stored public key, and the signature counter.
    // A session is created only after all of those pass.
    [HttpPost("verify")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimits.Authentication)]
    [Consumes("application/json")]
    public async Task<IActionResult> Verify(AuthenticatorAssertionRawResponse assertion, CancellationToken cancellationToken)
    {
        var ceremony = await ceremonies.ConsumeAsync(Request, Response, CeremonyKinds.Authentication);
        if (ceremony is null)
            return await Reject(null, "no active or unused challenge");

        var stored = await db.PasskeyCredentials
            .Include(c => c.Account)
            .SingleOrDefaultAsync(c => c.CredentialId == assertion.RawId, cancellationToken);
        if (stored?.Account is null || stored.RevokedAt is not null)
            return await Reject(null, "unknown or revoked credential");

        try
        {
            var result = await fido2.MakeAssertionAsync(new MakeAssertionParams
            {
                AssertionResponse = assertion,
                OriginalOptions = AssertionOptions.FromJson(ceremony.OptionsJson),
                StoredPublicKey = stored.PublicKey,
                StoredSignatureCounter = (uint)stored.SignCount,
                IsUserHandleOwnerOfCredentialIdCallback = (args, _) => Task.FromResult(
                    args.UserHandle.AsSpan().SequenceEqual(stored.Account.UserHandle)
                    && args.CredentialId.AsSpan().SequenceEqual(stored.CredentialId)),
            }, cancellationToken);

            stored.SignCount = result.SignCount;
            stored.IsBackedUp = result.IsBackedUp;
            stored.LastUsedAt = clock.GetUtcNow();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return await Reject(stored.AccountId, EnrollmentController.FailureReason(ex));
        }

        await db.SaveChangesAsync(cancellationToken);
        await sessions.CreateAsync(Response, stored.Account);
        await audit.RecordAsync("login.success", AuditOutcome.Success, stored.AccountId);

        return Ok(new MeResponse(stored.Account.Id, stored.Account.DisplayName, stored.Account.Role));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        if (Guid.TryParse(User.FindFirst(SessionAuthenticationHandler.SessionIdClaim)?.Value, out var sessionId))
            await sessions.RevokeAsync(sessionId, Response);

        await audit.RecordAsync("logout", AuditOutcome.Success, User.AccountId());
        return NoContent();
    }

    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var account = await db.Accounts.AsNoTracking().SingleAsync(a => a.Id == User.AccountId());
        return Ok(new MeResponse(account.Id, account.DisplayName, account.Role));
    }

    private async Task<IActionResult> Reject(string? accountId, string reason)
    {
        await audit.RecordAsync("login.failed", AuditOutcome.Failure, accountId, detail: reason);
        return Unauthorized(new { error = "Sign-in could not be verified." });
    }
}
