using System.Security.Cryptography;
using System.Text;

namespace AiNewsCurator.Application.Services;

public static class OpsAuthCodeHasher
{
    public static string Hash(string code)
    {
        var bytes = Encoding.UTF8.GetBytes(code ?? string.Empty);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
