using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace UnityDebugger.Adapter.Diagnostics
{
    internal sealed class PathRedactor
    {
        private readonly IReadOnlyList<Replacement> replacements;

        public PathRedactor(
            string? userProfilePath,
            string? workspacePath)
        {
            var values = new List<Replacement>();
            Add(values, workspacePath, "<workspace>");
            Add(values, userProfilePath, "<user-home>");
            replacements = values
                .OrderByDescending(item => item.Path.Length)
                .ToArray();
        }

        public string Redact(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;
            var result = value;
            foreach (var replacement in replacements)
            {
                result = replacement.Pattern.Replace(
                    result,
                    replacement.Token);
            }
            return result;
        }

        private static void Add(
            ICollection<Replacement> values,
            string? path,
            string token)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;
            string normalized;
            try
            {
                normalized = Path.GetFullPath(path)
                    .TrimEnd(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar);
            }
            catch (
                Exception exception
                ) when (
                    exception is ArgumentException ||
                    exception is NotSupportedException ||
                    exception is PathTooLongException)
            {
                return;
            }
            if (normalized.Length == 0)
                return;

            var pattern = Regex.Escape(normalized)
                .Replace(@"\\", @"[\\/]");
            values.Add(
                new Replacement(
                    normalized,
                    token,
                    new Regex(
                        pattern,
                        RegexOptions.IgnoreCase |
                        RegexOptions.CultureInvariant)));
        }

        private sealed class Replacement
        {
            public Replacement(
                string path,
                string token,
                Regex pattern)
            {
                Path = path;
                Token = token;
                Pattern = pattern;
            }

            public string Path { get; }
            public string Token { get; }
            public Regex Pattern { get; }
        }
    }
}
