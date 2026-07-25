using System;
using UnityDebugger.Adapter.Backend;
using UnityDebugger.Adapter.Dap;

namespace UnityDebugger.Adapter
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            try
            {
                var session = new UnityDebugSession(
                    () => new UnavailableDebuggerBackend());
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
        }
    }
}
