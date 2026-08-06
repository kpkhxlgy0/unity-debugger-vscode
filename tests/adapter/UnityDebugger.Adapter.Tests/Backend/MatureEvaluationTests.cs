using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using UnityDebugger.Adapter.Backend;
using Xunit;

namespace UnityDebugger.Adapter.Tests.Backend
{
    public sealed class MatureEvaluationTests
    {
        [Fact]
        public void ResolverUsesOnlyTheStoppedFramesEnclosingNamespace()
        {
            var types = new HashSet<string>(StringComparer.Ordinal)
            {
                "MyGame.Runtime.DevTools.GameRuntimeBootstrapMenu",
                "UnityEngine.RuntimeInitializeLoadType",
            };

            Assert.Equal(
                "MyGame.Runtime.DevTools.GameRuntimeBootstrapMenu",
                SoftDebuggerSessionFacade.ResolveIdentifierInFrameNamespace(
                    "MyGame.Runtime.DevTools",
                    "GameRuntimeBootstrapMenu",
                    types.Contains));
            Assert.Null(
                SoftDebuggerSessionFacade.ResolveIdentifierInFrameNamespace(
                    "MyGame.Runtime.DevTools",
                    "RuntimeInitializeLoadType",
                    types.Contains));
        }

        [Fact]
        public void UnknownIdentifierUsesTheReferenceDiagnostic()
        {
            Assert.Equal(
                "The identifier `RuntimeInitializeLoadType` is not in the scope",
                SoftDebuggerSessionFacade.NormalizeEvaluationError(
                    "Unknown identifier: RuntimeInitializeLoadType"));
        }

        [Fact]
        public void AllInspectionOperationsDelegateToTheMatureFacade()
        {
            var facade = new FakeSoftDebuggerSessionFacade();
            using (var backend = new MonoDebuggingBackend(() => facade))
            {
                backend.Attach(Target());

                backend.GetScopes(
                    1,
                    10000,
                    CancellationToken.None);
                backend.GetVariables(
                    2,
                    10000,
                    CancellationToken.None);
                backend.Evaluate(
                    1,
                    "value.ToString()",
                    10000,
                    CancellationToken.None);
                backend.SetVariable(
                    2,
                    "value",
                    "43",
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
