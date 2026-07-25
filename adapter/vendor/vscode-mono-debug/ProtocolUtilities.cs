using System;
using System.Reflection;
using System.Text.RegularExpressions;

namespace VSCodeDebug
{
    internal static class ProtocolUtilities
    {
        private static readonly Regex TokenPattern =
            new Regex(@"\{(_\w+)\}", RegexOptions.Compiled);

        public static string ExpandVariables(
            string format,
            object variables)
        {
            if (format == null)
            {
                return string.Empty;
            }

            return TokenPattern.Replace(
                format,
                match => Resolve(match.Groups[1].Value, variables));
        }

        private static string Resolve(string name, object variables)
        {
            if (variables == null)
            {
                return "<unavailable>";
            }

            var property = variables.GetType().GetProperty(
                name,
                BindingFlags.Instance | BindingFlags.Public);
            var value = property?.GetValue(variables, null);
            return value?.ToString() ?? "<unavailable>";
        }
    }
}
