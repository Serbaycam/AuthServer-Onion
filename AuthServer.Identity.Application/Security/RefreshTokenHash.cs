using System.Security.Cryptography;
using System.Text;

namespace AuthServer.Identity.Application.Security;

public static class RefreshTokenHash
{
    public static string Compute(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
