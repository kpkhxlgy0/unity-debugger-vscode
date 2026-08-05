using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using UnityDebugger.Adapter.Backend;
using UnityDebugger.Adapter.Engine;
using UnityDebugger.Adapter.Engine.Control;
using UnityDebugger.Adapter.Engine.Events;
using UnityDebugger.Adapter.Engine.Mono;
using Xunit;

namespace UnityDebugger.Adapter.Tests.Engine
{
    public sealed class UnityDebuggerEngineControlTests
    {
        [Fact]
        public void AttachCreatesManagersBeforeStartingDispatch()
        {
            var connection = new RecordingConnection();
            var engine = CreateEngine(connection);

            engine.Attach(Target());
            Assert.True(
                SpinWait.SpinUntil(
                    () => connection.Operations.Contains("start-dispatch"),
                    TimeSpan.FromSeconds(1)));

            Assert.Equal(
                new[]
                {
                    "connect",
                    "create-event-source",
                    "create-step-runtime",
                    "start-dispatch",
                },
                connection.Operations.Take(4));
            engine.Disconnect();
        }

        [Fact]
        public void StepEventResetsSuspendedStateBeforeRaisingStopped()
        {
            var connection = new RecordingConnection();
            var engine = CreateEngine(connection);
            engine.Attach(Target());
            WaitForDispatch(connection);
            var frameId = engine.SuspendedState.RegisterFrame(new object());
            var wasReset = false;
            engine.Stopped += (_, __) =>
            {
                wasReset = !engine.SuspendedState.TryGetFrame<object>(
                    frameId,
                    out _);
            };

            connection.Source.Add(
                new EngineEventSet(
                    EngineSuspendPolicy.All,
                    EngineEvent.Step(7)));

            Assert.True(
                SpinWait.SpinUntil(
                    () => wasReset,
                    TimeSpan.FromSeconds(1)));
            engine.Disconnect();
        }

        [Fact]
        public void ContinueCancelsStepBeforeResumeWithoutResettingHandles()
        {
            var connection = new RecordingConnection();
            var engine = CreateEngine(connection);
            engine.Attach(Target());
            WaitForDispatch(connection);
            engine.StepOver(7);
            connection.Operations.Clear();
            var frame = new object();
            var frameId = engine.SuspendedState.RegisterFrame(frame);

            engine.Continue(7);

            Assert.Equal(
                new[] { "disable:Over", "resume" },
                connection.Operations);
            Assert.True(
                engine.SuspendedState.TryGetFrame<object>(
                    frameId,
                    out var mapped));
            Assert.Same(frame, mapped);
            engine.Disconnect();
        }

        [Fact]
        public void PauseSuspendsThenRaisesOneStopPerMappedThread()
        {
            var connection = new RecordingConnection();
            connection.Threads.Add(new BackendThread(7, "Main"));
            connection.Threads.Add(new BackendThread(9, "Worker"));
            var engine = CreateEngine(connection);
            engine.Attach(Target());
            WaitForDispatch(connection);
            var stoppedThreads = new List<long>();
            var callbackPropertyId = 0;
            engine.Stopped += (_, arguments) =>
            {
                stoppedThreads.Add(arguments.ThreadId);
                if (callbackPropertyId != 0)
                {
                    Assert.False(
                        engine.SuspendedState.TryGetProperty<object>(
                            callbackPropertyId,
                            out _));
                }
                callbackPropertyId =
                    engine.SuspendedState.RegisterProperty(new object());
            };
            engine.SuspendedState.RegisterProperty(new object());
            connection.Operations.Clear();

            engine.Pause(7);

            Assert.Equal("suspend", connection.Operations[0]);
            Assert.Equal(new long[] { 7, 9 }, stoppedThreads);
            Assert.Equal(1, callbackPropertyId);
            engine.Disconnect();
        }

        [Fact]
        public void DispatcherTerminationIsRaisedOnce()
        {
            var expected = new InvalidOperationException("queue");
            var connection = new RecordingConnection(expected);
            var engine = CreateEngine(connection);
            var terminationCount = 0;
            engine.Terminated += (_, __) => terminationCount++;

            engine.Attach(Target());

            Assert.True(
                SpinWait.SpinUntil(
                    () => terminationCount == 1,
                    TimeSpan.FromSeconds(1)));
            engine.Disconnect();
            Assert.Equal(1, terminationCount);
        }

        private static UnityDebuggerEngine CreateEngine(
            RecordingConnection connection) =>
            new UnityDebuggerEngine(_ => connection);

        private static void WaitForDispatch(
            RecordingConnection connection)
        {
            Assert.True(
                SpinWait.SpinUntil(
                    () => connection.Operations.Contains("start-dispatch"),
                    TimeSpan.FromSeconds(1)));
        }

        private static AttachTarget Target() =>
            new AttachTarget(
                1234,
                IPAddress.Loopback,
                56000,
                @"H:\fixture",
                "2022.3.62t11");

        private sealed class RecordingConnection : IMonoEngineConnection
        {
            public RecordingConnection(Exception? sourceFailure = null)
            {
                Source = new RecordingEventSource(
                    Operations,
                    sourceFailure);
            }

            public List<string> Operations { get; } = new List<string>();
            public List<BackendThread> Threads { get; } =
                new List<BackendThread>();
            public RecordingEventSource Source { get; }

            public void Connect()
            {
                Operations.Add("connect");
            }

            public IEngineEventSource CreateEventSource()
            {
                Operations.Add("create-event-source");
                return Source;
            }

            public IStepRuntime CreateStepRuntime(
                EngineEventDispatcher dispatcher)
            {
                Operations.Add("create-step-runtime");
                return new RecordingStepRuntime(Operations);
            }

            public IReadOnlyList<BackendThread> GetThreads() => Threads;

            public IReadOnlyList<BackendStackFrame> GetStackTrace(
                long threadId,
                int startFrame,
                int levels) => Array.Empty<BackendStackFrame>();

            public void Suspend()
            {
                Operations.Add("suspend");
            }

            public void Disconnect()
            {
                Operations.Add("disconnect");
            }

            public void Dispose()
            {
                Operations.Add("dispose");
            }
        }

        private sealed class RecordingEventSource : IEngineEventSource
        {
            private readonly List<string> operations;
            private readonly Exception? failure;
            private readonly BlockingCollection<EngineEventSet> sets =
                new BlockingCollection<EngineEventSet>();
            private int started;

            public RecordingEventSource(
                List<string> operations,
                Exception? failure)
            {
                this.operations = operations;
                this.failure = failure;
            }

            public void Add(EngineEventSet value)
            {
                sets.Add(value);
            }

            public EngineEventSet GetNextEventSet(
                CancellationToken cancellationToken)
            {
                if (Interlocked.Exchange(ref started, 1) == 0)
                    operations.Add("start-dispatch");
                if (failure != null)
                    throw failure;
                return sets.Take(cancellationToken);
            }

            public void Resume()
            {
                operations.Add("event-resume");
            }
        }

        private sealed class RecordingStepRuntime : IStepRuntime
        {
            private readonly List<string> operations;

            public RecordingStepRuntime(List<string> operations)
            {
                this.operations = operations;
            }

            public bool IsStaticConstructorFrame(long threadId) => false;

            public IStepRequest CreateStepRequest(
                long threadId,
                StepRequestOptions options)
            {
                operations.Add($"create:{options.Depth}");
                return new RecordingStepRequest(
                    operations,
                    options.Depth);
            }

            public void Resume()
            {
                operations.Add("resume");
            }
        }

        private sealed class RecordingStepRequest : IStepRequest
        {
            private readonly List<string> operations;
            private readonly EngineStepDepth depth;

            public RecordingStepRequest(
                List<string> operations,
                EngineStepDepth depth)
            {
                this.operations = operations;
                this.depth = depth;
            }

            public void Enable()
            {
                operations.Add($"enable:{depth}");
            }

            public void Disable()
            {
                operations.Add($"disable:{depth}");
            }
        }
    }
}
