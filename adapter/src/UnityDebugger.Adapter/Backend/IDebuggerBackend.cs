using System;
using System.Collections.Generic;

namespace UnityDebugger.Adapter.Backend
{
    internal interface IDebuggerBackend : IDisposable
    {
        event EventHandler<BackendStoppedEventArgs>? Stopped;
        event EventHandler? Continued;
        event EventHandler<BackendThreadEventArgs>? ThreadChanged;
        event EventHandler<BackendBreakpointChangedEventArgs>?
            BreakpointChanged;
        event EventHandler? ReloadStarted;
        event EventHandler? ReloadProgress;
        event EventHandler? ReloadCompleted;
        event EventHandler? ReconnectFailed;
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
            BackendEvaluationMode mode);
        IReadOnlyList<BackendVariable> GetVariables(
            long variablesReference,
            BackendEvaluationMode mode);
        BackendEvaluationResult Evaluate(
            long frameId,
            string expression,
            BackendEvaluationMode mode);
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
