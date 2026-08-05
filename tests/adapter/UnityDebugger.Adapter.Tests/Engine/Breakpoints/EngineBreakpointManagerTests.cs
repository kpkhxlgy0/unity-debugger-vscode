using System;
using System.Collections.Generic;
using System.Linq;
using UnityDebugger.Adapter.Backend;
using UnityDebugger.Adapter.Engine.Breakpoints;
using UnityDebugger.Adapter.Engine.Source;
using UnityDebugger.Adapter.Tests.Engine.Evaluation;
using Xunit;

namespace UnityDebugger.Adapter.Tests.Engine.Breakpoints
{
    public sealed class EngineBreakpointManagerTests
    {
        [Fact]
        public void DomainReloadUnbindsThenRebindsTheSamePendingBreakpoint()
        {
            var runtime = new FakeBreakpointRuntime();
            var sources = new EngineSourceMapManager();
            var manager = new EngineBreakpointManager(sources, runtime);
            var pending = manager.RequestSourceBreakpoint(
                SourceBreakpoint(@"D:\project\Assets\Button.cs", 95));
            var firstType = LoadedType(
                "domain-1",
                "assembly-1",
                @"D:\project\Assets\Button.cs",
                95);
            manager.ProcessTypeLoaded(firstType);
            var firstBoundId = Assert.Single(pending.Bound).Id;
            Assert.True(sources.TryGetDomain(
                firstType.DomainIdentity,
                out var firstDomain));

            manager.UnbindDomain(firstDomain);

            Assert.Empty(pending.Bound);
            Assert.True(manager.ContainsPending(pending.Id));
            Assert.True(runtime.Requests[0].Disabled);

            var replacement = LoadedType(
                "domain-2",
                "assembly-2",
                @"D:\project\Assets\Button.cs",
                95);
            manager.ProcessTypeLoaded(replacement);

            Assert.Single(pending.Bound);
            Assert.NotEqual(firstBoundId, pending.Bound.Single().Id);
            Assert.Equal(2, runtime.Requests.Count);
        }

        [Fact]
        public void NestedTypeLocationsBindThePendingSourceBreakpoint()
        {
            var runtime = new FakeBreakpointRuntime();
            var manager = new EngineBreakpointManager(
                new EngineSourceMapManager(),
                runtime);
            var pending = manager.RequestSourceBreakpoint(
                SourceBreakpoint(@"D:\project\Assets\Button.cs", 22));
            var module = Module("assembly-1");
            var nested = new FakeRuntimeType("Nested", "Game.Outer+Nested")
                .WithSource(
                    "domain-1",
                    module,
                    @"D:\project\Assets\Button.cs",
                    22);
            var outer = new FakeRuntimeType("Outer", "Game.Outer`1")
                .WithSource(
                    "domain-1",
                    module,
                    @"D:\project\Assets\Button.cs",
                    10)
                .WithNested(nested);

            manager.ProcessTypeLoaded(outer);

            Assert.Single(pending.Bound);
        }

        [Fact]
        public void BreakpointHitReturnsPendingIdWhileUnknownHitsResume()
        {
            var runtime = new FakeBreakpointRuntime();
            var manager = new EngineBreakpointManager(
                new EngineSourceMapManager(),
                runtime);
            var pending = manager.RequestSourceBreakpoint(
                SourceBreakpoint(@"D:\project\Assets\Button.cs", 95));
            manager.ProcessTypeLoaded(LoadedType(
                "domain-1",
                "assembly-1",
                @"D:\project\Assets\Button.cs",
                95));
            var request = Assert.Single(runtime.Requests);

            var known = manager.ProcessBreakpointHit(
                new RuntimeBreakpointHit(request.Identity, true));
            var unknown = manager.ProcessBreakpointHit(
                new RuntimeBreakpointHit(new object(), true));
            var nonDebuggable = manager.ProcessBreakpointHit(
                new RuntimeBreakpointHit(request.Identity, false));

            Assert.Equal(BreakpointHitAction.Stop, known.Action);
            Assert.Equal(new[] { pending.Id }, known.BreakpointIds);
            Assert.Equal(BreakpointHitAction.Resume, unknown.Action);
            Assert.Equal(BreakpointHitAction.Resume, nonDebuggable.Action);
        }

        [Fact]
        public void RemovingPendingBreakpointDisablesAllBoundRequests()
        {
            var runtime = new FakeBreakpointRuntime();
            var manager = new EngineBreakpointManager(
                new EngineSourceMapManager(),
                runtime);
            var pending = manager.RequestSourceBreakpoint(
                SourceBreakpoint(@"D:\project\Assets\Button.cs", 95));
            manager.ProcessTypeLoaded(LoadedType(
                "domain-1",
                "assembly-1",
                @"D:\project\Assets\Button.cs",
                95));

            manager.RemovePendingBreakpoint(pending.Id);

            Assert.False(manager.ContainsPending(pending.Id));
            Assert.True(Assert.Single(runtime.Requests).Disabled);
        }

        private static LogicalBreakpoint SourceBreakpoint(
            string path,
            int line) =>
            new LogicalBreakpoint(
                7,
                path,
                line,
                1,
                null,
                null,
                null);

        private static FakeRuntimeType LoadedType(
            object domain,
            string assembly,
            string path,
            int line) =>
            new FakeRuntimeType("Button", "Game.Button")
                .WithSource(domain, Module(assembly), path, line);

        private static RuntimeModuleDescriptor Module(string id) =>
            new RuntimeModuleDescriptor(
                id,
                $"{id}.dll",
                $@"D:\project\{id}.dll");

        private sealed class FakeBreakpointRuntime : IEngineBreakpointRuntime
        {
            public List<FakeBreakpointRequest> Requests { get; } =
                new List<FakeBreakpointRequest>();

            public IEngineBreakpointRequest CreateBreakpoint(
                EngineSourceLocation location)
            {
                var request = new FakeBreakpointRequest();
                Requests.Add(request);
                return request;
            }
        }

        private sealed class FakeBreakpointRequest : IEngineBreakpointRequest
        {
            public object Identity { get; } = new object();
            public bool Disabled { get; private set; }

            public void Enable()
            {
            }

            public void Disable()
            {
                Disabled = true;
            }
        }
    }
}
