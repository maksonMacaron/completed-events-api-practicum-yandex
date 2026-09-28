using System.Security.Cryptography;
using System.Text;
using EventsAPI.Application.Abstractions.Authentication;

namespace EventsAPI.Infrastructure.Authentication;

public sealed class PasswordHasher : IPasswordHasher
{
    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(bytes);
    }

    public bool Verify(string password, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(passwordHash))
            return false;

        byte[] expectedHash;
        try
        {
            expectedHash = Convert.FromHexString(passwordHash);
        }
        catch (FormatException)
        {
            return false;
        }

        var actualHash = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }
}
