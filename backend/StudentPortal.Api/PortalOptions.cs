namespace StudentPortal.Api;

public sealed class PortalOptions
{
    public string RelyingPartyId { get; set; } = "localhost";
    public string RelyingPartyName { get; set; } = "Ashesi Student Records Portal";
    public string[] Origins { get; set; } = [];
    public int SessionIdleMinutes { get; set; } = 15;
    public int SessionAbsoluteHours { get; set; } = 8;
    public int EnrollmentCodeValidHours { get; set; } = 24;
    public int EnrollmentCodeMaxAttempts { get; set; } = 5;
    public int AuthRequestsPerMinute { get; set; } = 20;
    public bool ApplyMigrationsOnStartup { get; set; }

    // Off only for the Docker run, which serves http://localhost (a secure context for WebAuthn).
    public bool RedirectToHttps { get; set; } = true;
}
