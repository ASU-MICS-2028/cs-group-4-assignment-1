using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace StudentPortal.Api.Security;

// Limits repeated sign-in and enrollment attempts from one address.
public static class RateLimits
{
    public const string Authentication = nameof(Authentication);

    public static void Configure(RateLimiterOptions options, int requestsPerMinute)
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.AddPolicy(Authentication, context => RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = requestsPerMinute,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
    }
}
