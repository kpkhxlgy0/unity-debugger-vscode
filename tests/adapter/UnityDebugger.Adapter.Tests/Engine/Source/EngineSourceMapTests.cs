using System.Collections.Generic;
using System.Linq;
using UnityDebugger.Adapter.Backend;
using UnityDebugger.Adapter.Engine.Source;
using UnityDebugger.Adapter.Tests.Engine.Evaluation;
using Xunit;

namespace UnityDebugger.Adapter.Tests.Engine.Source
{
    public sealed class EngineSourceMapTests
    {
        [Fact]
        public void TypeLoadMapsNestedTypesAndMarksModuleWithSymbols()
        {
            var domain = new object();
            var module = Module("assembly-1");
            var nested = new FakeRuntimeType("Nested", "Game.Nested")
                .WithSource(domain, module, @"D:\project\Assets\Button.cs", 22);
            var outer = new FakeRuntimeType("Button", "Game.Button")
                .WithSource(domain, module, @"D:\project\Assets\Button.cs", 10)
                .WithNested(nested);
            var manager = new EngineSourceMapManager();

            manager.ProcessTypeLoaded(outer);

            Assert.Equal(
                new[] { 10, 22 },
                manager.GetLocations(
                        @"D:\project\Assets\Button.cs",
                        0)
                    .Select(value => value.Line));
            Assert.True(Assert.Single(manager.Modules).HasSymbols);
        }

        [Fact]
        public void WindowsSourceLookupIsNormalizedAndCaseInsensitive()
        {
            var domain = new object();
            var module = Module("assembly-1");
            var type = new FakeRuntimeType("Button", "Game.Button")
                .WithSource(domain, module, @"D:\Project\Assets\Button.cs", 95);
            var manager = new EngineSourceMapManager();
            manager.ProcessTypeLoaded(type);

            var locations = manager.GetLocations(
                @"d:/project/assets/BUTTON.cs",
                95);

            Assert.Single(locations);
        }

        [Fact]
        public void RepeatedTypeLoadsEmitOneOrderedModuleLifecycle()
        {
            var firstDomain = new object();
            var secondDomain = new object();
            var firstModule = Module("assembly-1");
            var secondModule = Module("assembly-2");
            var events = new List<string>();
            var manager = new EngineSourceMapManager();
            manager.ModuleChanged += (_, arguments) => events.Add(
                $"{(arguments.Loaded ? "load" : "unload")}:" +
                arguments.Module.Id);
            var firstType = new FakeRuntimeType("First", "Game.First")
                .WithSource(
                    firstDomain,
                    firstModule,
                    @"D:\project\Assets\First.cs",
                    10);
            var secondType = new FakeRuntimeType("Second", "Game.Second")
                .WithSource(
                    secondDomain,
                    secondModule,
                    @"D:\project\Assets\Second.cs",
                    20);

            manager.ProcessTypeLoaded(firstType);
            manager.ProcessTypeLoaded(firstType);
            manager.ProcessTypeLoaded(secondType);
            manager.RemoveDomain(firstDomain);

            Assert.Equal(
                new[]
                {
                    "load:assembly-1",
                    "load:assembly-2",
                    "unload:assembly-1",
                },
                events);
            Assert.Equal("assembly-2", Assert.Single(manager.Modules).Id);
            Assert.Empty(manager.GetLocations(
                @"D:\project\Assets\First.cs",
                0));
            Assert.Single(manager.GetLocations(
                @"D:\project\Assets\Second.cs",
                0));
        }

        private static RuntimeModuleDescriptor Module(string id) =>
            new RuntimeModuleDescriptor(
                id,
                $"{id}.dll",
                $@"D:\project\Library\ScriptAssemblies\{id}.dll");
    }
}
