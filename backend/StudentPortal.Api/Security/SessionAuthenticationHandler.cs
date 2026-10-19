using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace StudentPortal.Api.Security;

public sealed class SessionAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    SessionService sessions,
    AuditLog audit)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "PortalSession";
    public const string SessionIdClaim = "sid";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Cookies.TryGetValue(SessionService.CookieName, out var token) || string.IsNullOrEmpty(token))
            return AuthenticateResult.NoResult();

        var session = await sessions.ValidateAsync(token);
        if (session?.Account is null)
            return AuthenticateResult.Fail("Session is not valid.");

        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, session.Account.Id),
            new Claim(ClaimTypes.Name, session.Account.DisplayName),
            new Claim(ClaimTypes.Role, session.Account.Role),
            new Claim(SessionIdClaim, session.Id.ToString()),
        ], SchemeName);

        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }

    // No valid session: 401.
    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        await audit.RecordAsync("access.denied", AuditOutcome.Denied, target: RequestLine(), detail: "401 no valid session");
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        await Response.WriteAsJsonAsync(new { error = "Authentication required." });
    }

    // Valid session, but the policy check failed: 403.
    protected override async Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        await audit.RecordAsync("access.denied", AuditOutcome.Denied, Context.User.AccountId(), RequestLine(), "403 policy check failed");
        Response.StatusCode = StatusCodes.Status403Forbidden;
        await Response.WriteAsJsonAsync(new { error = "You are not permitted to perform this action." });
    }

    private string RequestLine() => $"{Request.Method} {Request.Path}";
}

public static class ClaimsPrincipalExtensions
{
    public static string? AccountId(this ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.NameIdentifier);
}
