using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace StudentPortal.Api.Security;

public static class Tokens
{
    // 256-bit random value sent to the browser in a cookie.
    public static string NewToken() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    // Only the hash is stored, so a copy of the database does not contain usable session or ceremony tokens.
    public static byte[] Hash(string token) => SHA256.HashData(Encoding.UTF8.GetBytes(token));

    public static CookieOptions CookieOptions(DateTimeOffset? expires = null) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = "/",
        IsEssential = true,
        Expires = expires,
    };
}
