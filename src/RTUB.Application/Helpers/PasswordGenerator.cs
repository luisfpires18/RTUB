using System.Security.Cryptography;

namespace RTUB.Application.Helpers;

/// <summary>
/// Helper class for generating simple random passwords
/// </summary>
public static class PasswordGenerator
{
    /// <summary>
    /// Generates a simple random password suitable for older users
    /// - 4 characters (digits only for easier memorization)
    /// </summary>
    /// <returns>A simple random password</returns>
    public static string GeneratePassword()
    {
        const string digits = "0123456789";
        const int length = 4;

        var password = new char[length];

        for (int i = 0; i < length; i++)
        {
            password[i] = GetRandomChar(digits);
        }

        return new string(password);
    }

    private static char GetRandomChar(string chars)
    {
        int index = RandomNumberGenerator.GetInt32(chars.Length);
        return chars[index];
    }
}
