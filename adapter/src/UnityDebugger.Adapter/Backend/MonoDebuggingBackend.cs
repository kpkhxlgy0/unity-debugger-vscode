using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using Mono.Debugger.Soft;

namespace UnityDebugger.Adapter.Backend
{
    internal sealed class MonoDebuggingBackend : IDebuggerBackend
    {
        private const int MaxConnectionAttempts = 10;
        private const int ConnectionAttemptIntervalMilliseconds = 500;
        private readonly Func<ISoftDebuggerSessionFacade> facadeFactory;
        private ISoftDebuggerSessionFacade? facade;
        private bool disposed;
        private bool terminatedRaised;

        public MonoDebuggingBackend(
            Func<ISoftDebuggerSessionFacade> facadeFactory)
        {
            this.facadeFactory = facadeFactory ??
                throw new ArgumentNullException(nameof(facadeFactory));
        }

        public event EventHandler<BackendStoppedEventArgs>? Stopped;
        public event EventHandler<BackendThreadEventArgs>? ThreadChanged;
        public event EventHandler<BackendModuleChangedEventArgs>?
            ModuleChanged;
        public event EventHandler<BackendBreakpointChangedEventArgs>?
            BreakpointChanged;
        public event EventHandler<BackendOutputEventArgs>? Output;
        public event EventHandler? Terminated;

        public bool IsAttached { get; private set; }

        public void Attach(AttachTarget target)
        {
            ThrowIfDisposed();
            if (facade != null || IsAttached)
            {
                throw new InvalidOperationException(
                    "The debugger backend is already attached.");
            }
            if (!IPAddress.IsLoopback(target.Address))
            {
                throw new DebuggerBackendException(
                    "Only loopback Editor targets are allowed.");
            }

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
            catch (VMMismatchException exception)
            {
                ReleaseFacade();
                throw new DebuggerBackendException(
                    "Editor uses an incompatible Mono Soft Debugger " +
                    "protocol.",
                    exception);
            }
            catch (Exception exception)
            {
                ReleaseFacade();
                throw new DebuggerBackendException(
                    "Could not connect to the local Editor debugger.",
                    exception);
            }
        }

        public void Disconnect()
        {
            ReleaseFacade();
        }

        public IReadOnlyList<BackendThread> GetThreads()
        {
            return RequireAttached().GetThreads();
        }

        public IReadOnlyList<BackendStackFrame> GetStackTrace(
            long threadId,
            int startFrame,
            int levels)
        {
            return RequireAttached().GetStackTrace(
                threadId,
                startFrame,
                levels);
        }

        public IReadOnlyList<BackendScope> GetScopes(
            long frameId,
            BackendEvaluationMode mode,
            int timeoutMilliseconds,
            CancellationToken cancellationToken)
        {
            return RequireAttached().GetScopes(
                frameId,
                mode,
                timeoutMilliseconds,
                cancellationToken);
        }

        public IReadOnlyList<BackendVariable> GetVariables(
            long variablesReference,
            BackendEvaluationMode mode,
            int timeoutMilliseconds,
            CancellationToken cancellationToken)
        {
            return RequireAttached().GetVariables(
                variablesReference,
                mode,
                timeoutMilliseconds,
                cancellationToken);
        }

        public BackendEvaluationResult? Evaluate(
            long frameId,
            string expression,
            BackendEvaluationMode mode,
            int timeoutMilliseconds,
            CancellationToken cancellationToken)
        {
            return RequireAttached().Evaluate(
                frameId,
                expression,
                mode,
                timeoutMilliseconds,
                cancellationToken);
        }

        public BackendSetVariableResult? SetVariable(
            long variablesReference,
            string name,
            string expression,
            BackendEvaluationMode mode,
            int timeoutMilliseconds,
            CancellationToken cancellationToken)
        {
            return RequireAttached().SetVariable(
                variablesReference,
                name,
                expression,
                mode,
                timeoutMilliseconds,
                cancellationToken);
        }

        public BackendBoundBreakpoint BindBreakpoint(
            LogicalBreakpoint breakpoint)
        {
            return RequireAttached().BindBreakpoint(breakpoint);
        }

        public BackendBoundBreakpoint BindFunctionBreakpoint(
            LogicalFunctionBreakpoint breakpoint)
        {
            return RequireAttached().BindFunctionBreakpoint(breakpoint);
        }

        public void RemoveBreakpoint(long backendBreakpointId)
        {
            RequireAttached().RemoveBreakpoint(backendBreakpointId);
        }

        public IReadOnlyList<BackendStepInTarget> GetStepInTargets(
            long frameId)
        {
            return RequireAttached().GetStepInTargets(frameId);
        }

        public IReadOnlyList<BackendGotoTarget> GetGotoTargets(
            string sourcePath,
            int line,
            int column)
        {
            return RequireAttached().GetGotoTargets(
                sourcePath,
                line,
                column);
        }

        public void Continue(long threadId)
        {
            RequireAttached().Continue();
        }

        public void Pause(long threadId)
        {
            RequireAttached().Pause();
        }

        public void StepIn(long threadId, long? targetId)
        {
            RequireAttached().StepIn();
        }

        public void StepOver(long threadId)
        {
            RequireAttached().StepOver();
        }

        public void StepOut(long threadId)
        {
            RequireAttached().StepOut();
        }

        public void Goto(long threadId, long targetId)
        {
            RequireAttached().Goto(threadId, targetId);
        }

        public void ConfigureExceptions(ExceptionBreakMode mode)
        {
            RequireAttached().ConfigureExceptions(mode);
        }

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
            value.ModuleChanged += OnModuleChanged;
            value.BreakpointChanged += OnBreakpointChanged;
            value.Output += OnOutput;
        }

        private void Unsubscribe(ISoftDebuggerSessionFacade value)
        {
            value.TargetExited -= OnTargetExited;
            value.TargetStopped -= OnTargetStopped;
            value.ThreadChanged -= OnThreadChanged;
            value.ModuleChanged -= OnModuleChanged;
            value.BreakpointChanged -= OnBreakpointChanged;
            value.Output -= OnOutput;
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
            if (facade == null)
                return;
            ReleaseFacade();
            RaiseTerminatedOnce();
        }

        private void OnTargetStopped(
            object? sender,
            BackendStoppedEventArgs arguments)
        {
            Stopped?.Invoke(this, arguments);
        }

        private void OnThreadChanged(
            object? sender,
            BackendThreadEventArgs arguments)
        {
            ThreadChanged?.Invoke(this, arguments);
        }

        private void OnModuleChanged(
            object? sender,
            BackendModuleChangedEventArgs arguments)
        {
            ModuleChanged?.Invoke(this, arguments);
        }

        private void OnBreakpointChanged(
            object? sender,
            BackendBreakpointChangedEventArgs arguments)
        {
            BreakpointChanged?.Invoke(this, arguments);
        }

        private void OnOutput(
            object? sender,
            BackendOutputEventArgs arguments)
        {
            Output?.Invoke(this, arguments);
        }

        private ISoftDebuggerSessionFacade RequireAttached()
        {
            ThrowIfDisposed();
            if (!IsAttached || facade == null)
            {
                throw new InvalidOperationException(
                    "The debugger backend is not attached.");
            }
            return facade;
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(
                    nameof(MonoDebuggingBackend));
            }
        }

        private void RaiseTerminatedOnce()
        {
            if (terminatedRaised)
                return;
            terminatedRaised = true;
            Terminated?.Invoke(this, EventArgs.Empty);
        }
    }
}
