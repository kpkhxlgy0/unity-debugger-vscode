using System;
using System.IO;

namespace UnityDebugger.Adapter.Source
{
    internal sealed class MappedSource
    {
        public MappedSource(
            bool available,
            string name,
            string? path,
            int sourceReference,
            string? message)
        {
            Available = available;
            Name = name;
            Path = path;
            SourceReference = sourceReference;
            Message = message;
        }

        public bool Available { get; }
        public string Name { get; }
        public string? Path { get; }
        public int SourceReference { get; }
        public string? Message { get; }
    }

    internal sealed class SourceMapper
    {
        private const string MissingMessage =
            "Source file is unavailable in this workspace.";
        private readonly string workspaceRoot;
        private readonly string workspacePrefix;
        private readonly Func<string, bool> fileExists;

        public SourceMapper(
            string workspaceRoot,
            Func<string, bool> fileExists)
        {
            this.workspaceRoot = Path.GetFullPath(workspaceRoot)
                .TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);
            workspacePrefix = this.workspaceRoot +
                Path.DirectorySeparatorChar;
            this.fileExists = fileExists ??
                throw new ArgumentNullException(nameof(fileExists));
        }

        public MappedSource ToClientPath(string runtimePath)
        {
            string normalized;
            try
            {
                normalized = Path.GetFullPath(runtimePath);
            }
            catch (
                Exception exception
                ) when (
                    exception is ArgumentException ||
                    exception is NotSupportedException ||
                    exception is PathTooLongException)
            {
                return Missing();
            }

            var inside =
                string.Equals(
                    normalized,
                    workspaceRoot,
                    StringComparison.OrdinalIgnoreCase) ||
                normalized.StartsWith(
                    workspacePrefix,
                    StringComparison.OrdinalIgnoreCase);
            if (!inside || !fileExists(normalized))
                return Missing();

            return new MappedSource(
                true,
                Path.GetFileName(normalized),
                normalized,
                0,
                null);
        }

        private static MappedSource Missing() =>
            new MappedSource(
                false,
                "Unavailable source",
                null,
                0,
                MissingMessage);
    }
}
