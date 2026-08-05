using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityDebugger.Adapter.Engine.Events;
using Xunit;

namespace UnityDebugger.Adapter.Tests.Engine
{
    public sealed class EngineEventDispatcherTests
    {
        [Fact]
        public async Task NonBreakingSuspendedEventSetIsResumed()
        {
            var source = new ScriptedEventSource();
            source.Add(
                new EngineEventSet(
                    EngineSuspendPolicy.All,
                    EngineEvent.ThreadStarted(2)));
            var receiver = new RecordingEventReceiver();
            var dispatcher = new EngineEventDispatcher(source, receiver);

            dispatcher.Start();
            Assert.True(receiver.WaitForCount(1));
            Assert.True(source.WaitForResumeCount(1));
            dispatcher.Stop();
            await dispatcher.Completion;

            Assert.Equal(
                new[] { "event:ThreadStarted:2", "resume" },
                source.AndReceiverLog(receiver));
        }

        [Fact]
        public async Task BreakingEventsFromOtherThreadsAreQueuedUntilResume()
        {
            var source = new ScriptedEventSource();
            source.Add(
                new EngineEventSet(
                    EngineSuspendPolicy.All,
                    EngineEvent.Breakpoint(3),
                    EngineEvent.Step(4)));
            var receiver = new RecordingEventReceiver();
            var dispatcher = new EngineEventDispatcher(source, receiver);

            dispatcher.Start();
            Assert.True(receiver.WaitForCount(1));
            Assert.Equal(new[] { "Breakpoint:3" }, receiver.Events);

            dispatcher.BeforeResuming();
            dispatcher.AfterResuming();
            Assert.True(receiver.WaitForCount(2));
            dispatcher.Stop();
            await dispatcher.Completion;

            Assert.Equal(
                new[] { "Breakpoint:3", "Step:4" },
                receiver.Events);
        }

        [Fact]
        public async Task SameThreadExceptionConsumesOneResumeWithoutSecondStop()
        {
            var source = new ScriptedEventSource();
            source.Add(
                new EngineEventSet(
                    EngineSuspendPolicy.All,
                    EngineEvent.Breakpoint(3),
                    EngineEvent.Exception(3)));
            var receiver = new RecordingEventReceiver();
            var dispatcher = new EngineEventDispatcher(source, receiver);

            dispatcher.Start();
            Assert.True(receiver.WaitForCount(1));
            Assert.True(source.WaitForResumeCount(1));
            dispatcher.Stop();
            await dispatcher.Completion;

            Assert.Equal(new[] { "Breakpoint:3" }, receiver.Events);
        }

        [Fact]
        public async Task SameThreadNonExceptionBreakingEventsAreBothDispatched()
        {
            var source = new ScriptedEventSource();
            source.Add(
                new EngineEventSet(
                    EngineSuspendPolicy.All,
                    EngineEvent.Breakpoint(3),
                    EngineEvent.Step(3)));
            var receiver = new RecordingEventReceiver();
            var dispatcher = new EngineEventDispatcher(source, receiver);

            dispatcher.Start();
            Assert.True(receiver.WaitForCount(2));
            dispatcher.Stop();
            await dispatcher.Completion;

            Assert.Equal(
                new[] { "Breakpoint:3", "Step:3" },
                receiver.Events);
            Assert.Equal(0, source.ResumeCount);
        }

        [Fact]
        public async Task SuspendPolicyNoneDoesNotResumeNonBreakingEventSet()
        {
            var source = new ScriptedEventSource();
            source.Add(
                new EngineEventSet(
                    EngineSuspendPolicy.None,
                    EngineEvent.ThreadExited(9)));
            var receiver = new RecordingEventReceiver();
            var dispatcher = new EngineEventDispatcher(source, receiver);

            dispatcher.Start();
            Assert.True(receiver.WaitForCount(1));
            dispatcher.Stop();
            await dispatcher.Completion;

            Assert.Equal(0, source.ResumeCount);
        }

        [Fact]
        public async Task UnexpectedSourceFailureTerminatesExactlyOnce()
        {
            var expected = new InvalidOperationException("event queue");
            var source = new ScriptedEventSource(expected);
            var receiver = new RecordingEventReceiver();
            var dispatcher = new EngineEventDispatcher(source, receiver);

            dispatcher.Start();
            await dispatcher.Completion;

            Assert.Single(receiver.Terminations);
            Assert.Same(expected, receiver.Terminations[0]);
        }

        private sealed class ScriptedEventSource : IEngineEventSource
        {
            private readonly BlockingCollection<EngineEventSet> eventSets =
                new BlockingCollection<EngineEventSet>();
            private readonly Exception? failure;
            private readonly ManualResetEventSlim resumed =
                new ManualResetEventSlim();
            private int resumeCount;

            public ScriptedEventSource(Exception? failure = null)
            {
                this.failure = failure;
            }

            public int ResumeCount => Volatile.Read(ref resumeCount);

            public void Add(EngineEventSet value)
            {
                eventSets.Add(value);
            }

            public EngineEventSet GetNextEventSet(
                CancellationToken cancellationToken)
            {
                if (failure != null)
                    throw failure;
                return eventSets.Take(cancellationToken);
            }

            public void Resume()
            {
                Interlocked.Increment(ref resumeCount);
                resumed.Set();
            }

            public bool WaitForResumeCount(int expected) =>
                SpinWait.SpinUntil(
                    () => ResumeCount >= expected,
                    TimeSpan.FromSeconds(1));

            public IReadOnlyList<string> AndReceiverLog(
                RecordingEventReceiver receiver)
            {
                var values = receiver.Events
                    .Select(value => $"event:{value}")
                    .ToList();
                for (var index = 0; index < ResumeCount; index++)
                    values.Add("resume");
                return values;
            }
        }

        private sealed class RecordingEventReceiver : IEngineEventReceiver
        {
            private readonly ConcurrentQueue<string> events =
                new ConcurrentQueue<string>();
            private readonly ConcurrentQueue<Exception?> terminations =
                new ConcurrentQueue<Exception?>();

            public IReadOnlyList<string> Events => events.ToArray();
            public IReadOnlyList<Exception?> Terminations =>
                terminations.ToArray();

            public void Process(EngineEvent value)
            {
                events.Enqueue($"{value.Kind}:{value.ThreadId}");
            }

            public void Terminate(Exception? exception)
            {
                terminations.Enqueue(exception);
            }

            public bool WaitForCount(int expected) =>
                SpinWait.SpinUntil(
                    () => events.Count >= expected,
                    TimeSpan.FromSeconds(1));
        }
    }
}
