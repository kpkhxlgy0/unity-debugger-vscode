using System;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace UnityDebugger.Adapter.Diagnostics
{
    internal static class BuildIdentity
    {
        public const string Version = "0.4.0";
        private const string UnknownBuildId = Version + "+gunknown";
        private static readonly Regex CommitPattern = new Regex(
            "^[0-9a-f]{40}$",
            RegexOptions.CultureInvariant);
        private static readonly Regex BuildIdPattern = new Regex(
            "^0\\.4\\.0\\+g[0-9a-f]{12}$",
            RegexOptions.CultureInvariant);

        public static string ReadFromDirectory(string directory)
        {
            try
            {
                var filePath = Path.Combine(directory, "build-info.json");
                var root = JObject.Parse(File.ReadAllText(filePath));
                var version = (string?)root["version"];
                var commit = (string?)root["commit"];
                var buildId = (string?)root["buildId"];
                if (version != Version ||
                    commit == null ||
                    !CommitPattern.IsMatch(commit) ||
                    buildId == null ||
                    !BuildIdPattern.IsMatch(buildId) ||
                    buildId != Version + "+g" + commit.Substring(0, 12))
                {
                    return UnknownBuildId;
                }
                return buildId;
            }
            catch
            {
                return UnknownBuildId;
            }
        }
    }
}
