using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using UnityDebugger.Adapter.Backend;
using Xunit;

namespace UnityDebugger.Adapter.Tests.Backend
{
    public sealed class MatureControlTests
    {
        [Fact]
        public void TargetedStepAndGotoDelegateDirectlyToTheMatureFacade()
        {
            var facade = new FakeSoftDebuggerSessionFacade();
            using (var backend = new MonoDebuggingBackend(() => facade))
            {
                backend.Attach(Target());

                backend.StepIn(42, 51);
                backend.Goto(42, 71);

                Assert.Equal(42, facade.LastStepInThreadId);
                Assert.Equal(51, facade.LastStepInTargetId);
                Assert.Equal(42, facade.LastGotoThreadId);
                Assert.Equal(71, facade.LastGotoTargetId);
            }
        }

        [Fact]
        public void OrdinaryControlCallsRemainOneToOne()
        {
            var facade = new FakeSoftDebuggerSessionFacade();
            using (var backend = new MonoDebuggingBackend(() => facade))
            {
                backend.Attach(Target());

                backend.Continue(42);
                backend.Pause(42);
                backend.StepIn(42, null);
                backend.StepOver(42);
                backend.StepOut(42);

                Assert.Equal(1, facade.ContinueCount);
                Assert.Equal(1, facade.PauseCount);
                Assert.Equal(1, facade.StepInCount);
                Assert.Equal(1, facade.StepOverCount);
                Assert.Equal(1, facade.StepOutCount);
            }
        }

        [Fact]
        public void ReferenceStepTargetsUseNextIlOffsetAndStableOrdering()
        {
            var selectorType = typeof(Mono.Debugging.Soft.SoftDebuggerSession)
                .Assembly.GetType(
                    "Mono.Debugging.Soft.ReferenceStepTargetSelector");
            Assert.NotNull(selectorType);
            var select = selectorType!.GetMethod(
                "Select",
                BindingFlags.Static | BindingFlags.Public |
                BindingFlags.NonPublic);
            Assert.NotNull(select);
            var generic = select!.MakeGenericMethod(typeof(StepCandidate));
            var candidates = new[]
            {
                new StepCandidate(11, 2, "Beta", true, true),
                new StepCandidate(12, 3, "Ignored opcode", false, true),
                new StepCandidate(13, 2, "Duplicate", true, true),
                new StepCandidate(14, 4, "Ignored target", true, false),
                new StepCandidate(19, 5, "Alpha", true, true),
                new StepCandidate(20, 6, "Next statement", true, true),
            };
            var selected = (IReadOnlyList<StepCandidate>)generic.Invoke(
                null,
                new object[]
                {
                    10,
                    new[] { 10, 20, 30 },
                    candidates,
                    new Func<StepCandidate, int>(item => item.Offset),
                    new Func<StepCandidate, int>(item => item.Token),
                    new Func<StepCandidate, string>(item => item.Label),
                    new Func<StepCandidate, bool>(item => item.IsCall),
                    new Func<StepCandidate, bool>(item => item.HasTarget),
                })!;

            Assert.Equal(
                new[] { "Alpha", "Beta" },
                selected.Select(item => item.Label));
        }

        [Fact]
        public void SpecificStepBreakpointMatchesRequestAndThreadAtDispatch()
        {
            var matcherType = typeof(Mono.Debugging.Soft.SoftDebuggerSession)
                .Assembly.GetType(
                    "Mono.Debugging.Soft.ReferenceSpecificBreakpointMatcher");
            Assert.NotNull(matcherType);
            var isMatching = matcherType!.GetMethod(
                "IsMatching",
                BindingFlags.Static | BindingFlags.Public |
                BindingFlags.NonPublic);
            Assert.NotNull(isMatching);
            var request = new object();

            Assert.True((bool)isMatching!.Invoke(
                null,
                new object[] { request, 17L, request, 17L })!);
            Assert.False((bool)isMatching.Invoke(
                null,
                new object[] { request, 17L, new object(), 17L })!);
            Assert.False((bool)isMatching.Invoke(
                null,
                new object[] { request, 17L, request, 18L })!);
        }

        [Fact]
        public void GotoControlUsesRequestedThreadAndResolvedLocation()
        {
            var sessionType = typeof(Mono.Debugging.Soft.SoftDebuggerSession);
            var targetType = sessionType.Assembly.GetType(
                "Mono.Debugging.Soft.SoftGotoTarget");
            Assert.NotNull(targetType);

            Assert.NotNull(sessionType.GetMethod(
                "GetGotoTargets",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(string), typeof(int), typeof(int) },
                null));
            Assert.NotNull(sessionType.GetMethod(
                "Goto",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(long), targetType! },
                null));
        }

        private static AttachTarget Target() => new AttachTarget(
            123,
            IPAddress.Loopback,
            56000,
            @"D:\Fixture",
            "2022.3.62t12");

        private sealed class StepCandidate
        {
            public StepCandidate(
                int offset,
                int token,
                string label,
                bool isCall,
                bool hasTarget)
            {
                Offset = offset;
                Token = token;
                Label = label;
                IsCall = isCall;
                HasTarget = hasTarget;
            }

            public int Offset { get; }
            public int Token { get; }
            public string Label { get; }
            public bool IsCall { get; }
            public bool HasTarget { get; }
        }
    }
}
