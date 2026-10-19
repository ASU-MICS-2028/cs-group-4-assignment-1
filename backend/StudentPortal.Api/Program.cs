using Fido2NetLib;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StudentPortal.Api;
using StudentPortal.Api.Authorization;
using StudentPortal.Api.Data;
using StudentPortal.Api.Security;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<PortalOptions>(builder.Configuration.GetSection("Portal"));
var portal = builder.Configuration.GetSection("Portal").Get<PortalOptions>() ?? new PortalOptions();

builder.Services.AddDbContext<PortalDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Portal")));

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHttpContextAccessor();

// Relying party: the server only accepts WebAuthn responses for this RP ID and these origins.
builder.Services.AddSingleton(sp =>
{
    var options = sp.GetRequiredService<IOptions<PortalOptions>>().Value;
    return new Fido2(new Fido2Configuration
    {
        RPID = options.RelyingPartyId,
        RPName = options.RelyingPartyName,
        Origins = options.Origins.ToHashSet(),
    }, null!);
});

builder.Services.AddScoped<AuditLog>();
builder.Services.AddScoped<SessionService>();
builder.Services.AddScoped<CeremonyStore>();
builder.Services.AddScoped<EnrollmentCodeService>();

builder.Services
    .AddAuthentication(SessionAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, SessionAuthenticationHandler>(SessionAuthenticationHandler.SchemeName, null);

builder.Services.AddAuthorization(Policies.Configure);
builder.Services.AddScoped<IAuthorizationHandler, ReadStudentRecordHandler>();
builder.Services.AddScoped<IAuthorizationHandler, TeachCourseHandler>();
builder.Services.AddScoped<IAuthorizationHandler, UpdateGradeHandler>();

builder.Services.AddRateLimiter(options => RateLimits.Configure(options, portal.AuthRequestsPerMinute));
builder.Services.AddProblemDetails();
builder.Services.AddControllers();

var app = builder.Build();

if (PortalCommands.IsCommand(args))
    return await PortalCommands.RunAsync(app, args);

if (portal.ApplyMigrationsOnStartup)
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<PortalDbContext>().Database.MigrateAsync();
}

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler();

if (portal.RedirectToHttps)
{
    if (!app.Environment.IsDevelopment())
        app.UseHsts();
    app.UseHttpsRedirection();
}
app.UseSecurityHeaders();
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapFallback("/api/{**path}", () => Results.NotFound());
app.MapFallbackToFile("index.html").AllowAnonymous();

await app.RunAsync();
return 0;

public partial class Program;
