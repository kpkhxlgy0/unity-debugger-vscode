using UnityDebugger.Adapter.Engine.Source;
using UnityDebugger.Adapter.Engine.State;
using Xunit;

namespace UnityDebugger.Adapter.Tests.Engine.State
{
    public sealed class UnityDomainStateTests
    {
        [Fact]
        public void DomainOwnsModulesAndBindingsWithoutOwningPendingBreakpoint()
        {
            var pendingBreakpoint = new object();
            var domain = new UnityDomainState(new object(), "Unity Child Domain");
            var module = domain.GetOrAddModule(
                new RuntimeModuleDescriptor(
                    "assembly-1",
                    "Assembly-CSharp.dll",
                    @"D:\project\Assembly-CSharp.dll"));

            domain.AddBoundBreakpoint(pendingBreakpoint);

            Assert.Same(module, Assert.Single(domain.Modules));
            Assert.Contains(pendingBreakpoint, domain.BoundBreakpoints);
            Assert.True(domain.RemoveBoundBreakpoint(pendingBreakpoint));
            Assert.Empty(domain.BoundBreakpoints);
        }

        [Fact]
        public void SameModuleIdentityIsRegisteredOnlyOnce()
        {
            var domain = new UnityDomainState(new object(), "Domain");
            var descriptor = new RuntimeModuleDescriptor(
                "assembly-1",
                "Assembly-CSharp.dll",
                @"D:\project\Assembly-CSharp.dll");

            var first = domain.GetOrAddModule(descriptor);
            var second = domain.GetOrAddModule(descriptor);

            Assert.Same(first, second);
            Assert.Single(domain.Modules);
        }
    }
}
