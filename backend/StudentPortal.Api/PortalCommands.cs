using Microsoft.EntityFrameworkCore;
using StudentPortal.Api.Data;
using StudentPortal.Api.Security;

namespace StudentPortal.Api;

// Command-line tasks for the IT help desk, run outside the web app:
//   dotnet run -- migrate
//   dotnet run -- issue-enrollment-code S001
public static class PortalCommands
{
    public static bool IsCommand(string[] args) =>
        args.Length > 0 && args[0] is "migrate" or "issue-enrollment-code";

    public static async Task<int> RunAsync(WebApplication app, string[] args)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PortalDbContext>();

        if (args[0] == "migrate")
        {
            await db.Database.MigrateAsync();
            Console.WriteLine("Database is up to date.");
            return 0;
        }

        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: issue-enrollment-code <AccountId>");
            return 1;
        }

        var accountId = args[1].Trim().ToUpperInvariant();
        var account = await db.Accounts.FindAsync(accountId);
        if (account is null)
        {
            Console.Error.WriteLine($"No account with ID {accountId}.");
            return 1;
        }

        var codes = scope.ServiceProvider.GetRequiredService<EnrollmentCodeService>();
        var audit = scope.ServiceProvider.GetRequiredService<AuditLog>();
        var (code, expiresAt) = await codes.IssueAsync(account.Id);
        await audit.RecordAsync("enrollment.code_issued", AuditOutcome.Success, "it-helpdesk", account.Id);

        // Printed once to the operator's console only. It is not written to any log or table.
        Console.WriteLine($"Enrollment code for {account.Id} ({account.DisplayName}): {code}");
        Console.WriteLine($"Single use. Expires {expiresAt:yyyy-MM-dd HH:mm} UTC.");
        return 0;
    }
}
