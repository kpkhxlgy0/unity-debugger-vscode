using System;
using System.Collections.Generic;
using System.Threading;
using UnityDebugger.Adapter.Backend;
using UnityDebugger.Adapter.Engine.Control;
using UnityDebugger.Adapter.Engine.Events;
using UnityDebugger.Adapter.Engine.Mono;
using UnityDebugger.Adapter.Engine.State;

namespace UnityDebugger.Adapter.Engine
{
    internal sealed class UnityDebuggerEngine :
        IDebuggerBackend,
        IEngineEventReceiver
    {
        private readonly Func<AttachTarget, IMonoEngineConnection>
            connectionFactory;
        private readonly object lifecycleLock = new object();
        private IMonoEngineConnection? connection;
        private EngineEventDispatcher? dispatcher;
        private IStepRuntime? stepRuntime;
        private StepManager? stepManager;
        private SuspendedState? suspendedState;
        private int terminated;
        private bool disposed;

        public UnityDebuggerEngine(
            Func<AttachTarget, IMonoEngineConnection> connectionFactory)
        {
            this.connectionFactory = connectionFactory ??
                throw new ArgumentNullException(nameof(connectionFactory));
        }

        public event EventHandler<BackendStoppedEventArgs>? Stopped;
        public event EventHandler<BackendThreadEventArgs>? ThreadChanged;
#pragma warning disable CS0067
        public event EventHandler<BackendModuleChangedEventArgs>?
            ModuleChanged;
        public event EventHandler<BackendBreakpointChangedEventArgs>?
            BreakpointChanged;
        public event EventHandler<BackendOutputEventArgs>? Output;
#pragma warning restore CS0067
        public event EventHandler? Terminated;

        public bool IsAttached { get; private set; }

        internal SuspendedState SuspendedState =>
            suspendedState ??
            throw new InvalidOperationException(
                "The debugger engine is not attached.");

        public void Attach(AttachTarget target)
        {
            lock (lifecycleLock)
            {
                ThrowIfDisposed();
                if (connection != null)
                {
                    throw new InvalidOperationException(
                        "The debugger engine is already attached.");
                }

                var createdConnection = connectionFactory(target);
                try
                {
                    createdConnection.Connect();
                    var createdState = new SuspendedState();
                    var eventSource =
                        createdConnection.CreateEventSource();
                    var createdDispatcher =
                        new EngineEventDispatcher(eventSource, this);
                    var createdStepRuntime =
                        createdConnection.CreateStepRuntime(
                            createdDispatcher);
                    var createdStepManager = new StepManager(
                        createdStepRuntime,
                        threadId => RaiseStopped(
                            BackendStopReason.Step,
                            threadId));

                    connection = createdConnection;
                    suspendedState = createdState;
                    dispatcher = createdDispatcher;
                    stepRuntime = createdStepRuntime;
                    stepManager = createdStepManager;
                    IsAttached = true;
                    Interlocked.Exchange(ref terminated, 0);
                    createdDispatcher.Start();
                }
                catch
                {
                    createdConnection.Dispose();
                    throw;
                }
            }
        }

        public void Disconnect()
        {
            IMonoEngineConnection? value;
            EngineEventDispatcher? eventDispatcher;
            lock (lifecycleLock)
            {
                value = connection;
                eventDispatcher = dispatcher;
                connection = null;
                dispatcher = null;
                stepRuntime = null;
                stepManager = null;
                suspendedState = null;
                IsAttached = false;
            }
            if (value == null)
                return;
            eventDispatcher?.Stop();
            try
            {
                value.Disconnect();
            }
            finally
            {
                value.Dispose();
            }
        }

        public IReadOnlyList<BackendThread> GetThreads() =>
            RequireConnection().GetThreads();

        public IReadOnlyList<BackendStackFrame> GetStackTrace(
            long threadId,
            int startFrame,
            int levels) =>
            RequireConnection().GetStackTrace(
                threadId,
                startFrame,
                levels);

        public IReadOnlyList<BackendScope> GetScopes(
            long frameId,
            BackendEvaluationMode mode) => throw MigrationIncomplete();

        public IReadOnlyList<BackendVariable> GetVariables(
            long variablesReference,
            BackendEvaluationMode mode) => throw MigrationIncomplete();

        public BackendEvaluationResult Evaluate(
            long frameId,
            string expression,
            BackendEvaluationMode mode) => throw MigrationIncomplete();

        public BackendBoundBreakpoint BindBreakpoint(
            LogicalBreakpoint breakpoint) => throw MigrationIncomplete();

        public void RemoveBreakpoint(long backendBreakpointId) =>
            throw MigrationIncomplete();

        public void Continue(long threadId)
        {
            RequireStepManager().CancelStep();
            RequireStepRuntime().Resume();
        }

        public void Pause(long threadId)
        {
            var value = RequireConnection();
            value.Suspend();
            foreach (var thread in value.GetThreads())
            {
                SuspendedState.Reset();
                Stopped?.Invoke(
                    this,
                    new BackendStoppedEventArgs(
                        BackendStopReason.Pause,
                        thread.Id,
                        null));
            }
        }

        public void StepIn(long threadId) =>
            RequireStepManager().RequestStep(
                threadId,
                EngineStepDepth.Into);

        public void StepOver(long threadId) =>
            RequireStepManager().RequestStep(
                threadId,
                EngineStepDepth.Over);

        public void StepOut(long threadId) =>
            RequireStepManager().RequestStep(
                threadId,
                EngineStepDepth.Out);

        public void ConfigureExceptions(ExceptionBreakMode mode) =>
            throw MigrationIncomplete();

        public void Process(EngineEvent value)
        {
            switch (value.Kind)
            {
                case EngineEventKind.Step:
                    RequireStepManager().ProcessStepEvent(value.ThreadId);
                    break;
                case EngineEventKind.Breakpoint:
                    RaiseStopped(
                        BackendStopReason.Breakpoint,
                        value.ThreadId,
                        value.Payload as IReadOnlyList<long>);
                    break;
                case EngineEventKind.Exception:
                    RaiseStopped(
                        BackendStopReason.Exception,
                        value.ThreadId);
                    break;
                case EngineEventKind.UserBreak:
                    RaiseStopped(
                        BackendStopReason.Pause,
                        value.ThreadId);
                    break;
                case EngineEventKind.ThreadStarted:
                    ThreadChanged?.Invoke(
                        this,
                        new BackendThreadEventArgs(
                            value.ThreadId,
                            true));
                    break;
                case EngineEventKind.ThreadExited:
                    ThreadChanged?.Invoke(
                        this,
                        new BackendThreadEventArgs(
                            value.ThreadId,
                            false));
                    break;
                case EngineEventKind.VmDied:
                case EngineEventKind.VmDisconnected:
                    Terminate(null);
                    break;
            }
        }

        public void Terminate(Exception? exception)
        {
            IsAttached = false;
            if (Interlocked.Exchange(ref terminated, 1) == 0)
                Terminated?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            Disconnect();
        }

        private void RaiseStopped(
            BackendStopReason reason,
            long threadId,
            IReadOnlyList<long>? breakpointIds = null)
        {
            SuspendedState.Reset();
            Stopped?.Invoke(
                this,
                new BackendStoppedEventArgs(
                    reason,
                    threadId,
                    null,
                    breakpointIds ?? Array.Empty<long>()));
        }

        private IMonoEngineConnection RequireConnection() =>
            connection ??
            throw new InvalidOperationException(
                "The debugger engine is not attached.");

        private IStepRuntime RequireStepRuntime() =>
            stepRuntime ??
            throw new InvalidOperationException(
                "The debugger engine is not attached.");

        private StepManager RequireStepManager() =>
            stepManager ??
            throw new InvalidOperationException(
                "The debugger engine is not attached.");

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(UnityDebuggerEngine));
        }

        private static NotSupportedException MigrationIncomplete() =>
            new NotSupportedException(
                "This debugger engine feature is not migrated yet.");
    }
}
