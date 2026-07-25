using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using UnityDebugger.Adapter.Backend;

namespace UnityDebugger.Adapter.Tests.Backend
{
    internal sealed class FakeSoftDebuggerSessionFacade :
        ISoftDebuggerSessionFacade
    {
        public event EventHandler? TargetReady;
        public event EventHandler? TargetExited;
        public event EventHandler<BackendStoppedEventArgs>? TargetStopped;
        public event EventHandler<BackendThreadEventArgs>? ThreadChanged;
        public event EventHandler? AssemblyUnloaded;
        public event EventHandler? AssemblyLoaded;
        public event EventHandler<BackendBreakpointChangedEventArgs>?
            BreakpointChanged;

        public bool IsRunning { get; set; } = true;
        public bool HasExited { get; set; }
        public IPAddress? ConnectAddress { get; private set; }
        public int ConnectPort { get; private set; }
        public int MaxConnectionAttempts { get; private set; }
        public int ConnectionAttemptIntervalMilliseconds {
            get;
            private set;
        }
        public Exception? ConnectException { get; set; }
        public int ConnectCount { get; private set; }
        public int DetachCount { get; private set; }
        public int DisposeCount { get; private set; }
        public int ContinueCount { get; private set; }
        public int RemoveBreakpointCount { get; private set; }
        public List<LogicalBreakpoint> Bound { get; } =
            new List<LogicalBreakpoint>();

        public Task ConnectAsync(
            IPAddress address,
            int port,
            int maxConnectionAttempts,
            int connectionAttemptIntervalMilliseconds,
            CancellationToken cancellationToken)
        {
            ConnectCount++;
            ConnectAddress = address;
            ConnectPort = port;
            MaxConnectionAttempts = maxConnectionAttempts;
            ConnectionAttemptIntervalMilliseconds =
                connectionAttemptIntervalMilliseconds;
            if (ConnectException != null)
                return Task.FromException(ConnectException);
            TargetReady?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }

        public void Detach()
        {
            DetachCount++;
        }

        public void Continue()
        {
            ContinueCount++;
            IsRunning = true;
        }

        public BackendBoundBreakpoint BindBreakpoint(
            LogicalBreakpoint breakpoint)
        {
            Bound.Add(breakpoint);
            return new BackendBoundBreakpoint(
                Bound.Count,
                true,
                breakpoint.Line,
                null);
        }

        public void RemoveBreakpoint(long backendBreakpointId)
        {
            RemoveBreakpointCount++;
        }

        public void Dispose()
        {
            DisposeCount++;
        }

        public void RaiseTargetExited()
        {
            HasExited = true;
            TargetExited?.Invoke(this, EventArgs.Empty);
        }

        public void RaiseTargetStopped(
            BackendStoppedEventArgs arguments) =>
            TargetStopped?.Invoke(this, arguments);

        public void RaiseThreadChanged(
            BackendThreadEventArgs arguments) =>
            ThreadChanged?.Invoke(this, arguments);

        public void RaiseAssemblyUnloaded() =>
            AssemblyUnloaded?.Invoke(this, EventArgs.Empty);

        public void RaiseAssemblyLoaded() =>
            AssemblyLoaded?.Invoke(this, EventArgs.Empty);

        public void RaiseBreakpointChanged(
            BackendBreakpointChangedEventArgs arguments) =>
            BreakpointChanged?.Invoke(this, arguments);
    }
}
