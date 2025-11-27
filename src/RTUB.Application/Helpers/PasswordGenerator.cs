using System.Security.Cryptography;
using System.Text;

namespace RTUB.Application.Helpers;

/// <summary>
/// Helper class for generating simple random passwords
/// </summary>
public static class PasswordGenerator
{
    /// <summary>
    /// Generates a simple random password suitable for older users
    /// - 4-6 random letters followed by 2-4 random numbers
    /// </summary>
    /// <returns>A simple random password</returns>
    public static string GeneratePassword()
    {
        const string letters = "abcdefghijklmnopqrstuvwxyz";
        const string digits = "0123456789";

        var password = new StringBuilder();

        // Generate 4-6 random letters
        int letterCount = RandomNumberGenerator.GetInt32(4, 7); // 4, 5, or 6
        for (int i = 0; i < letterCount; i++)
        {
            password.Append(GetRandomChar(letters));
        }

        // Generate 2-4 random numbers
        int digitCount = RandomNumberGenerator.GetInt32(2, 5); // 2, 3, or 4
        for (int i = 0; i < digitCount; i++)
        {
            password.Append(GetRandomChar(digits));
        }

        return password.ToString();
    }

    private static char GetRandomChar(string chars)
    {
        int index = RandomNumberGenerator.GetInt32(chars.Length);
        return chars[index];
    }
}
