using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace UnityDebugger.Adapter.Backend
{
    internal interface ISoftDebuggerSessionFacade : IDisposable
    {
        event EventHandler? TargetReady;
        event EventHandler? TargetStarted;
        event EventHandler? TargetExited;
        event EventHandler<BackendStoppedEventArgs>? TargetStopped;
        event EventHandler<BackendThreadEventArgs>? ThreadChanged;
        event EventHandler<BackendModuleChangedEventArgs>? ModuleChanged;
        event EventHandler<BackendBreakpointChangedEventArgs>?
            BreakpointChanged;
        event EventHandler<BackendOutputEventArgs>? Output;
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
        void Pause();
        void StepIn();
        void StepOver();
        void StepOut();
        void ConfigureExceptions(ExceptionBreakMode mode);
        IReadOnlyList<BackendThread> GetThreads();
        IReadOnlyList<BackendStackFrame> GetStackTrace(
            long threadId,
            int startFrame,
            int levels);
        IReadOnlyList<BackendScope> GetScopes(
            long frameId,
            BackendEvaluationMode mode,
            int timeoutMilliseconds,
            CancellationToken cancellationToken);
        IReadOnlyList<BackendVariable> GetVariables(
            long variablesReference,
            BackendEvaluationMode mode,
            int timeoutMilliseconds,
            CancellationToken cancellationToken);
        BackendEvaluationResult? Evaluate(
            long frameId,
            string expression,
            BackendEvaluationMode mode,
            int timeoutMilliseconds,
            CancellationToken cancellationToken);
        BackendSetVariableResult? SetVariable(
            long variablesReference,
            string name,
            string expression,
            BackendEvaluationMode mode,
            int timeoutMilliseconds,
            CancellationToken cancellationToken);
        BackendBoundBreakpoint BindBreakpoint(
            LogicalBreakpoint breakpoint);
        BackendBoundBreakpoint BindFunctionBreakpoint(
            LogicalFunctionBreakpoint breakpoint);
        void RemoveBreakpoint(long backendBreakpointId);
        IReadOnlyList<BackendStepInTarget> GetStepInTargets(long frameId);
        IReadOnlyList<BackendGotoTarget> GetGotoTargets(
            string sourcePath,
            int line,
            int column);
        void Goto(long threadId, long targetId);
    }
}
