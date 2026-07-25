using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using Mono.Debugger.Soft;

namespace UnityDebugger.Adapter.Backend
{
    internal sealed class MonoDebuggerBackend : IDebuggerBackend
    {
        private const int MaxConnectionAttempts = 10;
        private const int ConnectionAttemptIntervalMilliseconds = 500;
        private readonly Func<ISoftDebuggerSessionFacade> facadeFactory;
        private ISoftDebuggerSessionFacade? facade;
        private bool disposed;
        private bool terminatedRaised;

        public MonoDebuggerBackend(
            Func<ISoftDebuggerSessionFacade> facadeFactory)
        {
            this.facadeFactory = facadeFactory ??
                throw new ArgumentNullException(nameof(facadeFactory));
        }

        public event EventHandler<BackendStoppedEventArgs>? Stopped;
#pragma warning disable CS0067
        public event EventHandler? Continued;
#pragma warning restore CS0067
        public event EventHandler<BackendThreadEventArgs>? ThreadChanged;
#pragma warning disable CS0067
        public event EventHandler<BackendBreakpointChangedEventArgs>?
            BreakpointChanged;
#pragma warning restore CS0067
        public event EventHandler? ReloadStarted;
        public event EventHandler? ReloadCompleted;
        public event EventHandler? Terminated;

        public bool IsAttached { get; private set; }

        public void Attach(AttachTarget target)
        {
            ThrowIfDisposed();
            if (facade != null || IsAttached)
                throw new InvalidOperationException(
                    "The debugger backend is already attached.");
            if (!IPAddress.IsLoopback(target.Address))
                throw new DebuggerBackendException(
                    "Only loopback Editor targets are allowed.");

            var createdFacade = facadeFactory();
            facade = createdFacade;
            Subscribe(createdFacade);
            try
            {
                createdFacade.ConnectAsync(
                    target.Address,
                    target.Port,
                    MaxConnectionAttempts,
                    ConnectionAttemptIntervalMilliseconds,
                    CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();
                IsAttached = true;
            }
            catch (VMMismatchException)
            {
                ReleaseFacade();
                throw new DebuggerBackendException(
                    "Editor uses an incompatible Mono Soft Debugger " +
                    "protocol.");
            }
            catch (Exception)
            {
                ReleaseFacade();
                throw new DebuggerBackendException(
                    $"Could not connect to local Editor at " +
                    $"127.0.0.1:{target.Port}. Check that the Editor " +
                    "process is alive, Code Optimization is set to Debug, " +
                    "and the local firewall is not blocking the port.");
            }
        }

        public void Disconnect()
        {
            ReleaseFacade();
        }

        public IReadOnlyList<BackendThread> GetThreads() =>
            throw NotImplemented();

        public IReadOnlyList<BackendStackFrame> GetStackTrace(
            long threadId,
            int startFrame,
            int levels) => throw NotImplemented();

        public IReadOnlyList<BackendScope> GetScopes(long frameId) =>
            throw NotImplemented();

        public IReadOnlyList<BackendVariable> GetVariables(
            long variablesReference) => throw NotImplemented();

        public BackendEvaluationResult Evaluate(
            long frameId,
            string expression) => throw NotImplemented();

        public BackendBoundBreakpoint BindBreakpoint(
            LogicalBreakpoint breakpoint) => throw NotImplemented();

        public void RemoveBreakpoint(long backendBreakpointId) =>
            throw NotImplemented();

        public void Continue(long threadId)
        {
            RequireAttached();
            facade!.Continue();
        }

        public void Pause(long threadId) => throw NotImplemented();
        public void StepIn(long threadId) => throw NotImplemented();
        public void StepOver(long threadId) => throw NotImplemented();
        public void StepOut(long threadId) => throw NotImplemented();

        public void ConfigureExceptions(ExceptionBreakMode mode) =>
            throw NotImplemented();

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            ReleaseFacade();
        }

        private void Subscribe(ISoftDebuggerSessionFacade value)
        {
            value.TargetExited += OnTargetExited;
            value.TargetStopped += OnTargetStopped;
            value.ThreadChanged += OnThreadChanged;
            value.AssemblyUnloaded += OnAssemblyUnloaded;
            value.AssemblyLoaded += OnAssemblyLoaded;
        }

        private void Unsubscribe(ISoftDebuggerSessionFacade value)
        {
            value.TargetExited -= OnTargetExited;
            value.TargetStopped -= OnTargetStopped;
            value.ThreadChanged -= OnThreadChanged;
            value.AssemblyUnloaded -= OnAssemblyUnloaded;
            value.AssemblyLoaded -= OnAssemblyLoaded;
        }

        private void ReleaseFacade()
        {
            var value = facade;
            if (value == null)
                return;

            facade = null;
            IsAttached = false;
            Unsubscribe(value);
            try
            {
                value.Detach();
            }
            finally
            {
                value.Dispose();
            }
        }

        private void OnTargetExited(object? sender, EventArgs arguments)
        {
            IsAttached = false;
            if (terminatedRaised)
                return;
            terminatedRaised = true;
            Terminated?.Invoke(this, EventArgs.Empty);
        }

        private void OnTargetStopped(
            object? sender,
            BackendStoppedEventArgs arguments) =>
            Stopped?.Invoke(this, arguments);

        private void OnThreadChanged(
            object? sender,
            BackendThreadEventArgs arguments) =>
            ThreadChanged?.Invoke(this, arguments);

        private void OnAssemblyUnloaded(
            object? sender,
            EventArgs arguments) =>
            ReloadStarted?.Invoke(this, EventArgs.Empty);

        private void OnAssemblyLoaded(
            object? sender,
            EventArgs arguments) =>
            ReloadCompleted?.Invoke(this, EventArgs.Empty);

        private void RequireAttached()
        {
            ThrowIfDisposed();
            if (!IsAttached || facade == null)
                throw new InvalidOperationException(
                    "The debugger backend is not attached.");
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(
                    nameof(MonoDebuggerBackend));
        }

        private static InvalidOperationException NotImplemented() =>
            new InvalidOperationException(
                "The debugger operation is not implemented yet.");
    }
}
