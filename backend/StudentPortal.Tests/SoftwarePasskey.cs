using System.Buffers.Text;
using System.Formats.Cbor;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace StudentPortal.Tests;

// A minimal software authenticator for automated tests. It produces the same
// structures a browser passkey would (attestationObject, authenticatorData,
// clientDataJSON, ES256 signature) using .NET's built-in ECDSA, so the real
// server-side verification code runs unchanged.
public sealed class SoftwarePasskey(string rpId, string origin) : IDisposable
{
    private const byte UserPresent = 0x01;
    private const byte UserVerified = 0x04;
    private const byte AttestedCredentialData = 0x40;

    private readonly ECDsa _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    public byte[] CredentialId { get; } = RandomNumberGenerator.GetBytes(32);
    public byte[] UserHandle { get; private set; } = [];
    private uint _signCount;

    public JsonObject Register(string optionsJson)
    {
        using var options = JsonDocument.Parse(optionsJson);
        var challenge = options.RootElement.GetProperty("challenge").GetString()!;
        UserHandle = Base64Url.DecodeFromChars(options.RootElement.GetProperty("user").GetProperty("id").GetString());

        var clientData = ClientData("webauthn.create", challenge, origin);
        var authData = AuthenticatorData(UserPresent | UserVerified | AttestedCredentialData, includeCredential: true);

        var cbor = new CborWriter();
        cbor.WriteStartMap(3);
        cbor.WriteTextString("fmt");
        cbor.WriteTextString("none");
        cbor.WriteTextString("attStmt");
        cbor.WriteStartMap(0);
        cbor.WriteEndMap();
        cbor.WriteTextString("authData");
        cbor.WriteByteString(authData);
        cbor.WriteEndMap();

        return new JsonObject
        {
            ["id"] = Base64Url.EncodeToString(CredentialId),
            ["rawId"] = Base64Url.EncodeToString(CredentialId),
            ["type"] = "public-key",
            ["response"] = new JsonObject
            {
                ["attestationObject"] = Base64Url.EncodeToString(cbor.Encode()),
                ["clientDataJSON"] = Base64Url.EncodeToString(clientData),
                ["transports"] = new JsonArray("internal"),
            },
            ["clientExtensionResults"] = new JsonObject(),
        };
    }

    public JsonObject SignIn(string optionsJson, string? originOverride = null)
    {
        using var options = JsonDocument.Parse(optionsJson);
        var challenge = options.RootElement.GetProperty("challenge").GetString()!;

        _signCount++;
        var clientData = ClientData("webauthn.get", challenge, originOverride ?? origin);
        var authData = AuthenticatorData(UserPresent | UserVerified, includeCredential: false);
        var signature = _key.SignData([.. authData, .. SHA256.HashData(clientData)], HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);

        return new JsonObject
        {
            ["id"] = Base64Url.EncodeToString(CredentialId),
            ["rawId"] = Base64Url.EncodeToString(CredentialId),
            ["type"] = "public-key",
            ["response"] = new JsonObject
            {
                ["authenticatorData"] = Base64Url.EncodeToString(authData),
                ["clientDataJSON"] = Base64Url.EncodeToString(clientData),
                ["signature"] = Base64Url.EncodeToString(signature),
                ["userHandle"] = Base64Url.EncodeToString(UserHandle),
            },
            ["clientExtensionResults"] = new JsonObject(),
        };
    }

    private static byte[] ClientData(string type, string challenge, string clientOrigin) =>
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { type, challenge, origin = clientOrigin, crossOrigin = false }));

    private byte[] AuthenticatorData(int flags, bool includeCredential)
    {
        var data = new List<byte>();
        data.AddRange(SHA256.HashData(Encoding.UTF8.GetBytes(rpId)));
        data.Add((byte)flags);
        data.AddRange([(byte)(_signCount >> 24), (byte)(_signCount >> 16), (byte)(_signCount >> 8), (byte)_signCount]);

        if (includeCredential)
        {
            data.AddRange(new byte[16]);
            data.AddRange([(byte)(CredentialId.Length >> 8), (byte)CredentialId.Length]);
            data.AddRange(CredentialId);
            data.AddRange(CosePublicKey());
        }

        return [.. data];
    }

    private byte[] CosePublicKey()
    {
        var point = _key.ExportParameters(false).Q;
        var cbor = new CborWriter();
        cbor.WriteStartMap(5);
        cbor.WriteInt32(1); cbor.WriteInt32(2);    // kty: EC2
        cbor.WriteInt32(3); cbor.WriteInt32(-7);   // alg: ES256
        cbor.WriteInt32(-1); cbor.WriteInt32(1);   // crv: P-256
        cbor.WriteInt32(-2); cbor.WriteByteString(point.X!);
        cbor.WriteInt32(-3); cbor.WriteByteString(point.Y!);
        cbor.WriteEndMap();
        return cbor.Encode();
    }

    public void Dispose() => _key.Dispose();
}
