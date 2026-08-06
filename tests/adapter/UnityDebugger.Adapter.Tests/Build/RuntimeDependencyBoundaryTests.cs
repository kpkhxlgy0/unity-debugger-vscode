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
        public void SolutionExposesThePinnedMatureDebuggerStack()
        {
            Assert.Equal(
                "Mono.Debugging",
                typeof(Mono.Debugging.Client.EvaluationOptions)
                    .Assembly.GetName().Name);
            Assert.Equal(
                "Mono.Debugging.Soft",
                typeof(Mono.Debugging.Soft.SoftDebuggerSession)
                    .Assembly.GetName().Name);
            Assert.Equal(
                "Mono.Debugger.Soft",
                typeof(Mono.Debugger.Soft.VirtualMachine)
                    .Assembly.GetName().Name);
        }

        [Fact]
        public void ProductionAssemblyContainsNoRejectedDirectEngineTypes()
        {
            var executable = Path.Combine(
                AppContext.BaseDirectory,
                "UnityDebuggerPure.exe");
            var names = Assembly.LoadFrom(executable)
                .GetTypes()
                .Select(value => value.FullName ?? string.Empty)
                .ToArray();

            Assert.DoesNotContain(
                names,
                name => name.StartsWith(
                    "UnityDebugger.Adapter.Engine.",
                    StringComparison.Ordinal));
            Assert.DoesNotContain(
                names,
                name => name.Contains("ExpressionEvaluator"));
            Assert.DoesNotContain(
                names,
                name => name.Contains("StepManager"));
            Assert.Contains(
                "UnityDebugger.Adapter.Backend.MonoDebuggingBackend",
                names);
        }
    }
}
