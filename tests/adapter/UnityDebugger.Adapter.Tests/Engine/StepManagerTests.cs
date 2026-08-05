using System;
using System.Collections.Generic;
using UnityDebugger.Adapter.Engine.Control;
using Xunit;

namespace UnityDebugger.Adapter.Tests.Engine
{
    public sealed class StepManagerTests
    {
        [Fact]
        public void NewStepDisablesOldRequestBeforeEnablingAndResumingNewRequest()
        {
            var runtime = new RecordingStepRuntime();
            var manager = new StepManager(runtime);

            manager.RequestStep(7, EngineStepDepth.Into);
            manager.RequestStep(7, EngineStepDepth.Over);

            Assert.Equal(
                new[]
                {
                    "create:7:Into",
                    "enable:Into",
                    "resume",
                    "disable:Into",
                    "create:7:Over",
                    "enable:Over",
                    "resume",
                },
                runtime.Operations);
        }

        [Fact]
        public void StepEventDisablesActiveRequestBeforeCompleting()
        {
            var runtime = new RecordingStepRuntime();
            var manager = new StepManager(
                runtime,
                threadId => runtime.Operations.Add($"complete:{threadId}"));
            manager.RequestStep(11, EngineStepDepth.Out);
            runtime.Operations.Clear();

            manager.ProcessStepEvent(11);
            manager.ProcessStepEvent(11);

            Assert.Equal(
                new[] { "disable:Out", "complete:11", "complete:11" },
                runtime.Operations);
        }

        [Fact]
        public void ContinueCancellationIsIdempotent()
        {
            var runtime = new RecordingStepRuntime();
            var manager = new StepManager(runtime);
            manager.RequestStep(3, EngineStepDepth.Over);
            runtime.Operations.Clear();

            manager.CancelStep();
            manager.CancelStep();

            Assert.Equal(new[] { "disable:Over" }, runtime.Operations);
        }

        [Fact]
        public void DefaultStepUsesLineSizeAndReferenceFilters()
        {
            var runtime = new RecordingStepRuntime();
            var manager = new StepManager(runtime);

            manager.RequestStep(5, EngineStepDepth.Into);

            var options = Assert.Single(runtime.Options);
            Assert.Equal(EngineStepSize.Line, options.Size);
            Assert.Equal(EngineStepDepth.Into, options.Depth);
            Assert.Equal(
                EngineStepFilter.DebuggerHidden |
                EngineStepFilter.DebuggerStepThrough |
                EngineStepFilter.DebuggerNonUserCode |
                EngineStepFilter.StaticConstructor,
                options.Filters);
        }

        [Fact]
        public void StaticConstructorFrameOmitsStaticConstructorFilter()
        {
            var runtime = new RecordingStepRuntime
            {
                IsInStaticConstructor = true,
            };
            var manager = new StepManager(runtime);

            manager.RequestStep(5, EngineStepDepth.Into);

            Assert.Equal(
                EngineStepFilter.DebuggerHidden |
                EngineStepFilter.DebuggerStepThrough |
                EngineStepFilter.DebuggerNonUserCode,
                Assert.Single(runtime.Options).Filters);
        }

        [Fact]
        public void EnableFailureResumesAndLeavesRequestForCancellation()
        {
            var runtime = new RecordingStepRuntime
            {
                EnableException = new InvalidOperationException("enable"),
            };
            var manager = new StepManager(runtime);

            Assert.Throws<InvalidOperationException>(
                () => manager.RequestStep(2, EngineStepDepth.Into));
            runtime.EnableException = null;
            manager.CancelStep();

            Assert.Equal(
                new[]
                {
                    "create:2:Into",
                    "enable:Into",
                    "resume",
                    "disable:Into",
                },
                runtime.Operations);
        }

        private sealed class RecordingStepRuntime : IStepRuntime
        {
            public List<string> Operations { get; } = new List<string>();
            public List<StepRequestOptions> Options { get; } =
                new List<StepRequestOptions>();
            public bool IsInStaticConstructor { get; set; }
            public Exception? EnableException { get; set; }

            public bool IsStaticConstructorFrame(long threadId) =>
                IsInStaticConstructor;

            public IStepRequest CreateStepRequest(
                long threadId,
                StepRequestOptions options)
            {
                Options.Add(options);
                Operations.Add($"create:{threadId}:{options.Depth}");
                return new RecordingStepRequest(this, options.Depth);
            }

            public void Resume()
            {
                Operations.Add("resume");
            }

            private sealed class RecordingStepRequest : IStepRequest
            {
                private readonly RecordingStepRuntime runtime;
                private readonly EngineStepDepth depth;

                public RecordingStepRequest(
                    RecordingStepRuntime runtime,
                    EngineStepDepth depth)
                {
                    this.runtime = runtime;
                    this.depth = depth;
                }

                public void Enable()
                {
                    runtime.Operations.Add($"enable:{depth}");
                    if (runtime.EnableException != null)
                        throw runtime.EnableException;
                }

                public void Disable()
                {
                    runtime.Operations.Add($"disable:{depth}");
                }
            }
        }
    }
}
