using System.Security.Cryptography;
using System.Text;

namespace AiNewsCurator.Api.Operations;

public static class OperationsAuthCookie
{
    public const string CookieName = "AiNewsOpsAuth";

    public static string CreateValue(string apiKey)
    {
        var keyBytes = SHA256.HashData(Encoding.UTF8.GetBytes(apiKey));
        return Convert.ToHexString(keyBytes);
    }

    public static bool Matches(string? cookieValue, string apiKey)
    {
        if (string.IsNullOrWhiteSpace(cookieValue))
        {
            return false;
        }

        var expected = Encoding.UTF8.GetBytes(CreateValue(apiKey));
        var actual = Encoding.UTF8.GetBytes(cookieValue.Trim());

        return actual.Length == expected.Length &&
               CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
