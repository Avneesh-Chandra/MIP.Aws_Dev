using System.Security.Cryptography;
using System.Text;

namespace MIP.Aws.Application.Common;

public static class TokenHasher
{
    public static string Hash(string rawToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToBase64String(bytes);
    }
}
