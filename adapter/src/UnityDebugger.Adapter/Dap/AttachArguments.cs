using System;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityDebugger.Adapter.Backend;

namespace UnityDebugger.Adapter.Dap
{
    internal static class AttachArguments
    {
        private static readonly Regex VersionPattern = new Regex(
            @"^(?:\d{4}\.\d+\.\d+[abfpxt]\d+|\d{4}\.\d+\.\d+)$",
            RegexOptions.CultureInvariant);

        public static AttachTarget Parse(JObject arguments)
        {
            if (arguments == null)
                throw new AttachArgumentException(
                    "Attach arguments are required.");

            var processId = RequiredInteger(arguments, "__processId");
            if (processId <= 0)
                throw new AttachArgumentException(
                    "__processId must be a positive integer.");

            IPAddress address;
            try
            {
                address = IPAddress.Parse(
                    RequiredString(arguments, "__host"));
            }
            catch (FormatException)
            {
                throw new AttachArgumentException(
                    "__host must be a loopback IP address.");
            }
            if (!IPAddress.IsLoopback(address))
                throw new AttachArgumentException(
                    "Only loopback Editor targets are allowed.");

            var port = RequiredInteger(arguments, "__port");
            if (port <= 0 || port > 65535)
                throw new AttachArgumentException(
                    "__port must be between 1 and 65535.");

            var workspaceRoot = RequiredString(
                arguments,
                "__workspaceRoot");
            if (string.IsNullOrWhiteSpace(workspaceRoot))
                throw new AttachArgumentException(
                    "__workspaceRoot must not be empty.");
            try
            {
                workspaceRoot = Path.GetFullPath(workspaceRoot);
            }
            catch (Exception exception)
                when (
                    exception is ArgumentException ||
                    exception is NotSupportedException ||
                    exception is PathTooLongException)
            {
                throw new AttachArgumentException(
                    "__workspaceRoot is not a valid path.");
            }

            var projectVersion = RequiredString(
                arguments,
                "__projectVersion");
            if (!VersionPattern.IsMatch(projectVersion))
                throw new AttachArgumentException(
                    "__projectVersion is malformed.");
            if (
                !projectVersion.StartsWith(
                    "2022.3.",
                    StringComparison.Ordinal) &&
                !projectVersion.StartsWith(
                    "6000.",
                    StringComparison.Ordinal))
            {
                throw new AttachArgumentException(
                    "The Editor version is outside the version 0.1.1 " +
                    "compatibility policy.");
            }

            return new AttachTarget(
                processId,
                address,
                port,
                workspaceRoot,
                projectVersion);
        }

        private static int RequiredInteger(
            JObject arguments,
            string property)
        {
            var token = arguments[property];
            if (token == null || token.Type != JTokenType.Integer)
                throw new AttachArgumentException(
                    $"{property} must be an integer.");
            try
            {
                return token.Value<int>();
            }
            catch (Exception exception)
                when (
                    exception is OverflowException ||
                    exception is FormatException)
            {
                throw new AttachArgumentException(
                    $"{property} must be an integer.");
            }
        }

        private static string RequiredString(
            JObject arguments,
            string property)
        {
            var token = arguments[property];
            if (token == null || token.Type != JTokenType.String)
                throw new AttachArgumentException(
                    $"{property} must be a string.");
            return token.Value<string>()!;
        }
    }

    internal sealed class AttachArgumentException : Exception
    {
        public AttachArgumentException(string message)
            : base(message)
        {
        }
    }
}
