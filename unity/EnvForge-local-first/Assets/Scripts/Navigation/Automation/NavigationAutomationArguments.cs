using System;
using System.Globalization;
using System.IO;

namespace EnvForge.Navigation.Automation
{
    internal static class NavigationAutomationArguments
    {
        public static bool HasFlag(string argumentName)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int index = 0; index < args.Length; index++)
            {
                if (string.Equals(args[index], argumentName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public static string GetValue(string argumentName)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int index = 0; index < args.Length - 1; index++)
            {
                if (string.Equals(args[index], argumentName, StringComparison.OrdinalIgnoreCase))
                {
                    return args[index + 1];
                }
            }

            return null;
        }

        public static int GetInt(string argumentName, int fallback, int minimum = int.MinValue)
        {
            string value = GetValue(argumentName);
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
                ? Math.Max(minimum, parsed)
                : fallback;
        }

        public static float GetFloat(string argumentName, float fallback, float minimum = float.NegativeInfinity)
        {
            string value = GetValue(argumentName);
            return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed)
                ? Math.Max(minimum, parsed)
                : fallback;
        }

        public static string GetFullPath(string argumentName)
        {
            string value = GetValue(argumentName);
            return string.IsNullOrWhiteSpace(value) ? null : Path.GetFullPath(value);
        }
    }
}
