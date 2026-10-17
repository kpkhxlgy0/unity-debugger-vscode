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

        void OpenSecondary(string path);
    }

    internal sealed class DiagnosticLog : IDiagnosticLog
    {
        private const int RetainedFileCount = 5;
        private static readonly HashSet<string> AllowedFields =
            new HashSet<string>(
                new[]
                {
                    "adapterVersion",
                    "buildId",
                    "event",
                    "processId",
                    "host",
                    "port",
                    "projectVersion",
                    "protocolVersion",
                    "breakpointCount",
                    "threadId",
                    "exceptionObjectId",
                    "exceptionRequestId",
                    "eventCount",
                    "stopKind",
                    "exitCode",
                    "exceptionType",
                },
                StringComparer.Ordinal);
        private readonly object writeLock = new object();
        private readonly TextWriter writer;
        private readonly PathRedactor redactor;
        private readonly bool ownsWriter;
        private TextWriter? secondaryWriter;
        private string? secondaryPath;
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

        // Mirrors every allowlisted line to a second append stream, so a
        // client that names a log file receives the same sanitized content as
        // the default log. Idempotent for the same path: a repeated call
        // keeps the stream that is already open. A later call with a different
        // path replaces the secondary sink, which is sufficient because the
        // adapter honours a single logFile per attached session.
        public void OpenSecondary(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException(
                    "Secondary diagnostic log path is required.",
                    nameof(path));
            }
            var fullPath = Path.GetFullPath(path);
            lock (writeLock)
            {
                if (disposed)
                {
                    throw new ObjectDisposedException(
                        nameof(DiagnosticLog));
                }
                if (string.Equals(
                    fullPath,
                    secondaryPath,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
                CloseSecondary();
                var directory = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);
                var stream = new FileStream(
                    fullPath,
                    FileMode.Append,
                    FileAccess.Write,
                    FileShare.Read);
                secondaryWriter = new StreamWriter(
                    stream,
                    new UTF8Encoding(false))
                {
                    AutoFlush = true,
                };
                secondaryPath = fullPath;
            }
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
            // The allowlist gate runs before either sink is touched, so a
            // rejected field reaches neither file.
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
                // Formatting carries the timestamp, so it belongs inside the
                // critical section: sampling outside it lets a writer that
                // acquires the lock first emit the later timestamp, and the
                // lines in both files then run backwards in time.
                var line = FormatLine(eventName, fields);
                writer.WriteLine(line);
                writer.Flush();
                if (secondaryWriter != null)
                {
                    secondaryWriter.WriteLine(line);
                    secondaryWriter.Flush();
                }
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
                CloseSecondary();
            }
        }

        private void CloseSecondary()
        {
            if (secondaryWriter == null)
                return;
            var previous = secondaryWriter;
            secondaryWriter = null;
            secondaryPath = null;
            try
            {
                previous.Dispose();
            }
            catch
            {
                // A user-supplied path can go away mid-session — a dropped
                // network share, a full disk — and disposing the sink is the
                // last thing its owner does. The writer is already detached
                // above, so nothing can retry it, and the caller treats a
                // diagnostics failure as non-fatal everywhere else: the open
                // path reports logFile.redirectFailed and attaches anyway.
            }
        }

        private string FormatLine(
            string eventName,
            IReadOnlyDictionary<string, object> fields)
        {
            var line = new StringBuilder();
            line.Append(
                DateTime.UtcNow.ToString(
                    "O",
                    CultureInfo.InvariantCulture));
            line.Append(" event=");
            line.Append(Sanitize(eventName));
            foreach (var field in fields)
            {
                line.Append(' ');
                line.Append(field.Key);
                line.Append('=');
                line.Append(
                    Sanitize(
                        Convert.ToString(
                            field.Value,
                            CultureInfo.InvariantCulture) ??
                        string.Empty));
            }
            return line.ToString();
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
