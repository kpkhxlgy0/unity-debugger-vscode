using System;
using System.Collections.Generic;
using System.Reflection;
using Xunit;

namespace UnityDebugger.Adapter.Tests.Backend
{
    public sealed class ReferenceExceptionRequestInstallerTests
    {
        [Fact]
        public void InstallsComplementaryUnhandledAndCaughtRequests()
        {
            var installerType = typeof(
                    Mono.Debugging.Soft.SoftDebuggerSession)
                .Assembly.GetType(
                    "Mono.Debugging.Soft.ReferenceExceptionRequestInstaller");
            Assert.NotNull(installerType);
            var install = installerType!.GetMethod(
                "Install",
                BindingFlags.Static | BindingFlags.Public |
                BindingFlags.NonPublic);
            Assert.NotNull(install);
            var requests = new List<Request>();
            var enabled = new List<Request>();

            var result = (Request[])install!
                .MakeGenericMethod(typeof(Request))
                .Invoke(
                    null,
                    new object[]
                    {
                        new Func<bool, bool, Request>((caught, uncaught) =>
                        {
                            var request = new Request(caught, uncaught);
                            requests.Add(request);
                            return request;
                        }),
                        new Action<Request>(request => enabled.Add(request)),
                    })!;

            Assert.Equal(2, requests.Count);
            Assert.False(requests[0].Caught);
            Assert.True(requests[0].Uncaught);
            Assert.True(requests[1].Caught);
            Assert.False(requests[1].Uncaught);
            Assert.Equal(requests, enabled);
            Assert.Equal(requests, result);
        }

        private sealed class Request
        {
            public Request(bool caught, bool uncaught)
            {
                Caught = caught;
                Uncaught = uncaught;
            }

            public bool Caught { get; }
            public bool Uncaught { get; }
        }
    }
}
