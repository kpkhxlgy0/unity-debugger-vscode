using System;
using System.Collections.Generic;
using System.Diagnostics;
using Mono.Debugging.Client;
using UnityDebugger.Adapter.Backend;
using UnityDebugger.Adapter.Dap;
using UnityDebugger.Adapter.Diagnostics;
using VSCodeDebug;

namespace UnityDebugger.Adapter
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            DiagnosticLog? log = null;
            var exitCode = 1;
            try
            {
                log = DiagnosticLog.CreateDefault();
                var buildId = BuildIdentity.ReadFromDirectory(
                    AppContext.BaseDirectory);
                SafeWrite(
                    log,
                    "adapter.start",
                    new Dictionary<string, object>
                    {
                        ["adapterVersion"] = BuildIdentity.Version,
                        ["buildId"] = buildId,
                        ["processId"] =
                            Process.GetCurrentProcess().Id,
                    });
                ProtocolTrace.Sink = command =>
                    SafeWrite(
                        log,
                        "dap.command",
                        new Dictionary<string, object>
                        {
                            ["event"] = command,
                        });
                InternalDebuggerLog.Sink = eventName =>
                    SafeWrite(
                        log,
                        eventName,
                        new Dictionary<string, object>());
                DebuggerLoggingService.CustomLogger =
                    new MonoDebuggerLogger(log);
                var session = new UnityDebugSession(
                    () => new MonoDebuggingBackend(
                        () => new SoftDebuggerSessionFacade()));
                session.Start(
                    Console.OpenStandardInput(),
                    Console.OpenStandardOutput())
                    .GetAwaiter()
                    .GetResult();
                exitCode = 0;
            }
            catch (Exception exception)
            {
                if (log != null)
                {
                    SafeWrite(
                        log,
                        "adapter.error",
                        new Dictionary<string, object>
                        {
                            ["exceptionType"] =
                                exception.GetType().Name,
                        });
                }
            }
            finally
            {
                ProtocolTrace.Sink = _ => { };
                InternalDebuggerLog.Sink = null;
                DebuggerLoggingService.CustomLogger = null;
                if (log != null)
                {
                    SafeWrite(
                        log,
                        "adapter.exit",
                        new Dictionary<string, object>
                        {
                            ["exitCode"] = exitCode,
                        });
                    log.Dispose();
                }
            }
            return exitCode;
        }

        private static void SafeWrite(
            IDiagnosticLog log,
            string eventName,
            IReadOnlyDictionary<string, object> fields)
        {
            try
            {
                log.Write(eventName, fields);
            }
            catch
            {
                // Diagnostics must never corrupt stdout DAP transport.
            }
        }
    }
}
