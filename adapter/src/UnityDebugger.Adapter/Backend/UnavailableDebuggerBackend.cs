using System;
using System.Collections.Generic;
using System.Threading;

namespace UnityDebugger.Adapter.Backend
{
    internal sealed class UnavailableDebuggerBackend : IDebuggerBackend
    {
#pragma warning disable CS0067
        public event EventHandler<BackendStoppedEventArgs>? Stopped;
        public event EventHandler? Continued;
        public event EventHandler<BackendThreadEventArgs>? ThreadChanged;
        public event EventHandler<BackendModuleChangedEventArgs>?
            ModuleChanged;
        public event EventHandler<BackendBreakpointChangedEventArgs>?
            BreakpointChanged;
        public event EventHandler<BackendOutputEventArgs>? Output;
        public event EventHandler? ReloadStarted;
        public event EventHandler? ReloadProgress;
        public event EventHandler? ReloadCompleted;
        public event EventHandler? ReconnectFailed;
        public event EventHandler? Terminated;
#pragma warning restore CS0067

        public bool IsAttached => false;

        public void Attach(AttachTarget target)
        {
            throw new DebuggerBackendException(
                "Mono debugger backend is not initialized.");
        }

        public void Disconnect()
        {
        }

        public IReadOnlyList<BackendThread> GetThreads() =>
            throw NotAttached();

        public IReadOnlyList<BackendStackFrame> GetStackTrace(
            long threadId,
            int startFrame,
            int levels) => throw NotAttached();

        public IReadOnlyList<BackendScope> GetScopes(
            long frameId,
            BackendEvaluationMode mode,
            int timeoutMilliseconds,
            CancellationToken cancellationToken) =>
            throw NotAttached();

        public IReadOnlyList<BackendVariable> GetVariables(
            long variablesReference,
            BackendEvaluationMode mode,
            int timeoutMilliseconds,
            CancellationToken cancellationToken) => throw NotAttached();

        public BackendEvaluationResult? Evaluate(
            long frameId,
            string expression,
            BackendEvaluationMode mode,
            int timeoutMilliseconds,
            CancellationToken cancellationToken) => throw NotAttached();

        public BackendSetVariableResult? SetVariable(
            long variablesReference,
            string name,
            string expression,
            BackendEvaluationMode mode,
            int timeoutMilliseconds,
            CancellationToken cancellationToken) => throw NotAttached();

        public BackendBoundBreakpoint BindBreakpoint(
            LogicalBreakpoint breakpoint) => throw NotAttached();

        public void RemoveBreakpoint(long backendBreakpointId) =>
            throw NotAttached();

        public IReadOnlyList<BackendStepInTarget> GetStepInTargets(
            long frameId) => throw NotAttached();

        public IReadOnlyList<BackendGotoTarget> GetGotoTargets(
            string sourcePath,
            int line,
            int column) => throw NotAttached();

        public void Continue(long threadId) => throw NotAttached();
        public void Pause(long threadId) => throw NotAttached();
        public void StepIn(long threadId, long? targetId) =>
            throw NotAttached();
        public void StepOver(long threadId) => throw NotAttached();
        public void StepOut(long threadId) => throw NotAttached();
        public void Goto(long threadId, long targetId) =>
            throw NotAttached();

        public void ConfigureExceptions(ExceptionBreakMode mode) =>
            throw NotAttached();

        public void Dispose()
        {
        }

        private static InvalidOperationException NotAttached() =>
            new InvalidOperationException(
                "Mono debugger backend is not attached.");
    }
}
