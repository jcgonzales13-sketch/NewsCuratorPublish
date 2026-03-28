using System.Security.Cryptography;

namespace AiNewsCurator.Application.Services;

public static class OpsLoginCodeGenerator
{
    public static string GenerateSixDigitCode()
    {
        return RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
    }
}
