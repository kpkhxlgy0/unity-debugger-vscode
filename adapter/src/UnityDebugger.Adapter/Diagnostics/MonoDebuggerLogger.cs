using System;
using Mono.Debugging.Client;

namespace UnityDebugger.Adapter.Diagnostics
{
    internal sealed class MonoDebuggerLogger : ICustomLogger
    {
        private const string EventName = "mono-debugger-log";
        private readonly Action<string, string?> sink;

        public MonoDebuggerLogger(Action<string, string?> sink)
        {
            this.sink = sink ??
                throw new ArgumentNullException(nameof(sink));
        }

        public void LogError(string message, Exception ex)
        {
            Write(ex);
        }

        public void LogAndShowException(string message, Exception ex)
        {
            Write(ex);
        }

        public void LogMessage(
            string messageFormat,
            params object[] args)
        {
            Write(null);
        }

        public string? GetNewDebuggerLogFilename()
        {
            return null;
        }

        private void Write(Exception? exception)
        {
            try
            {
                sink(EventName, exception?.GetType().Name);
            }
            catch
            {
                // Diagnostics must never corrupt the DAP channel.
            }
        }
    }
}
