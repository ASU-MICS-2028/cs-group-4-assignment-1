using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StudentPortal.Api.Data;
using StudentPortal.Api.Security;

namespace StudentPortal.Tests;

// Starts the real API against the student_portal_test database, rebuilt from the
// migrations (schema + seed data) at the start of the run.
public sealed class PortalFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string RpId = "localhost";
    public const string Origin = "https://localhost";

    // Defaults to the Docker database from compose.yaml ("Database: start" task).
    private static readonly string ConnectionString =
        new ConfigurationBuilder()
            .AddUserSecrets(typeof(Program).Assembly, optional: true)
            .AddEnvironmentVariables()
            .Build()
            .GetConnectionString("PortalTest")
        ?? "Host=localhost;Port=5433;Database=student_portal_test;Username=portal_app;Password=portal-app-local-only;GSS Encryption Mode=Disable";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Portal", ConnectionString);
        builder.UseSetting("Portal:Origins:0", Origin);
        builder.UseSetting("Portal:AuthRequestsPerMinute", "1000");
        builder.UseSetting("Portal:ApplyMigrationsOnStartup", "false");
    }

    public async Task InitializeAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PortalDbContext>();
        await db.Database.ExecuteSqlRawAsync("DROP SCHEMA IF EXISTS public CASCADE; CREATE SCHEMA public;");
        await db.Database.MigrateAsync();
    }

    Task IAsyncLifetime.DisposeAsync() => Task.CompletedTask;

    public HttpClient NewBrowser() => CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri(Origin),
        HandleCookies = true,
        AllowAutoRedirect = false,
    });

    public async Task<T> WithDbAsync<T>(Func<PortalDbContext, Task<T>> action)
    {
        await using var scope = Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<PortalDbContext>());
    }

    public async Task<string> IssueEnrollmentCodeAsync(string accountId)
    {
        await using var scope = Services.CreateAsyncScope();
        var (code, _) = await scope.ServiceProvider.GetRequiredService<EnrollmentCodeService>().IssueAsync(accountId);
        return code;
    }

    // Full enrollment through the HTTP API: code check, then passkey registration.
    public async Task<(SoftwarePasskey Passkey, HttpResponseMessage Response)> EnrollAsync(HttpClient browser, string accountId)
    {
        var code = await IssueEnrollmentCodeAsync(accountId);
        var begin = await browser.PostAsJsonAsync("/api/enrollment/begin", new { accountId, code });
        begin.EnsureSuccessStatusCode();

        var passkey = new SoftwarePasskey(RpId, Origin);
        var attestation = passkey.Register(await begin.Content.ReadAsStringAsync());
        var complete = await browser.PostAsJsonAsync("/api/enrollment/complete", attestation);
        return (passkey, complete);
    }

    public static async Task<HttpResponseMessage> SignInAsync(HttpClient browser, SoftwarePasskey passkey)
    {
        var options = await browser.PostAsync("/api/auth/options", null);
        options.EnsureSuccessStatusCode();
        var assertion = passkey.SignIn(await options.Content.ReadAsStringAsync());
        return await browser.PostAsJsonAsync("/api/auth/verify", assertion);
    }

    // A signed-in browser for the given account.
    public async Task<HttpClient> SignedInAsync(string accountId)
    {
        var browser = NewBrowser();
        var (passkey, enrolled) = await EnrollAsync(browser, accountId);
        Assert.Equal(HttpStatusCode.Created, enrolled.StatusCode);
        var signIn = await SignInAsync(browser, passkey);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
        return browser;
    }
}

[CollectionDefinition(Name)]
public sealed class PortalCollection : ICollectionFixture<PortalFixture>
{
    public const string Name = "portal";
}
