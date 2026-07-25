using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Mono.Debugging.Client;
using Mono.Debugging.Soft;

namespace UnityDebugger.Adapter.Backend
{
    internal sealed class SoftDebuggerSessionFacade :
        ISoftDebuggerSessionFacade
    {
        private readonly UnitySoftDebuggerSession session;
        private readonly object breakpointLock = new object();
        private readonly Dictionary<long, Breakpoint> breakpoints =
            new Dictionary<long, Breakpoint>();
        private readonly Dictionary<BreakEvent, long> breakpointIds =
            new Dictionary<BreakEvent, long>();
        private long nextBreakpointId = 1;
        private bool detachCompleted;
        private bool disposed;

        public SoftDebuggerSessionFacade()
        {
            session = new UnitySoftDebuggerSession();
            session.TargetReady += (_, __) =>
                TargetReady?.Invoke(this, EventArgs.Empty);
            session.TargetExited += (_, __) =>
                TargetExited?.Invoke(this, EventArgs.Empty);
            session.TargetStopped += (_, arguments) =>
                RaiseStopped(arguments, BackendStopReason.Pause);
            session.TargetInterrupted += (_, arguments) =>
                RaiseStopped(arguments, BackendStopReason.Pause);
            session.TargetHitBreakpoint += (_, arguments) =>
                RaiseStopped(arguments, BackendStopReason.Breakpoint);
            session.TargetExceptionThrown += (_, arguments) =>
                RaiseStopped(arguments, BackendStopReason.Exception);
            session.TargetUnhandledException += (_, arguments) =>
                RaiseStopped(arguments, BackendStopReason.Exception);
            session.TargetThreadStarted += (_, arguments) =>
                RaiseThread(arguments, true);
            session.TargetThreadStopped += (_, arguments) =>
                RaiseThread(arguments, false);
            session.AssemblyUnloaded += (_, __) =>
                AssemblyUnloaded?.Invoke(this, EventArgs.Empty);
            session.AssemblyLoaded += (_, __) =>
                AssemblyLoaded?.Invoke(this, EventArgs.Empty);
            session.Breakpoints.BreakEventStatusChanged +=
                OnBreakEventStatusChanged;
        }

        public event EventHandler? TargetReady;
        public event EventHandler? TargetExited;
        public event EventHandler<BackendStoppedEventArgs>? TargetStopped;
        public event EventHandler<BackendThreadEventArgs>? ThreadChanged;
        public event EventHandler? AssemblyUnloaded;
        public event EventHandler? AssemblyLoaded;
        public event EventHandler<BackendBreakpointChangedEventArgs>?
            BreakpointChanged;

        public bool IsRunning => session.IsRunning;
        public bool HasExited => session.HasExited;

        public async Task ConnectAsync(
            IPAddress address,
            int port,
            int maxConnectionAttempts,
            int connectionAttemptIntervalMilliseconds,
            CancellationToken cancellationToken)
        {
            if (disposed)
                throw new ObjectDisposedException(
                    nameof(SoftDebuggerSessionFacade));

            var completion =
                new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
            EventHandler<TargetEventArgs>? readyHandler = null;
            EventHandler<TargetEventArgs>? exitHandler = null;
            readyHandler = (_, __) => completion.TrySetResult(true);
            exitHandler = (_, __) => completion.TrySetException(
                new InvalidOperationException(
                    "Editor exited before debugger attach completed."));
            session.TargetReady += readyHandler;
            session.TargetExited += exitHandler;
            session.ExceptionHandler = exception =>
            {
                completion.TrySetException(exception);
                return true;
            };

            using (cancellationToken.Register(
                () => completion.TrySetCanceled()))
            {
                try
                {
                    var connectArgs = new SoftDebuggerConnectArgs(
                        string.Empty,
                        address,
                        port)
                    {
                        MaxConnectionAttempts = maxConnectionAttempts,
                        TimeBetweenConnectionAttempts =
                            connectionAttemptIntervalMilliseconds,
                    };
                    var evaluationOptions =
                        EvaluationOptions.DefaultOptions.Clone();
                    evaluationOptions.AllowTargetInvoke = false;
                    evaluationOptions.AllowMethodEvaluation = false;
                    evaluationOptions.AllowToStringCalls = false;
                    var options = new DebuggerSessionOptions
                    {
                        EvaluationOptions = evaluationOptions,
                    };
                    session.SetOutputOptions(new OutputOptions());
                    session.Run(
                        new SoftDebuggerStartInfo(connectArgs),
                        options);
                    await completion.Task.ConfigureAwait(false);
                }
                finally
                {
                    session.TargetReady -= readyHandler;
                    session.TargetExited -= exitHandler;
                    session.ExceptionHandler = null;
                }
            }
        }

        public void Continue()
        {
            if (disposed)
                throw new ObjectDisposedException(
                    nameof(SoftDebuggerSessionFacade));
            session.Continue();
        }

        public BackendBoundBreakpoint BindBreakpoint(
            LogicalBreakpoint breakpoint)
        {
            if (disposed)
                throw new ObjectDisposedException(
                    nameof(SoftDebuggerSessionFacade));

            var value = session.Breakpoints.Add(
                breakpoint.SourcePath,
                breakpoint.Line,
                Math.Max(1, breakpoint.Column));
            if (value == null)
            {
                return new BackendBoundBreakpoint(
                    0,
                    false,
                    breakpoint.Line,
                    "Symbols are not loaded.");
            }

            value.ConditionExpression = breakpoint.Condition;
            value.CommitChanges();
            long id;
            lock (breakpointLock)
            {
                id = nextBreakpointId++;
                breakpoints.Add(id, value);
                breakpointIds.Add(value, id);
            }
            return ToBackendBreakpoint(id, value);
        }

        public void RemoveBreakpoint(long backendBreakpointId)
        {
            Breakpoint? value;
            lock (breakpointLock)
            {
                if (!breakpoints.TryGetValue(
                    backendBreakpointId,
                    out value))
                {
                    return;
                }
                breakpoints.Remove(backendBreakpointId);
                breakpointIds.Remove(value);
            }
            session.Breakpoints.Remove(value);
        }

        public void Detach()
        {
            if (detachCompleted)
                return;
            detachCompleted = true;

            try
            {
                if (session.IsConnected)
                {
                    if (!session.IsRunning && !session.HasExited)
                        session.Continue();
                    session.Detach();
                }
            }
            finally
            {
                session.Dispose();
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            Detach();
        }

        private void OnBreakEventStatusChanged(
            object? sender,
            BreakEventArgs arguments)
        {
            if (!(arguments.BreakEvent is Breakpoint value))
                return;

            long id;
            lock (breakpointLock)
            {
                if (!breakpointIds.TryGetValue(value, out id))
                    return;
            }
            BreakpointChanged?.Invoke(
                this,
                new BackendBreakpointChangedEventArgs(
                    ToBackendBreakpoint(id, value)));
        }

        private BackendBoundBreakpoint ToBackendBreakpoint(
            long id,
            Breakpoint value)
        {
            var status = value.GetStatus(session);
            var verified = status == BreakEventStatus.Bound;
            return new BackendBoundBreakpoint(
                id,
                verified,
                value.Line,
                verified
                    ? null
                    : status == BreakEventStatus.NotBound ||
                      status == BreakEventStatus.Disconnected
                        ? "Symbols are not loaded."
                        : "Breakpoint could not be bound.");
        }

        private void RaiseStopped(
            TargetEventArgs arguments,
            BackendStopReason reason)
        {
            TargetStopped?.Invoke(
                this,
                new BackendStoppedEventArgs(
                    reason,
                    arguments.Thread?.Id ?? 0,
                    null));
        }

        private void RaiseThread(
            TargetEventArgs arguments,
            bool started)
        {
            ThreadChanged?.Invoke(
                this,
                new BackendThreadEventArgs(
                    arguments.Thread?.Id ?? 0,
                    started));
        }
    }
}
