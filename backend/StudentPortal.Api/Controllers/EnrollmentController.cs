using System.ComponentModel.DataAnnotations;
using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using StudentPortal.Api.Data;
using StudentPortal.Api.Security;

namespace StudentPortal.Api.Controllers;

public sealed record BeginEnrollmentRequest(
    [Required, StringLength(10)] string AccountId,
    [Required, StringLength(32)] string Code);

[ApiController]
[Route("api/enrollment")]
[AllowAnonymous]
[EnableRateLimiting(RateLimits.Authentication)]
public sealed class EnrollmentController(
    PortalDbContext db,
    Fido2 fido2,
    EnrollmentCodeService codes,
    CeremonyStore ceremonies,
    AuditLog audit,
    TimeProvider clock) : ControllerBase
{
    // Step 1: identify the account and prove possession of the one-time enrollment code.
    // Only then does the server issue a registration challenge for that account.
    [HttpPost("begin")]
    [Consumes("application/json")]
    public async Task<IActionResult> Begin(BeginEnrollmentRequest request)
    {
        var accountId = request.AccountId.Trim().ToUpperInvariant();
        var account = await db.Accounts.FindAsync(accountId);

        EnrollmentCode? code = null;
        if (account is null)
            codes.RejectUnknownAccount(request.Code);
        else
            code = await codes.VerifyAsync(account.Id, request.Code);

        if (account is null || code is null)
        {
            await audit.RecordAsync("enrollment.code_rejected", AuditOutcome.Failure, target: accountId);
            return BadRequest(new { error = "The account ID or enrollment code is not valid, or the code has expired." });
        }

        var existing = await db.PasskeyCredentials
            .Where(c => c.AccountId == account.Id && c.RevokedAt == null)
            .Select(c => c.CredentialId)
            .ToListAsync();

        var options = fido2.RequestNewCredential(new RequestNewCredentialParams
        {
            User = new Fido2User { Id = account.UserHandle, Name = account.Id, DisplayName = account.DisplayName },
            ExcludeCredentials = existing.Select(id => new PublicKeyCredentialDescriptor(id)).ToList(),
            AuthenticatorSelection = new AuthenticatorSelection
            {
                ResidentKey = ResidentKeyRequirement.Required,
                UserVerification = UserVerificationRequirement.Required,
            },
            AttestationPreference = AttestationConveyancePreference.None,
        });

        await ceremonies.StartAsync(Response, CeremonyKinds.Registration, options.ToJson(), account.Id, code.Id);
        await audit.RecordAsync("enrollment.code_accepted", AuditOutcome.Success, account.Id);

        return Content(options.ToJson(), "application/json");
    }

    // Step 2: verify the authenticator's response and bind the new public key to the account.
    [HttpPost("complete")]
    [Consumes("application/json")]
    public async Task<IActionResult> Complete(AuthenticatorAttestationRawResponse attestation, CancellationToken cancellationToken)
    {
        var ceremony = await ceremonies.ConsumeAsync(Request, Response, CeremonyKinds.Registration);
        if (ceremony?.AccountId is null || ceremony.EnrollmentCodeId is null)
        {
            await audit.RecordAsync("passkey.registration_failed", AuditOutcome.Failure, detail: "no active registration challenge");
            return BadRequest(new { error = "Registration has expired or was already used. Start again." });
        }

        RegisteredPublicKeyCredential credential;
        try
        {
            credential = await fido2.MakeNewCredentialAsync(new MakeNewCredentialParams
            {
                AttestationResponse = attestation,
                OriginalOptions = CredentialCreateOptions.FromJson(ceremony.OptionsJson),
                IsCredentialIdUniqueToUserCallback = async (args, ct) =>
                    !await db.PasskeyCredentials.AnyAsync(c => c.CredentialId == args.CredentialId, ct),
            }, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await audit.RecordAsync("passkey.registration_failed", AuditOutcome.Failure, ceremony.AccountId, detail: FailureReason(ex));
            return BadRequest(new { error = "The passkey could not be verified." });
        }

        var now = clock.GetUtcNow();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // The enrollment code is single use: only one registration can claim it.
        var claimed = await db.EnrollmentCodes
            .Where(c => c.Id == ceremony.EnrollmentCodeId && c.UsedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.UsedAt, (DateTimeOffset?)now), cancellationToken);
        if (claimed != 1)
            return BadRequest(new { error = "This enrollment code has already been used." });

        db.PasskeyCredentials.Add(new PasskeyCredential
        {
            AccountId = ceremony.AccountId,
            CredentialId = credential.Id,
            PublicKey = credential.PublicKey,
            SignCount = credential.SignCount,
            AaGuid = credential.AaGuid,
            Transports = credential.Transports?.Select(t => t.ToString().ToLowerInvariant()).ToArray() ?? [],
            AttestationFormat = credential.AttestationFormat,
            IsBackupEligible = credential.IsBackupEligible,
            IsBackedUp = credential.IsBackedUp,
            CreatedAt = now,
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await audit.RecordAsync("passkey.registered", AuditOutcome.Success, ceremony.AccountId);

        return StatusCode(StatusCodes.Status201Created, new { accountId = ceremony.AccountId, message = "Passkey registered. You can now sign in." });
    }

    internal static string FailureReason(Exception ex) =>
        ex is Fido2VerificationException f ? $"{f.Code}" : ex.GetType().Name;
}
