using System;
using Mono.Debugging.Client;
using UnityDebugger.Adapter.Backend;
using UnityDebugger.Adapter.Dap;
using UnityDebugger.Adapter.Diagnostics;

namespace UnityDebugger.Adapter
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            DebuggerLoggingService.CustomLogger =
                new MonoDebuggerLogger((_, __) => { });
            try
            {
                var session = new UnityDebugSession(
                    () => new MonoDebuggerBackend(
                        () => new SoftDebuggerSessionFacade()));
                session.Start(
                    Console.OpenStandardInput(),
                    Console.OpenStandardOutput())
                    .GetAwaiter()
                    .GetResult();
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception.GetType().Name);
                return 1;
            }
            finally
            {
                DebuggerLoggingService.CustomLogger = null;
            }
        }
    }
}
