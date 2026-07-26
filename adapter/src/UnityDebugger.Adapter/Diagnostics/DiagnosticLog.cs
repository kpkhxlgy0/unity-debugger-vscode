using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace UnityDebugger.Adapter.Diagnostics
{
    internal interface IDiagnosticLog : IDisposable
    {
        void Write(
            string eventName,
            IReadOnlyDictionary<string, object> fields);
    }

    internal sealed class DiagnosticLog : IDiagnosticLog
    {
        private const int RetainedFileCount = 5;
        private static readonly HashSet<string> AllowedFields =
            new HashSet<string>(
                new[]
                {
                    "adapterVersion",
                    "event",
                    "processId",
                    "host",
                    "port",
                    "projectVersion",
                    "protocolVersion",
                    "breakpointCount",
                    "threadId",
                    "exitCode",
                    "exceptionType",
                },
                StringComparer.Ordinal);
        private readonly object writeLock = new object();
        private readonly TextWriter writer;
        private readonly PathRedactor redactor;
        private readonly bool ownsWriter;
        private bool disposed;

        public DiagnosticLog(
            TextWriter writer,
            PathRedactor redactor)
            : this(writer, redactor, false)
        {
        }

        private DiagnosticLog(
            TextWriter writer,
            PathRedactor redactor,
            bool ownsWriter)
        {
            this.writer = writer ??
                throw new ArgumentNullException(nameof(writer));
            this.redactor = redactor ??
                throw new ArgumentNullException(nameof(redactor));
            this.ownsWriter = ownsWriter;
        }

        public static DiagnosticLog CreateDefault()
        {
            var localRoot = Path.GetFullPath(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData));
            var logDirectory = ResolveLogDirectory(localRoot);
            VerifyInside(logDirectory, localRoot);
            Directory.CreateDirectory(logDirectory);
            RetainLatest(logDirectory);

            var fileName =
                "adapter-" +
                DateTime.UtcNow.ToString(
                    "yyyyMMddTHHmmssfffZ",
                    CultureInfo.InvariantCulture) +
                "-" +
                Process.GetCurrentProcess().Id.ToString(
                    CultureInfo.InvariantCulture) +
                ".log";
            var filePath = Path.GetFullPath(
                Path.Combine(logDirectory, fileName));
            VerifyInside(filePath, logDirectory);
            var stream = new FileStream(
                filePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.Read);
            var writer = new StreamWriter(
                stream,
                new UTF8Encoding(false))
            {
                AutoFlush = true,
            };
            return new DiagnosticLog(
                writer,
                new PathRedactor(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.UserProfile),
                    null),
                true);
        }

        internal static string ResolveLogDirectory(string localRoot)
        {
            return Path.GetFullPath(
                Path.Combine(
                    localRoot,
                    "unity-debugger-pure",
                    "logs"));
        }

        public void Write(
            string eventName,
            IReadOnlyDictionary<string, object> fields)
        {
            if (string.IsNullOrWhiteSpace(eventName))
                throw new ArgumentException(
                    "Diagnostic event name is required.",
                    nameof(eventName));
            if (fields == null)
                throw new ArgumentNullException(nameof(fields));
            foreach (var field in fields.Keys)
            {
                if (!AllowedFields.Contains(field))
                {
                    throw new ArgumentException(
                        "Diagnostic field is not allowlisted.",
                        nameof(fields));
                }
            }

            lock (writeLock)
            {
                if (disposed)
                    throw new ObjectDisposedException(
                        nameof(DiagnosticLog));
                writer.Write(
                    DateTime.UtcNow.ToString(
                        "O",
                        CultureInfo.InvariantCulture));
                writer.Write(" event=");
                writer.Write(Sanitize(eventName));
                foreach (var field in fields)
                {
                    writer.Write(' ');
                    writer.Write(field.Key);
                    writer.Write('=');
                    writer.Write(
                        Sanitize(
                            Convert.ToString(
                                field.Value,
                                CultureInfo.InvariantCulture) ??
                            string.Empty));
                }
                writer.WriteLine();
                writer.Flush();
            }
        }

        public void Dispose()
        {
            lock (writeLock)
            {
                if (disposed)
                    return;
                disposed = true;
                writer.Flush();
                if (ownsWriter)
                    writer.Dispose();
            }
        }

        private string Sanitize(string value) =>
            redactor.Redact(value)
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");

        private static void RetainLatest(string logDirectory)
        {
            var files = Directory
                .EnumerateFiles(
                    logDirectory,
                    "adapter-*.log",
                    SearchOption.TopDirectoryOnly)
                .Select(Path.GetFullPath)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .Skip(RetainedFileCount - 1)
                .ToArray();
            foreach (var file in files)
            {
                VerifyInside(file, logDirectory);
                File.Delete(file);
            }
        }

        private static void VerifyInside(
            string candidate,
            string parent)
        {
            var normalizedParent = Path.GetFullPath(parent)
                .TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            var normalizedCandidate = Path.GetFullPath(candidate);
            if (!normalizedCandidate.StartsWith(
                normalizedParent,
                StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Diagnostic path escaped its fixed directory.");
            }
        }
    }
}
