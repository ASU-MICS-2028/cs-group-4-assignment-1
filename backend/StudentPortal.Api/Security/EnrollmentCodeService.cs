using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StudentPortal.Api.Data;

namespace StudentPortal.Api.Security;

// The initial enrollment step. IT issues a one-time code through a separate channel
// (here, the issue-enrollment-code command). The student proves they hold it before
// the server will start passkey registration for that account.
public sealed class EnrollmentCodeService(PortalDbContext db, TimeProvider clock, IOptions<PortalOptions> options)
{
    private const string Alphabet = "ABCDEFGHJKMNPQRSTVWXYZ23456789";
    private const int CodeLength = 12;
    private const int Iterations = 210_000;

    public async Task<(string Code, DateTimeOffset ExpiresAt)> IssueAsync(string accountId)
    {
        var now = clock.GetUtcNow();

        // Issuing a new code cancels any earlier unused code for the account.
        await db.EnrollmentCodes
            .Where(c => c.AccountId == accountId && c.UsedAt == null && c.ExpiresAt > now)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.ExpiresAt, now));

        var code = new string(RandomNumberGenerator.GetItems<char>(Alphabet, CodeLength));
        var salt = RandomNumberGenerator.GetBytes(16);
        var expiresAt = now.AddHours(options.Value.EnrollmentCodeValidHours);

        db.EnrollmentCodes.Add(new EnrollmentCode
        {
            AccountId = accountId,
            Salt = salt,
            CodeHash = HashCode(code, salt),
            IssuedAt = now,
            ExpiresAt = expiresAt,
        });
        await db.SaveChangesAsync();

        return ($"{code[..4]}-{code[4..8]}-{code[8..]}", expiresAt);
    }

    public async Task<EnrollmentCode?> VerifyAsync(string accountId, string submittedCode)
    {
        var now = clock.GetUtcNow();
        var record = await db.EnrollmentCodes
            .Where(c => c.AccountId == accountId && c.UsedAt == null && c.ExpiresAt > now)
            .OrderByDescending(c => c.IssuedAt)
            .FirstOrDefaultAsync();

        if (record is null || record.FailedAttempts >= options.Value.EnrollmentCodeMaxAttempts)
        {
            HashCode(Normalise(submittedCode), RandomNumberGenerator.GetBytes(16));
            return null;
        }

        var candidate = HashCode(Normalise(submittedCode), record.Salt);
        if (!CryptographicOperations.FixedTimeEquals(candidate, record.CodeHash))
        {
            record.FailedAttempts++;
            await db.SaveChangesAsync();
            return null;
        }

        return record;
    }

    // Same work for unknown accounts, so response time does not reveal which IDs exist.
    public void RejectUnknownAccount(string submittedCode) =>
        HashCode(Normalise(submittedCode), RandomNumberGenerator.GetBytes(16));

    private static string Normalise(string code) =>
        new(code.ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());

    private static byte[] HashCode(string code, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(code, salt, Iterations, HashAlgorithmName.SHA256, 32);
}
