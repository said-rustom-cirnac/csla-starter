using System.Security.Cryptography;

namespace CslaStarter.Infrastructure.Web;

/// <summary>
/// The Content-Security-Policy nonce of the current request; inline scripts must carry it. 128 random bits as
/// hex: valid in a CSP nonce and never altered by HTML attribute encoding (base64's '+' would be).
/// </summary>
public sealed class CspNonce
{
    public string Value { get; } = RandomNumberGenerator.GetHexString(32, lowercase: true);
}
