using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Mono.Debugger.Soft;

namespace UnityDebugger.Adapter.Backend
{
    internal sealed class MonoDebuggerBackend : IDebuggerBackend
    {
        private const int MaxConnectionAttempts = 10;
        private const int ConnectionAttemptIntervalMilliseconds = 500;
        private readonly Func<ISoftDebuggerSessionFacade> facadeFactory;
        private readonly AssemblyReloadCoordinator reloadCoordinator;
        private readonly ReconnectController reconnectController;
        private readonly Func<int, bool> processIsAlive;
        private readonly Func<TimeSpan, CancellationToken, Task> retryDelay;
        private ISoftDebuggerSessionFacade? facade;
        private AttachTarget? target;
        private CancellationTokenSource? reconnectCancellation;
        private ExceptionBreakMode exceptionMode;
        private bool disconnectRequested;
        private bool disposed;
        private bool terminatedRaised;

        public MonoDebuggerBackend(
            Func<ISoftDebuggerSessionFacade> facadeFactory)
            : this(
                facadeFactory,
                new AssemblyReloadCoordinator(
                    (duration, cancellationToken) =>
                        Task.Delay(duration, cancellationToken)),
                new ReconnectController(),
                DefaultProcessIsAlive,
                (duration, cancellationToken) =>
                    Task.Delay(duration, cancellationToken))
        {
        }

        internal MonoDebuggerBackend(
            Func<ISoftDebuggerSessionFacade> facadeFactory,
            AssemblyReloadCoordinator reloadCoordinator,
            ReconnectController reconnectController,
            Func<int, bool> processIsAlive,
            Func<TimeSpan, CancellationToken, Task> retryDelay)
        {
            this.facadeFactory = facadeFactory ??
                throw new ArgumentNullException(nameof(facadeFactory));
            this.reloadCoordinator = reloadCoordinator ??
                throw new ArgumentNullException(
                    nameof(reloadCoordinator));
            this.reconnectController = reconnectController ??
                throw new ArgumentNullException(
                    nameof(reconnectController));
            this.processIsAlive = processIsAlive ??
                throw new ArgumentNullException(nameof(processIsAlive));
            this.retryDelay = retryDelay ??
                throw new ArgumentNullException(nameof(retryDelay));
            this.reloadCoordinator.ReloadStarted +=
                OnCoordinatedReloadStarted;
            this.reloadCoordinator.AssemblyLoaded +=
                OnCoordinatedAssemblyLoaded;
            this.reloadCoordinator.ReloadCompleted +=
                OnCoordinatedReloadCompleted;
        }

        public event EventHandler<BackendStoppedEventArgs>? Stopped;
        public event EventHandler? Continued;
        public event EventHandler<BackendThreadEventArgs>? ThreadChanged;
        public event EventHandler<BackendBreakpointChangedEventArgs>?
            BreakpointChanged;
        public event EventHandler? ReloadStarted;
        public event EventHandler? ReloadProgress;
        public event EventHandler? ReloadCompleted;
        public event EventHandler? ReconnectFailed;
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

            disconnectRequested = false;
            this.target = target;
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
            disconnectRequested = true;
            reconnectCancellation?.Cancel();
            reloadCoordinator.Disconnect();
            ReleaseFacade();
        }

        public IReadOnlyList<BackendThread> GetThreads()
        {
            RequireAttached();
            return facade!.GetThreads();
        }

        public IReadOnlyList<BackendStackFrame> GetStackTrace(
            long threadId,
            int startFrame,
            int levels)
        {
            RequireAttached();
            return facade!.GetStackTrace(
                threadId,
                startFrame,
                levels);
        }

        public IReadOnlyList<BackendScope> GetScopes(long frameId)
        {
            RequireAttached();
            return facade!.GetScopes(frameId);
        }

        public IReadOnlyList<BackendVariable> GetVariables(
            long variablesReference)
        {
            RequireAttached();
            return facade!.GetVariables(variablesReference);
        }

        public BackendEvaluationResult Evaluate(
            long frameId,
            string expression,
            BackendEvaluationMode mode)
        {
            RequireAttached();
            return facade!.Evaluate(frameId, expression, mode);
        }

        public BackendBoundBreakpoint BindBreakpoint(
            LogicalBreakpoint breakpoint)
        {
            RequireAttached();
            return facade!.BindBreakpoint(breakpoint);
        }

        public void RemoveBreakpoint(long backendBreakpointId)
        {
            RequireAttached();
            facade!.RemoveBreakpoint(backendBreakpointId);
        }

        public void Continue(long threadId)
        {
            RequireAttached();
            facade!.Continue();
        }

        public void Pause(long threadId)
        {
            RequireAttached();
            facade!.Pause();
        }

        public void StepIn(long threadId)
        {
            RequireAttached();
            facade!.StepIn();
        }

        public void StepOver(long threadId)
        {
            RequireAttached();
            facade!.StepOver();
        }

        public void StepOut(long threadId)
        {
            RequireAttached();
            facade!.StepOut();
        }

        public void ConfigureExceptions(ExceptionBreakMode mode)
        {
            RequireAttached();
            facade!.ConfigureExceptions(mode);
            exceptionMode = mode;
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            disconnectRequested = true;
            reconnectCancellation?.Cancel();
            ReleaseFacade();
            reloadCoordinator.ReloadStarted -=
                OnCoordinatedReloadStarted;
            reloadCoordinator.AssemblyLoaded -=
                OnCoordinatedAssemblyLoaded;
            reloadCoordinator.ReloadCompleted -=
                OnCoordinatedReloadCompleted;
            reloadCoordinator.Dispose();
        }

        private void Subscribe(ISoftDebuggerSessionFacade value)
        {
            value.TargetExited += OnTargetExited;
            value.TargetStarted += OnTargetStarted;
            value.TargetStopped += OnTargetStopped;
            value.ThreadChanged += OnThreadChanged;
            value.AssemblyUnloaded += OnAssemblyUnloaded;
            value.AssemblyLoaded += OnAssemblyLoaded;
            value.BreakpointChanged += OnBreakpointChanged;
        }

        private void Unsubscribe(ISoftDebuggerSessionFacade value)
        {
            value.TargetExited -= OnTargetExited;
            value.TargetStarted -= OnTargetStarted;
            value.TargetStopped -= OnTargetStopped;
            value.ThreadChanged -= OnThreadChanged;
            value.AssemblyUnloaded -= OnAssemblyUnloaded;
            value.AssemblyLoaded -= OnAssemblyLoaded;
            value.BreakpointChanged -= OnBreakpointChanged;
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
            if (!IsAttached)
                return;
            IsAttached = false;
            var value = facade;
            facade = null;
            if (value != null)
                ReleaseSpecificFacade(value);
            if (disposed || disconnectRequested)
                return;

            var reconnectTarget = target;
            if (
                reconnectTarget == null ||
                !processIsAlive(reconnectTarget.ProcessId))
            {
                RaiseTerminatedOnce();
                return;
            }

            reloadCoordinator.OnAssemblyUnloaded();
            reconnectCancellation?.Cancel();
            reconnectCancellation?.Dispose();
            reconnectCancellation = new CancellationTokenSource();
            _ = ReconnectAsync(
                reconnectTarget,
                reconnectCancellation.Token);
        }

        private void OnTargetStopped(
            object? sender,
            BackendStoppedEventArgs arguments) =>
            Stopped?.Invoke(this, arguments);

        private void OnTargetStarted(
            object? sender,
            EventArgs arguments) =>
            Continued?.Invoke(this, EventArgs.Empty);

        private void OnThreadChanged(
            object? sender,
            BackendThreadEventArgs arguments) =>
            ThreadChanged?.Invoke(this, arguments);

        private void OnAssemblyUnloaded(
            object? sender,
            EventArgs arguments) =>
            reloadCoordinator.OnAssemblyUnloaded();

        private void OnAssemblyLoaded(
            object? sender,
            EventArgs arguments) =>
            reloadCoordinator.OnAssemblyLoaded();

        private void OnCoordinatedReloadStarted(
            object? sender,
            EventArgs arguments) =>
            ReloadStarted?.Invoke(this, EventArgs.Empty);

        private void OnCoordinatedAssemblyLoaded(
            object? sender,
            EventArgs arguments) =>
            ReloadProgress?.Invoke(this, EventArgs.Empty);

        private void OnCoordinatedReloadCompleted(
            object? sender,
            EventArgs arguments) =>
            ReloadCompleted?.Invoke(this, EventArgs.Empty);

        private void OnBreakpointChanged(
            object? sender,
            BackendBreakpointChangedEventArgs arguments) =>
            BreakpointChanged?.Invoke(this, arguments);

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

        private async Task ReconnectAsync(
            AttachTarget reconnectTarget,
            CancellationToken cancellationToken)
        {
            try
            {
                var reconnected = await reconnectController.TryReconnect(
                    () =>
                        processIsAlive(reconnectTarget.ProcessId),
                    () => TryCreateReplacement(
                        reconnectTarget,
                        cancellationToken),
                    retryDelay,
                    cancellationToken)
                    .ConfigureAwait(false);
                if (!reconnected && !disconnectRequested && !disposed)
                {
                    ReconnectFailed?.Invoke(this, EventArgs.Empty);
                    RaiseTerminatedOnce();
                }
            }
            catch (OperationCanceledException)
            {
                // Explicit disconnect owns cancellation.
            }
        }

        private bool TryCreateReplacement(
            AttachTarget reconnectTarget,
            CancellationToken cancellationToken)
        {
            if (disconnectRequested || disposed)
                return false;
            var replacement = facadeFactory();
            Subscribe(replacement);
            try
            {
                replacement.ConnectAsync(
                    reconnectTarget.Address,
                    reconnectTarget.Port,
                    1,
                    0,
                    cancellationToken)
                    .GetAwaiter()
                    .GetResult();
                if (disconnectRequested || disposed)
                {
                    ReleaseSpecificFacade(replacement);
                    return false;
                }
                replacement.ConfigureExceptions(exceptionMode);
                facade = replacement;
                IsAttached = true;
                reloadCoordinator.OnAssemblyLoaded();
                return true;
            }
            catch (Exception)
            {
                ReleaseSpecificFacade(replacement);
                return false;
            }
        }

        private void ReleaseSpecificFacade(
            ISoftDebuggerSessionFacade value)
        {
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

        private void RaiseTerminatedOnce()
        {
            if (terminatedRaised)
                return;
            terminatedRaised = true;
            Terminated?.Invoke(this, EventArgs.Empty);
        }

        private static bool DefaultProcessIsAlive(int processId)
        {
            try
            {
                using (var process = Process.GetProcessById(processId))
                    return !process.HasExited;
            }
            catch (
                Exception exception
                ) when (
                    exception is ArgumentException ||
                    exception is InvalidOperationException)
            {
                return false;
            }
        }

    }
}
