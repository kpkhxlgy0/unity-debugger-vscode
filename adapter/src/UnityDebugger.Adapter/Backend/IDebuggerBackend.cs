using System;
using System.Collections.Generic;
using System.Threading;

namespace UnityDebugger.Adapter.Backend
{
    internal interface IDebuggerBackend : IDisposable
    {
        event EventHandler<BackendStoppedEventArgs>? Stopped;
        event EventHandler<BackendThreadEventArgs>? ThreadChanged;
        event EventHandler<BackendModuleChangedEventArgs>? ModuleChanged;
        event EventHandler<BackendBreakpointChangedEventArgs>?
            BreakpointChanged;
        event EventHandler<BackendOutputEventArgs>? Output;
        event EventHandler? Terminated;

        bool IsAttached { get; }
        void Attach(AttachTarget target);
        void Disconnect();
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
        void RemoveBreakpoint(long backendBreakpointId);
        void Continue(long threadId);
        void Pause(long threadId);
        void StepIn(long threadId);
        void StepOver(long threadId);
        void StepOut(long threadId);
        void ConfigureExceptions(ExceptionBreakMode mode);
    }
}
