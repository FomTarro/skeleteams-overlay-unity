using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Skeletom.Essentials.Utils
{
    public static class TextUtils
    {
        private static Regex REGEX_MODERN_LANGUAGE_FILTER = new Regex("([^\\u0000-\\ud7ff])");

        private static Regex REGEX_PUNCTUATION_FILTER = new Regex("[?.,!#\"/\\ '<>#\t\n;:]+");

        /// <summary>
        /// Filters out characters from a string that do not belong to a modern written language, such as emoji and ancient languages
        /// </summary>
        /// <param name="input">The string to filter</param>
        public static string FilterModernLanguage(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return input;
            }
            StringBuilder output = new StringBuilder();
            char c = '\0';
            for (int i = 0; i < input.Length; i++)
            {
                c = FilterModernLanguageChars(input[i]);
                if (c != '\0')
                {
                    output.Append(c);
                }
            }
            return output.ToString();
        }

        /// <summary>
        /// Replaces the given char with null '\0' if the char is not part of a modern language set
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static char FilterModernLanguageChars(char input)
        {
            return REGEX_MODERN_LANGUAGE_FILTER.IsMatch(input.ToString()) ? '\0' : input;
        }

        /// <summary>
        /// Return the index of the first punctuation character in a string. Returns -1 if none can be found.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static int GetIndexOfPunctuation(string input)
        {
            if (REGEX_PUNCTUATION_FILTER.IsMatch(input))
            {
                return REGEX_PUNCTUATION_FILTER.Match(input).Index;
            }
            else
            {
                return -1;
            }
        }

        /// <summary>
        /// Removes characters that cannot exist in a file name, forces to lowercase, and optionally removes spaces.
        /// </summary>
        /// <param name="input"></param>
        /// <param name="replaceSpaces">Replace spaces with underscores?</param>
        /// <returns></returns>
        public static string MakeFilenameSafe(string input, bool replaceSpaces = true)
        {
            if (string.IsNullOrEmpty(input))
            {
                return input;
            }
            string output = RemoveInvalidCharacters(input.ToLower().Trim());
            if (replaceSpaces)
            {
                output = output.Replace(' ', '_');
            }
            return output;
        }

        /// <summary>
        /// Removes characters that cannot exist in a file name
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        private static string RemoveInvalidCharacters(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return input;
            }
            string output = FilterModernLanguage(input);
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                output = output.Replace(c.ToString(), string.Empty);
            }
            return output;
        }

        /// <summary>
        /// Compresses consecutive whitespace strings into a single whitespace
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static string RemoveConsecutiveWhitespace(string input)
        {
            return Regex.Replace(input, @"\s+", " ");
        }
    }
}