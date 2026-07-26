using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityDebugger.Adapter.Backend;

namespace UnityDebugger.TestAdapter
{
    internal sealed class ScenarioDebuggerBackend : IDebuggerBackend
    {
        private readonly string scenario;
        private readonly CancellationTokenSource lifetime =
            new CancellationTokenSource();
        private readonly object breakpointLock = new object();
        private readonly HashSet<long> activeBreakpointIds =
            new HashSet<long>();
        private AttachTarget? target;
        private long nextBreakpointId = 1;
        private int firstBreakpoint;
        private int pauseRequested;

#pragma warning disable CS0067
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
#pragma warning restore CS0067

        public ScenarioDebuggerBackend(string scenario)
        {
            this.scenario = scenario;
        }

        public bool IsAttached { get; private set; }
        public int DisposeCount { get; private set; }

        public void Attach(AttachTarget attachTarget)
        {
            target = attachTarget;
            IsAttached = true;
        }

        public void Disconnect()
        {
            IsAttached = false;
            lifetime.Cancel();
        }

        public IReadOnlyList<BackendThread> GetThreads()
        {
            ThrowIfCrashScenario();
            return new[] { new BackendThread(1, "Main Thread") };
        }

        public IReadOnlyList<BackendStackFrame> GetStackTrace(
            long threadId,
            int startFrame,
            int levels)
        {
            ThrowIfCrashScenario();
            var sourcePath = Path.Combine(
                target!.WorkspaceRoot,
                "Assets",
                "DebuggerFixture.cs");
            if (
                scenario == "pause-source" &&
                Volatile.Read(ref pauseRequested) != 0)
            {
                return new[]
                {
                    new BackendStackFrame(
                        9,
                        1,
                        "UnityEngine.PlayerLoop",
                        string.Empty,
                        0,
                        1),
                    new BackendStackFrame(
                        10,
                        1,
                        "FixtureBehaviour.Update",
                        sourcePath,
                        12,
                        1),
                };
            }
            return new[]
            {
                new BackendStackFrame(
                    10,
                    1,
                    "FixtureBehaviour.Update",
                    sourcePath,
                    12,
                    1),
            };
        }

        public IReadOnlyList<BackendScope> GetScopes(long frameId)
        {
            ThrowIfCrashScenario();
            return new[]
            {
                new BackendScope("Locals", 20, false),
            };
        }

        public IReadOnlyList<BackendVariable> GetVariables(
            long variablesReference)
        {
            ThrowIfCrashScenario();
            return new[]
            {
                new BackendVariable(
                    "health",
                    "0",
                    "System.Int32",
                    0),
            };
        }

        public BackendEvaluationResult Evaluate(
            long frameId,
            string expression,
            BackendEvaluationMode mode)
        {
            ThrowIfCrashScenario();
            if (
                scenario == "pause-source" &&
                string.Equals(
                    expression,
                    "_isVisible",
                    StringComparison.Ordinal) &&
                mode == BackendEvaluationMode.Safe)
            {
                return new BackendEvaluationResult(
                    "false",
                    "System.Boolean",
                    0);
            }
            return new BackendEvaluationResult(
                "0",
                "System.Int32",
                0);
        }

        public BackendBoundBreakpoint BindBreakpoint(
            LogicalBreakpoint breakpoint)
        {
            var id = nextBreakpointId++;
            lock (breakpointLock)
                activeBreakpointIds.Add(id);
            if (Interlocked.Exchange(ref firstBreakpoint, 1) == 0)
            {
                if (scenario == "reload")
                {
                    Schedule(
                        () => ReloadStarted?.Invoke(
                            this,
                            EventArgs.Empty),
                        30);
                    Schedule(
                        () => BreakpointChanged?.Invoke(
                            this,
                            new BackendBreakpointChangedEventArgs(
                                new BackendBoundBreakpoint(
                                    id,
                                    false,
                                    breakpoint.Line,
                                    "Symbols are not loaded."))),
                        70);
                    Schedule(
                        () => ReloadCompleted?.Invoke(
                            this,
                            EventArgs.Empty),
                        100);
                    ScheduleBreakpointStop(id, 130);
                }
                else if (scenario != "exception")
                {
                    ScheduleStop(BackendStopReason.Breakpoint, 30);
                }
            }
            return new BackendBoundBreakpoint(
                id,
                true,
                breakpoint.Line,
                null);
        }

        public void RemoveBreakpoint(long backendBreakpointId)
        {
            lock (breakpointLock)
                activeBreakpointIds.Remove(backendBreakpointId);
        }

        public void Continue(long threadId)
        {
            Continued?.Invoke(this, EventArgs.Empty);
            ScheduleStop(BackendStopReason.Pause, 30);
        }

        public void Pause(long threadId)
        {
            if (scenario == "pause-source")
                Interlocked.Exchange(ref pauseRequested, 1);
            ScheduleStop(BackendStopReason.Pause, 10);
        }

        public void StepIn(long threadId) => Step();
        public void StepOver(long threadId) => Step();
        public void StepOut(long threadId) => Step();

        public void ConfigureExceptions(ExceptionBreakMode mode)
        {
            if (
                scenario == "exception" &&
                mode == ExceptionBreakMode.All)
            {
                ScheduleStop(BackendStopReason.Exception, 30);
            }
        }

        public void Dispose()
        {
            DisposeCount++;
            IsAttached = false;
            lifetime.Cancel();
            lifetime.Dispose();
        }

        private void Step()
        {
            Continued?.Invoke(this, EventArgs.Empty);
            ScheduleStop(BackendStopReason.Step, 30);
        }

        private void ScheduleStop(
            BackendStopReason reason,
            int delayMilliseconds)
        {
            Schedule(
                () => Stopped?.Invoke(
                    this,
                    new BackendStoppedEventArgs(reason, 1, null)),
                delayMilliseconds);
        }

        private void ScheduleBreakpointStop(
            long backendBreakpointId,
            int delayMilliseconds)
        {
            Schedule(
                () =>
                {
                    lock (breakpointLock)
                    {
                        if (!activeBreakpointIds.Contains(
                            backendBreakpointId))
                        {
                            return;
                        }
                    }
                    Stopped?.Invoke(
                        this,
                        new BackendStoppedEventArgs(
                            BackendStopReason.Breakpoint,
                            1,
                            null));
                },
                delayMilliseconds);
        }

        private void Schedule(Action action, int delayMilliseconds)
        {
            var token = lifetime.Token;
            _ = Task.Run(
                async () =>
                {
                    try
                    {
                        await Task.Delay(delayMilliseconds, token)
                            .ConfigureAwait(false);
                        if (!token.IsCancellationRequested)
                            action();
                    }
                    catch (OperationCanceledException)
                    {
                    }
                },
                token);
        }

        private void ThrowIfCrashScenario()
        {
            if (scenario == "backend-crash")
            {
                throw new DebuggerBackendException(
                    "Simulated backend failure.");
            }
        }
    }
}
