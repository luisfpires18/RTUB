using System.Security.Cryptography;
using System.Text;

namespace RTUB.Application.Helpers;

/// <summary>
/// Helper class for generating secure random passwords
/// </summary>
public static class PasswordGenerator
{
    /// <summary>
    /// Generates a random password that meets ASP.NET Identity requirements
    /// - At least 10 characters
    /// - Contains uppercase letters
    /// - Contains lowercase letters
    /// - Contains digits
    /// - Contains special characters
    /// </summary>
    /// <returns>A secure random password</returns>
    public static string GeneratePassword()
    {
        const string uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        const string lowercase = "abcdefghijklmnopqrstuvwxyz";
        const string digits = "0123456789";
        const string specialChars = "!@#$%^&*";
        const int length = 10;

        var password = new StringBuilder();

        // Ensure at least one character from each required category
        password.Append(GetRandomChar(uppercase));
        password.Append(GetRandomChar(lowercase));
        password.Append(GetRandomChar(digits));
        password.Append(GetRandomChar(specialChars));

        // Fill the rest with random characters from all categories
        string allChars = uppercase + lowercase + digits + specialChars;
        for (int i = 4; i < length; i++)
        {
            password.Append(GetRandomChar(allChars));
        }

        // Shuffle the password to avoid predictable patterns
        return Shuffle(password.ToString());
    }

    private static char GetRandomChar(string chars)
    {
        int index = RandomNumberGenerator.GetInt32(chars.Length);
        return chars[index];
    }

    private static string Shuffle(string str)
    {
        char[] array = str.ToCharArray();
        int n = array.Length;
        
        for (int i = n - 1; i > 0; i--)
        {
            int j = RandomNumberGenerator.GetInt32(i + 1);
            // Swap
            (array[i], array[j]) = (array[j], array[i]);
        }
        
        return new string(array);
    }
}
