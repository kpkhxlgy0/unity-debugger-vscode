using System;
using System.Collections.Generic;
using UnityDebugger.Adapter.Backend;

namespace UnityDebugger.Adapter.Tests.Fakes
{
    internal sealed class FakeDebuggerBackend : IDebuggerBackend
    {
        public event EventHandler<BackendStoppedEventArgs>? Stopped;
        public event EventHandler? Continued;
        public event EventHandler<BackendThreadEventArgs>? ThreadChanged;
        public event EventHandler<BackendBreakpointChangedEventArgs>?
            BreakpointChanged;
        public event EventHandler? ReloadStarted;
        public event EventHandler? ReloadCompleted;
        public event EventHandler? Terminated;

        public List<BackendThread> Threads { get; } =
            new List<BackendThread>();
        public List<BackendStackFrame> Frames { get; } =
            new List<BackendStackFrame>();
        public List<BackendScope> Scopes { get; } =
            new List<BackendScope>();
        public List<BackendVariable> Variables { get; } =
            new List<BackendVariable>();
        public Dictionary<long, IReadOnlyList<BackendVariable>>
            VariablesByReference { get; } =
                new Dictionary<long, IReadOnlyList<BackendVariable>>();
        public List<LogicalBreakpoint> Bound { get; } =
            new List<LogicalBreakpoint>();
        public List<long> RemovedBreakpointIds { get; } =
            new List<long>();
        public List<ExceptionBreakMode> ExceptionModes { get; } =
            new List<ExceptionBreakMode>();

        public bool IsAttached { get; private set; }
        public bool BindAsPending { get; set; }
        public Exception? AttachException { get; set; }
        public BackendEvaluationResult EvaluationResult { get; set; } =
            new BackendEvaluationResult("", "", 0);
        public string? LastExpression { get; private set; }
        public int StackTraceCount { get; private set; }
        public int ScopesCount { get; private set; }
        public int VariablesCount { get; private set; }
        public int EvaluateCount { get; private set; }
        public AttachTarget? LastTarget { get; private set; }
        public ExceptionBreakMode? LastExceptionMode { get; private set; }
        public int AttachCount { get; private set; }
        public int DisconnectCount { get; private set; }
        public int DisposeCount { get; private set; }
        public int ContinueCount { get; private set; }
        public int PauseCount { get; private set; }
        public int StepInCount { get; private set; }
        public int StepOverCount { get; private set; }
        public int StepOutCount { get; private set; }
        public int RemoveBreakpointCount { get; private set; }
        public int ConfigureExceptionsCount { get; private set; }
        public long? LastControlThreadId { get; private set; }
        public bool RaiseContinuedSynchronously { get; set; }
        public int SynchronousStoppedEventCount { get; set; }
        public Exception? ControlException { get; set; }

        public void Attach(AttachTarget target)
        {
            AttachCount++;
            LastTarget = target;
            if (AttachException != null)
                throw AttachException;
            IsAttached = true;
        }

        public void Disconnect()
        {
            DisconnectCount++;
            IsAttached = false;
        }

        public IReadOnlyList<BackendThread> GetThreads() => Threads;

        public IReadOnlyList<BackendStackFrame> GetStackTrace(
            long threadId,
            int startFrame,
            int levels)
        {
            StackTraceCount++;
            return Frames;
        }

        public IReadOnlyList<BackendScope> GetScopes(long frameId)
        {
            ScopesCount++;
            return Scopes;
        }

        public IReadOnlyList<BackendVariable> GetVariables(
            long variablesReference)
        {
            VariablesCount++;
            return VariablesByReference.TryGetValue(
                variablesReference,
                out var values)
                    ? values
                    : Variables;
        }

        public BackendEvaluationResult Evaluate(
            long frameId,
            string expression)
        {
            EvaluateCount++;
            LastExpression = expression;
            return EvaluationResult;
        }

        public BackendBoundBreakpoint BindBreakpoint(
            LogicalBreakpoint breakpoint)
        {
            Bound.Add(breakpoint);
            return new BackendBoundBreakpoint(
                Bound.Count,
                !BindAsPending,
                breakpoint.Line,
                BindAsPending ? "Symbols are not loaded." : null);
        }

        public void RemoveBreakpoint(long backendBreakpointId)
        {
            RemoveBreakpointCount++;
            RemovedBreakpointIds.Add(backendBreakpointId);
        }

        public void Continue(long threadId)
        {
            ContinueCount++;
            LastControlThreadId = threadId;
            if (ControlException != null)
                throw ControlException;
            if (RaiseContinuedSynchronously)
                RaiseContinued();
        }

        public void Pause(long threadId)
        {
            PauseCount++;
            LastControlThreadId = threadId;
            if (ControlException != null)
                throw ControlException;
            for (
                var index = 0;
                index < SynchronousStoppedEventCount;
                index++)
            {
                RaiseStopped(
                    new BackendStoppedEventArgs(
                        BackendStopReason.Pause,
                        threadId,
                        null));
            }
        }

        public void StepIn(long threadId)
        {
            StepInCount++;
            LastControlThreadId = threadId;
            if (ControlException != null)
                throw ControlException;
            if (RaiseContinuedSynchronously)
                RaiseContinued();
        }

        public void StepOver(long threadId)
        {
            StepOverCount++;
            LastControlThreadId = threadId;
            if (ControlException != null)
                throw ControlException;
            if (RaiseContinuedSynchronously)
                RaiseContinued();
        }

        public void StepOut(long threadId)
        {
            StepOutCount++;
            LastControlThreadId = threadId;
            if (ControlException != null)
                throw ControlException;
            if (RaiseContinuedSynchronously)
                RaiseContinued();
        }

        public void ConfigureExceptions(ExceptionBreakMode mode)
        {
            ConfigureExceptionsCount++;
            LastExceptionMode = mode;
            ExceptionModes.Add(mode);
        }

        public void Dispose()
        {
            DisposeCount++;
            IsAttached = false;
        }

        public void RaiseStopped(BackendStoppedEventArgs arguments) =>
            Stopped?.Invoke(this, arguments);

        public void RaiseReloadStarted() =>
            ReloadStarted?.Invoke(this, EventArgs.Empty);

        public void RaiseReloadCompleted() =>
            ReloadCompleted?.Invoke(this, EventArgs.Empty);

        public void RaiseBreakpointChanged(
            BackendBreakpointChangedEventArgs arguments) =>
            BreakpointChanged?.Invoke(this, arguments);

        public void RaiseTerminated() =>
            Terminated?.Invoke(this, EventArgs.Empty);

        public void RaiseContinued() =>
            Continued?.Invoke(this, EventArgs.Empty);

        public void RaiseThreadChanged(BackendThreadEventArgs arguments) =>
            ThreadChanged?.Invoke(this, arguments);
    }
}
