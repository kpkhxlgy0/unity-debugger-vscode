using System;
using System.Collections.Generic;
using UnityDebugger.Adapter.Dap;

namespace UnityDebugger.TestAdapter
{
    internal static class Program
    {
        private static readonly HashSet<string> Scenarios =
            new HashSet<string>(
                new[]
                {
                    "normal",
                    "reload",
                    "exception",
                    "protocol-error",
                    "backend-crash",
                },
                StringComparer.Ordinal);

        private static int Main(string[] args)
        {
            if (
                args.Length != 1 ||
                !Scenarios.Contains(args[0]))
            {
                return 2;
            }

            var backend = new ScenarioDebuggerBackend(args[0]);
            try
            {
                var session = new UnityDebugSession(() => backend);
                session.Start(
                    Console.OpenStandardInput(),
                    Console.OpenStandardOutput())
                    .GetAwaiter()
                    .GetResult();
                var expectedDisposeCount =
                    args[0] == "protocol-error" ? 0 : 1;
                return backend.DisposeCount == expectedDisposeCount
                    ? 0
                    : 3;
            }
            catch
            {
                return 1;
            }
        }
    }
}
