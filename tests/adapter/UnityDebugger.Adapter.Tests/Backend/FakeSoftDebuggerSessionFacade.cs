using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using UnityDebugger.Adapter.Backend;

namespace UnityDebugger.Adapter.Tests.Backend
{
    internal sealed class FakeSoftDebuggerSessionFacade :
        ISoftDebuggerSessionFacade
    {
        private long nextBreakpointId = 1;
        private readonly HashSet<long> breakpointIds =
            new HashSet<long>();

        public event EventHandler? TargetReady;
        public event EventHandler? TargetStarted;
        public event EventHandler? TargetExited;
        public event EventHandler<BackendStoppedEventArgs>? TargetStopped;
        public event EventHandler<BackendThreadEventArgs>? ThreadChanged;
        public event EventHandler<BackendModuleChangedEventArgs>?
            ModuleChanged;
        public event EventHandler<BackendBreakpointChangedEventArgs>?
            BreakpointChanged;
        public event EventHandler<BackendOutputEventArgs>? Output;
        public event EventHandler? AssemblyUnloaded;
        public event EventHandler? AssemblyLoaded;

        public bool IsRunning { get; set; } = true;
        public bool HasExited { get; set; }
        public IPAddress? ConnectAddress { get; private set; }
        public int ConnectPort { get; private set; }
        public int MaxConnectionAttempts { get; private set; }
        public int ConnectionAttemptIntervalMilliseconds {
            get;
            private set;
        }
        public Exception? ConnectException { get; set; }
        public int ConnectCount { get; private set; }
        public int DetachCount { get; private set; }
        public int DisposeCount { get; private set; }
        public int ContinueCount { get; private set; }
        public int PauseCount { get; private set; }
        public int StepInCount { get; private set; }
        public int StepOverCount { get; private set; }
        public int StepOutCount { get; private set; }
        public int GotoCount { get; private set; }
        public int GetScopesCount { get; private set; }
        public int GetVariablesCount { get; private set; }
        public int EvaluateCount { get; private set; }
        public int SetVariableCount { get; private set; }
        public ExceptionBreakMode? ExceptionMode { get; private set; }
        public List<LogicalBreakpoint> Bound { get; } =
            new List<LogicalBreakpoint>();
        public List<LogicalFunctionBreakpoint> FunctionBound { get; } =
            new List<LogicalFunctionBreakpoint>();
        public List<BackendThread> Threads { get; } =
            new List<BackendThread>();
        public List<BackendStackFrame> Frames { get; } =
            new List<BackendStackFrame>();
        public List<BackendScope> Scopes { get; } =
            new List<BackendScope>();
        public List<BackendVariable> Variables { get; } =
            new List<BackendVariable>();
        public List<BackendStepInTarget> StepInTargets { get; } =
            new List<BackendStepInTarget>();
        public List<BackendGotoTarget> GotoTargets { get; } =
            new List<BackendGotoTarget>();
        public BackendEvaluationResult? EvaluationResult { get; set; }
        public BackendSetVariableResult? SetVariableResult { get; set; }

        public Task ConnectAsync(
            IPAddress address,
            int port,
            int maxConnectionAttempts,
            int connectionAttemptIntervalMilliseconds,
            CancellationToken cancellationToken)
        {
            ConnectCount++;
            ConnectAddress = address;
            ConnectPort = port;
            MaxConnectionAttempts = maxConnectionAttempts;
            ConnectionAttemptIntervalMilliseconds =
                connectionAttemptIntervalMilliseconds;
            if (ConnectException != null)
                return Task.FromException(ConnectException);
            TargetReady?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        }

        public void Detach()
        {
            DetachCount++;
        }

        public void Continue()
        {
            ContinueCount++;
        }

        public void Pause()
        {
            PauseCount++;
        }

        public void StepIn()
        {
            StepInCount++;
        }

        public void StepOver()
        {
            StepOverCount++;
        }

        public void StepOut()
        {
            StepOutCount++;
        }

        public void ConfigureExceptions(ExceptionBreakMode mode)
        {
            ExceptionMode = mode;
        }

        public IReadOnlyList<BackendThread> GetThreads() => Threads;

        public IReadOnlyList<BackendStackFrame> GetStackTrace(
            long threadId,
            int startFrame,
            int levels) => Frames;

        public IReadOnlyList<BackendScope> GetScopes(
            long frameId,
            int timeoutMilliseconds,
            CancellationToken cancellationToken)
        {
            GetScopesCount++;
            return Scopes;
        }

        public IReadOnlyList<BackendVariable> GetVariables(
            long variablesReference,
            int timeoutMilliseconds,
            CancellationToken cancellationToken)
        {
            GetVariablesCount++;
            return Variables;
        }

        public BackendEvaluationResult? Evaluate(
            long frameId,
            string expression,
            int timeoutMilliseconds,
            CancellationToken cancellationToken)
        {
            EvaluateCount++;
            return EvaluationResult;
        }

        public BackendSetVariableResult? SetVariable(
            long variablesReference,
            string name,
            string expression,
            int timeoutMilliseconds,
            CancellationToken cancellationToken)
        {
            SetVariableCount++;
            return SetVariableResult;
        }

        public BackendBoundBreakpoint BindBreakpoint(
            LogicalBreakpoint breakpoint)
        {
            Bound.Add(breakpoint);
            return AddBreakpoint(breakpoint.Line);
        }

        public BackendBoundBreakpoint BindFunctionBreakpoint(
            LogicalFunctionBreakpoint breakpoint)
        {
            FunctionBound.Add(breakpoint);
            return AddBreakpoint(0);
        }

        public void RemoveBreakpoint(long backendBreakpointId)
        {
            breakpointIds.Remove(backendBreakpointId);
        }

        public IReadOnlyList<BackendStepInTarget> GetStepInTargets(
            long frameId) => StepInTargets;

        public IReadOnlyList<BackendGotoTarget> GetGotoTargets(
            string sourcePath,
            int line,
            int column) => GotoTargets;

        public void Goto(long threadId, long targetId)
        {
            GotoCount++;
        }

        public void Dispose()
        {
            DisposeCount++;
        }

        public bool ContainsBreakpoint(long id) => breakpointIds.Contains(id);

        public void RaiseTargetStarted() =>
            TargetStarted?.Invoke(this, EventArgs.Empty);

        public void RaiseTargetExited()
        {
            HasExited = true;
            TargetExited?.Invoke(this, EventArgs.Empty);
        }

        public void RaiseTargetStopped(BackendStoppedEventArgs arguments) =>
            TargetStopped?.Invoke(this, arguments);

        public void RaiseThreadChanged(BackendThreadEventArgs arguments) =>
            ThreadChanged?.Invoke(this, arguments);

        public void RaiseModuleChanged(
            BackendModuleChangedEventArgs arguments) =>
            ModuleChanged?.Invoke(this, arguments);

        public void RaiseBreakpointChanged(
            BackendBreakpointChangedEventArgs arguments) =>
            BreakpointChanged?.Invoke(this, arguments);

        public void RaiseOutput(BackendOutputEventArgs arguments) =>
            Output?.Invoke(this, arguments);

        public void RaiseAssemblyUnloaded() =>
            AssemblyUnloaded?.Invoke(this, EventArgs.Empty);

        public void RaiseAssemblyLoaded() =>
            AssemblyLoaded?.Invoke(this, EventArgs.Empty);

        private BackendBoundBreakpoint AddBreakpoint(int line)
        {
            var id = nextBreakpointId++;
            breakpointIds.Add(id);
            return new BackendBoundBreakpoint(id, true, line, null);
        }
    }
}
