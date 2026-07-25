using System;
using System.IO;
using System.Reflection;
using Xunit;

namespace UnityDebugger.Adapter.Tests.Build
{
    public sealed class AdapterAssemblyTests
    {
        [Fact]
        public void Production_adapter_has_expected_assembly_name()
        {
            var executable = Path.Combine(
                AppContext.BaseDirectory,
                "UnityCommunityDebug.exe");

            var assembly = Assembly.LoadFrom(executable);

            Assert.Equal("UnityCommunityDebug", assembly.GetName().Name);
        }
    }
}
