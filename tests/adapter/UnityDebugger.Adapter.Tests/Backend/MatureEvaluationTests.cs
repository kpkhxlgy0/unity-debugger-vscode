using System.Net;
using System.Threading;
using UnityDebugger.Adapter.Backend;
using Xunit;

namespace UnityDebugger.Adapter.Tests.Backend
{
    public sealed class MatureEvaluationTests
    {
        [Fact]
        public void AllInspectionOperationsDelegateToTheMatureFacade()
        {
            var facade = new FakeSoftDebuggerSessionFacade();
            using (var backend = new MonoDebuggingBackend(() => facade))
            {
                backend.Attach(Target());

                backend.GetScopes(
                    1,
                    BackendEvaluationMode.Safe,
                    10000,
                    CancellationToken.None);
                backend.GetVariables(
                    2,
                    BackendEvaluationMode.Safe,
                    10000,
                    CancellationToken.None);
                backend.Evaluate(
                    1,
                    "value.ToString()",
                    BackendEvaluationMode.Safe,
                    10000,
                    CancellationToken.None);
                backend.SetVariable(
                    2,
                    "value",
                    "43",
                    BackendEvaluationMode.Safe,
                    10000,
                    CancellationToken.None);

                Assert.Equal(1, facade.GetScopesCount);
                Assert.Equal(1, facade.GetVariablesCount);
                Assert.Equal(1, facade.EvaluateCount);
                Assert.Equal(1, facade.SetVariableCount);
            }
        }

        private static AttachTarget Target() => new AttachTarget(
            123,
            IPAddress.Loopback,
            56000,
            @"D:\Fixture",
            "2022.3.62t12");
    }
}
