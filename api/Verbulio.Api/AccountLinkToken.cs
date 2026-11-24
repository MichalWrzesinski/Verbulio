using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace Verbulio.Api;

internal static class AccountLinkToken
{
    internal const int EncodedLength = 43;
    internal static string Create() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
    internal static byte[] Digest(string token) => SHA256.HashData(Encoding.UTF8.GetBytes(token));
}
