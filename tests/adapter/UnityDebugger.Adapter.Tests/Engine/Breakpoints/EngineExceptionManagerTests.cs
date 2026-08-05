using System.Collections.Generic;
using UnityDebugger.Adapter.Backend;
using UnityDebugger.Adapter.Engine.Breakpoints;
using Xunit;

namespace UnityDebugger.Adapter.Tests.Engine.Breakpoints
{
    public sealed class EngineExceptionManagerTests
    {
        [Fact]
        public void ModesDisableObsoleteRequestsBeforeEnablingReplacement()
        {
            var runtime = new ExceptionRuntime();
            var manager = new EngineExceptionManager(runtime);

            manager.Configure(ExceptionBreakMode.Uncaught);
            manager.Configure(ExceptionBreakMode.All);
            manager.Configure(ExceptionBreakMode.None);

            Assert.Equal(
                new[]
                {
                    "create:False:True",
                    "enable",
                    "disable",
                    "create:True:True",
                    "enable",
                    "disable",
                },
                runtime.Operations);
        }

        [Fact]
        public void KnownExceptionRequestProducesReferenceExceptionInfo()
        {
            var runtime = new ExceptionRuntime();
            var manager = new EngineExceptionManager(runtime);
            manager.Configure(ExceptionBreakMode.Uncaught);

            var result = manager.Process(
                new RuntimeExceptionHit(
                    runtime.LastRequest!.Identity,
                    "System.InvalidOperationException",
                    "Invalid state",
                    "at Button.Update()"));

            Assert.True(result.ShouldStop);
            Assert.Equal(
                "System.InvalidOperationException",
                result.ExceptionInfo!.ExceptionId);
            Assert.Equal("unhandled", result.ExceptionInfo.BreakMode);
        }

        [Fact]
        public void UnknownExceptionRequestResumes()
        {
            var manager = new EngineExceptionManager(
                new ExceptionRuntime());

            var result = manager.Process(
                new RuntimeExceptionHit(
                    new object(),
                    "System.Exception",
                    "message",
                    null));

            Assert.False(result.ShouldStop);
        }

        private sealed class ExceptionRuntime : IEngineExceptionRuntime
        {
            public List<string> Operations { get; } = new List<string>();
            public ExceptionRequest? LastRequest { get; private set; }

            public IEngineExceptionRequest CreateExceptionRequest(
                bool caught,
                bool uncaught)
            {
                Operations.Add($"create:{caught}:{uncaught}");
                LastRequest = new ExceptionRequest(Operations);
                return LastRequest;
            }
        }

        private sealed class ExceptionRequest : IEngineExceptionRequest
        {
            private readonly List<string> operations;

            public ExceptionRequest(List<string> operations)
            {
                this.operations = operations;
            }

            public object Identity { get; } = new object();
            public void Enable() => operations.Add("enable");
            public void Disable() => operations.Add("disable");
        }
    }
}
