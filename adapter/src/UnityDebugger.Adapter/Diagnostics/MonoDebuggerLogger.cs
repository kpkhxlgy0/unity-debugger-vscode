using System;
using System.Collections.Generic;
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

        public MonoDebuggerLogger(IDiagnosticLog log)
            : this(
                (eventName, exceptionType) =>
                {
                    var fields = new Dictionary<string, object>();
                    if (exceptionType != null)
                        fields["exceptionType"] = exceptionType;
                    log.Write(eventName, fields);
                })
        {
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

        public void LogInternalEvent(string eventName)
        {
            Write(
                InternalDebuggerLog.IsAllowed(eventName)
                    ? eventName
                    : EventName,
                null);
        }

        public string? GetNewDebuggerLogFilename()
        {
            return null;
        }

        private void Write(Exception? exception)
        {
            Write(EventName, exception);
        }

        private void Write(string eventName, Exception? exception)
        {
            try
            {
                sink(eventName, exception?.GetType().Name);
            }
            catch
            {
                // Diagnostics must never corrupt the DAP channel.
            }
        }
    }
}
