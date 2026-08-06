using System;
using System.Collections.Generic;
using System.Threading;
using Mono.Debugger.Soft;
using UnityDebugger.Adapter.Backend;
using UnityDebugger.Adapter.Engine.Breakpoints;
using UnityDebugger.Adapter.Engine.Control;
using UnityDebugger.Adapter.Engine.Evaluation;
using UnityDebugger.Adapter.Engine.Events;
using UnityDebugger.Adapter.Engine.Mono;
using UnityDebugger.Adapter.Engine.Source;
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
        private StepTargetManager? stepTargetManager;
        private GotoManager? gotoManager;
        private SuspendedState? suspendedState;
        private EvaluationService? evaluationService;
        private EngineSourceMapManager? sourceMapManager;
        private EngineBreakpointManager? engineBreakpointManager;
        private EngineExceptionManager? engineExceptionManager;
        private int terminated;
        private bool disposed;

        public UnityDebuggerEngine(
            Func<AttachTarget, IMonoEngineConnection> connectionFactory)
        {
            this.connectionFactory = connectionFactory ??
                throw new ArgumentNullException(nameof(connectionFactory));
        }

        public UnityDebuggerEngine()
            : this(target => new MonoEngineConnection(target))
        {
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
                    var createdEvaluationService =
                        new EvaluationService(createdState);
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
                    StepTargetManager? createdStepTargetManager = null;
                    if (createdConnection is IStepTargetRuntime stepTargets)
                    {
                        createdStepTargetManager = new StepTargetManager(
                            createdState,
                            stepTargets);
                    }
                    GotoManager? createdGotoManager = null;
                    if (createdConnection is IGotoRuntime gotoRuntime)
                    {
                        createdGotoManager = new GotoManager(
                            createdState,
                            gotoRuntime);
                    }
                    var createdSourceManager =
                        new EngineSourceMapManager();
                    EngineBreakpointManager? createdBreakpointManager = null;
                    if (createdConnection is IEngineBreakpointRuntime runtime)
                    {
                        createdBreakpointManager =
                            new EngineBreakpointManager(
                                createdSourceManager,
                                runtime,
                                createdEvaluationService);
                        createdBreakpointManager.BreakpointChanged +=
                            OnEngineBreakpointChanged;
                    }
                    createdSourceManager.ModuleChanged +=
                        OnEngineModuleChanged;
                    EngineExceptionManager? createdExceptionManager = null;
                    if (createdConnection is IEngineExceptionRuntime exceptionRuntime)
                    {
                        createdExceptionManager =
                            new EngineExceptionManager(exceptionRuntime);
                    }

                    connection = createdConnection;
                    suspendedState = createdState;
                    evaluationService = createdEvaluationService;
                    sourceMapManager = createdSourceManager;
                    engineBreakpointManager = createdBreakpointManager;
                    engineExceptionManager = createdExceptionManager;
                    dispatcher = createdDispatcher;
                    stepRuntime = createdStepRuntime;
                    stepManager = createdStepManager;
                    stepTargetManager = createdStepTargetManager;
                    gotoManager = createdGotoManager;
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
                stepTargetManager = null;
                gotoManager = null;
                suspendedState = null;
                evaluationService = null;
                if (engineBreakpointManager != null)
                {
                    engineBreakpointManager.BreakpointChanged -=
                        OnEngineBreakpointChanged;
                }
                if (sourceMapManager != null)
                    sourceMapManager.ModuleChanged -= OnEngineModuleChanged;
                sourceMapManager = null;
                engineBreakpointManager = null;
                engineExceptionManager = null;
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
            int levels)
        {
            var value = RequireConnection();
            var frames = value.GetStackTrace(
                threadId,
                startFrame,
                levels);
            var evaluationConnection = value as IMonoEvaluationConnection;
            var mapped = new List<BackendStackFrame>(frames.Count);
            foreach (var frame in frames)
            {
                var mappedFrame = frame;
                if (
                    evaluationConnection != null &&
                    evaluationConnection.TryGetFrameEvaluation(
                    frame.Id,
                    out var environment,
                    out var unityContext))
                {
                    mappedFrame = RequireEvaluationService().RegisterFrame(
                        frame,
                        environment,
                        unityContext);
                }
                mapped.Add(mappedFrame);
                stepTargetManager?.RegisterFrame(
                    mappedFrame.Id,
                    frame.Id);
            }
            return mapped;
        }

        public IReadOnlyList<BackendScope> GetScopes(
            long frameId,
            BackendEvaluationMode mode,
            int timeoutMilliseconds,
            CancellationToken cancellationToken) =>
            RequireEvaluationService().GetScopes(
                frameId,
                mode,
                timeoutMilliseconds,
                cancellationToken);

        public IReadOnlyList<BackendVariable> GetVariables(
            long variablesReference,
            BackendEvaluationMode mode,
            int timeoutMilliseconds,
            CancellationToken cancellationToken) =>
            RequireEvaluationService().GetVariables(
                variablesReference,
                mode,
                timeoutMilliseconds,
                cancellationToken);

        public BackendEvaluationResult? Evaluate(
            long frameId,
            string expression,
            BackendEvaluationMode mode,
            int timeoutMilliseconds,
            CancellationToken cancellationToken) =>
            RequireEvaluationService().Evaluate(
                frameId,
                expression,
                mode,
                timeoutMilliseconds,
                cancellationToken);

        public BackendSetVariableResult? SetVariable(
            long variablesReference,
            string name,
            string expression,
            BackendEvaluationMode mode,
            int timeoutMilliseconds,
            CancellationToken cancellationToken) =>
            RequireEvaluationService().SetVariable(
                variablesReference,
                name,
                expression,
                mode,
                timeoutMilliseconds,
                cancellationToken);

        public BackendBoundBreakpoint BindBreakpoint(
            LogicalBreakpoint breakpoint)
        {
            var manager = RequireBreakpointManager();
            if (connection is IMonoSourceConnection sourceConnection)
            {
                foreach (var type in sourceConnection.GetSourceTypes(
                    breakpoint.SourcePath))
                {
                    manager.ProcessTypeLoaded(type);
                }
            }
            return manager.ToBackendBreakpoint(
                manager.RequestSourceBreakpoint(breakpoint));
        }

        public BackendBoundBreakpoint BindFunctionBreakpoint(
            LogicalFunctionBreakpoint breakpoint)
        {
            throw new DebuggerBackendException(
                "Function breakpoints require the mature debugger backend.");
        }

        public void RemoveBreakpoint(long backendBreakpointId) =>
            RequireBreakpointManager().RemovePendingBreakpoint(
                backendBreakpointId);

        public IReadOnlyList<BackendStepInTarget> GetStepInTargets(
            long frameId) =>
            stepTargetManager?.GetTargets(frameId) ??
            Array.Empty<BackendStepInTarget>();

        public IReadOnlyList<BackendGotoTarget> GetGotoTargets(
            string sourcePath,
            int line,
            int column) =>
            gotoManager?.GetTargets(sourcePath, line, column) ??
            Array.Empty<BackendGotoTarget>();

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

        public void StepIn(long threadId, long? targetId)
        {
            if (
                targetId.HasValue &&
                stepTargetManager != null &&
                stepTargetManager.TryStepIn(threadId, targetId.Value))
            {
                return;
            }
            RequireStepManager().RequestStep(
                threadId,
                EngineStepDepth.Into);
        }

        public void StepOver(long threadId) =>
            RequireStepManager().RequestStep(
                threadId,
                EngineStepDepth.Over);

        public void StepOut(long threadId) =>
            RequireStepManager().RequestStep(
                threadId,
                EngineStepDepth.Out);

        public void Goto(long threadId, long targetId)
        {
            if (
                gotoManager != null &&
                gotoManager.TryGoto(threadId, targetId))
            {
                Stopped?.Invoke(
                    this,
                    new BackendStoppedEventArgs(
                        BackendStopReason.Goto,
                        threadId,
                        null));
            }
        }

        public void ConfigureExceptions(ExceptionBreakMode mode)
        {
            var manager = engineExceptionManager;
            if (manager == null)
                throw MigrationIncomplete();
            manager.Configure(mode);
        }

        public void Process(EngineEvent value)
        {
            switch (value.Kind)
            {
                case EngineEventKind.Step:
                    RequireStepManager().ProcessStepEvent(value.ThreadId);
                    break;
                case EngineEventKind.Breakpoint:
                    ProcessBreakpointEvent(value);
                    break;
                case EngineEventKind.Exception:
                    ProcessExceptionEvent(value);
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
                case EngineEventKind.TypeLoaded:
                    if (value.Payload is TypeLoadEvent typeLoaded)
                    {
                        engineBreakpointManager?.ProcessTypeLoaded(
                            new MonoRuntimeType(typeLoaded.Type));
                    }
                    break;
                case EngineEventKind.DomainUnloaded:
                    ProcessDomainUnload(value.Payload);
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
            IReadOnlyList<long>? breakpointIds = null,
            BackendExceptionInfo? exceptionInfo = null)
        {
            SuspendedState.Reset();
            Stopped?.Invoke(
                this,
                new BackendStoppedEventArgs(
                    reason,
                    threadId,
                    null,
                    breakpointIds ?? Array.Empty<long>(),
                    exceptionInfo));
        }

        private void ProcessBreakpointEvent(EngineEvent value)
        {
            if (
                engineBreakpointManager != null &&
                value.Payload is BreakpointEvent breakpointEvent &&
                breakpointEvent.Request != null)
            {
                IFrameEvaluationEnvironment? frame = null;
                if (connection is IMonoBreakpointEvaluationConnection evaluation)
                {
                    evaluation.TryGetTopFrameEvaluation(
                        value.ThreadId,
                        out frame);
                }
                var result = engineBreakpointManager.ProcessBreakpointHit(
                    new RuntimeBreakpointHit(
                        breakpointEvent.Request,
                        breakpointEvent.Method.Locations.Count > 0,
                        frame));
                if (result.Action == BreakpointHitAction.Resume)
                {
                    RequireStepRuntime().Resume();
                    return;
                }
                if (!string.IsNullOrEmpty(result.Output))
                {
                    Output?.Invoke(
                        this,
                        new BackendOutputEventArgs(
                            "console",
                            result.Output!));
                }
                if (result.Action == BreakpointHitAction.LogPoint)
                {
                    RequireStepRuntime().Resume();
                    return;
                }
                RaiseStopped(
                    BackendStopReason.Breakpoint,
                    value.ThreadId,
                    result.BreakpointIds);
                return;
            }

            RaiseStopped(
                BackendStopReason.Breakpoint,
                value.ThreadId,
                value.Payload as IReadOnlyList<long>);
        }

        private void ProcessExceptionEvent(EngineEvent value)
        {
            if (
                engineExceptionManager == null ||
                !(value.Payload is ExceptionEvent exceptionEvent) ||
                exceptionEvent.Request == null)
            {
                RaiseStopped(
                    BackendStopReason.Exception,
                    value.ThreadId);
                return;
            }

            var typeName = exceptionEvent.Exception.Type.FullName;
            var result = engineExceptionManager.Process(
                new RuntimeExceptionHit(
                    exceptionEvent.Request,
                    typeName,
                    typeName,
                    null));
            if (!result.ShouldStop)
            {
                RequireStepRuntime().Resume();
                return;
            }
            RaiseStopped(
                BackendStopReason.Exception,
                value.ThreadId,
                exceptionInfo: result.ExceptionInfo);
        }

        private void ProcessDomainUnload(object? payload)
        {
            if (
                !(payload is AppDomainUnloadEvent unloaded) ||
                sourceMapManager == null ||
                engineBreakpointManager == null ||
                !sourceMapManager.TryGetDomain(
                    unloaded.Domain,
                    out var domain))
            {
                return;
            }
            engineBreakpointManager.UnbindDomain(domain);
        }

        private void OnEngineBreakpointChanged(
            object? sender,
            BackendBreakpointChangedEventArgs arguments) =>
            BreakpointChanged?.Invoke(this, arguments);

        private void OnEngineModuleChanged(
            object? sender,
            BackendModuleChangedEventArgs arguments) =>
            ModuleChanged?.Invoke(this, arguments);

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

        private EvaluationService RequireEvaluationService() =>
            evaluationService ??
            throw new InvalidOperationException(
                "The debugger engine is not attached.");

        private EngineBreakpointManager RequireBreakpointManager() =>
            engineBreakpointManager ??
            throw MigrationIncomplete();

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
