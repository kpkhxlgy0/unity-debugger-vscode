using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;

namespace UnityDebugger.Adapter.Tests.Build
{
    public sealed class RuntimeDependencyBoundaryTests
    {
        [Fact]
        public void ProductionAdapterUsesOnlyTheNewEngineDependencies()
        {
            var executable = Path.Combine(
                AppContext.BaseDirectory,
                "UnityDebuggerPure.exe");
            var references = Assembly.LoadFrom(executable)
                .GetReferencedAssemblies()
                .Select(value => value.Name)
                .ToArray();

            Assert.DoesNotContain("Mono.Debugging", references);
            Assert.DoesNotContain("Mono.Debugging.Soft", references);
            Assert.DoesNotContain("ICSharpCode.NRefactory", references);
            Assert.DoesNotContain(
                "ICSharpCode.NRefactory.CSharp",
                references);
            Assert.Contains("Mono.Debugger.Soft", references);
            Assert.Contains("Microsoft.CodeAnalysis.CSharp", references);
        }
    }
}
