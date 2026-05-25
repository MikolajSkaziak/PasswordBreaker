namespace PasswordBreaker.Shared.Utils;

public static class BruteForceGenerator
{
    /// <summary>
    /// Generates a string based on the alphabet and a numeric index.
    /// This allows mapping an integer to a specific password combination.
    /// Example for alphabet "ab", maxLen 2: 
    /// 0 -> "a", 1 -> "b", 2 -> "aa", 3 -> "ab", 4 -> "ba", 5 -> "bb"
    /// </summary>
    public static string GenerateStringAtIndex(long index, string alphabet, int maxLength)
    {
        int baseSize = alphabet.Length;
        long currentStrLength = 1;
        long countAtLength = baseSize;

        // Determine the length of the string for the given index
        while (index >= countAtLength)
        {
            index -= countAtLength;
            currentStrLength++;
            countAtLength *= baseSize;

            if (currentStrLength > maxLength)
            {
                return string.Empty; // Index out of bounds for max length
            }
        }

        // Generate the string of `currentStrLength`
        Span<char> chars = stackalloc char[(int)currentStrLength];
        long tempIndex = index;
        for (int i = (int)currentStrLength - 1; i >= 0; i--)
        {
            chars[i] = alphabet[(int)(tempIndex % baseSize)];
            tempIndex /= baseSize;
        }

        return new string(chars);
    }
}
