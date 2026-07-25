using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace UnityDebugger.Adapter.Backend
{
    internal interface ISoftDebuggerSessionFacade : IDisposable
    {
        event EventHandler? TargetReady;
        event EventHandler? TargetExited;
        event EventHandler<BackendStoppedEventArgs>? TargetStopped;
        event EventHandler<BackendThreadEventArgs>? ThreadChanged;
        event EventHandler? AssemblyUnloaded;
        event EventHandler? AssemblyLoaded;

        bool IsRunning { get; }
        bool HasExited { get; }
        Task ConnectAsync(
            IPAddress address,
            int port,
            int maxConnectionAttempts,
            int connectionAttemptIntervalMilliseconds,
            CancellationToken cancellationToken);
        void Detach();
        void Continue();
    }
}
