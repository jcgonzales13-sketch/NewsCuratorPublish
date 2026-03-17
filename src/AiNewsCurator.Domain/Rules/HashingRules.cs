using System.Security.Cryptography;
using System.Text;

namespace AiNewsCurator.Domain.Rules;

public static class HashingRules
{
    public static string Sha256(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value.Trim().ToLowerInvariant()));
        return Convert.ToHexString(bytes);
    }
}
