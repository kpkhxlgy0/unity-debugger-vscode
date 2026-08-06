using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using Mono.Debugging.Backend;
using Mono.Debugging.Client;
using UnityDebugger.Adapter.Backend;
using Xunit;

namespace UnityDebugger.Adapter.Tests.Backend
{
    public sealed class MatureEvaluationTests
    {
        [Fact]
        public void ResolverUsesOnlyTheStoppedFramesEnclosingNamespace()
        {
            var loadedTypes = new HashSet<string>(StringComparer.Ordinal)
            {
                "UnityEngine.RuntimeInitializeLoadType",
            };
            var frameAssemblyTypes = new HashSet<string>(StringComparer.Ordinal)
            {
                "MyGame.Runtime.DevTools.GameRuntimeBootstrapMenu",
            };

            Assert.Equal(
                "MyGame.Runtime.DevTools.GameRuntimeBootstrapMenu",
                SoftDebuggerSessionFacade.ResolveIdentifierInFrameNamespace(
                    "MyGame.Runtime.DevTools",
                    "GameRuntimeBootstrapMenu",
                    loadedTypes.Contains,
                    frameAssemblyTypes.Contains));
            Assert.Null(
                SoftDebuggerSessionFacade.ResolveIdentifierInFrameNamespace(
                    "MyGame.Runtime.DevTools",
                    "RuntimeInitializeLoadType",
                    loadedTypes.Contains,
                    frameAssemblyTypes.Contains));
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
        public void MainThreadComponentLocalsMatchTheReferenceOrderAndNames()
        {
            var activeScene = Object("scene");
            var thisReference = Object("this");
            var thisGameObject = Object("gameObject");
            var local = Primitive("local", ObjectValueFlags.Variable);
            var parameter = Primitive("parameter", ObjectValueFlags.Parameter);

            var values = SoftDebuggerSessionFacade.ComposeFrameLocals(
                true,
                activeScene,
                thisReference,
                thisGameObject,
                new[] { local },
                new[] { parameter });

            Assert.Equal(
                new[]
                {
                    "Active scene",
                    "this",
                    "this.gameObject",
                    "local",
                    "parameter",
                },
                Array.ConvertAll(values, value => value.Name));
            Assert.Same(activeScene, values[0]);
            Assert.Same(thisReference, values[1]);
            Assert.Same(thisGameObject, values[2]);
        }

        [Fact]
        public void NonMainThreadLocalsOmitUnitySyntheticEntries()
        {
            var thisReference = Object("this");
            var local = Primitive("local", ObjectValueFlags.Variable);
            var parameter = Primitive("parameter", ObjectValueFlags.Parameter);

            var values = SoftDebuggerSessionFacade.ComposeFrameLocals(
                false,
                Object("scene"),
                thisReference,
                Object("gameObject"),
                new[] { local },
                new[] { parameter });

            Assert.Equal(
                new[] { "this", "local", "parameter" },
                Array.ConvertAll(values, value => value.Name));
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

        private static ObjectValue Object(string name) =>
            ObjectValue.CreateObject(
                null,
                new ObjectPath(name),
                name,
                "{" + name + "}",
                ObjectValueFlags.Variable,
                null);

        private static ObjectValue Primitive(
            string name,
            ObjectValueFlags flags) => ObjectValue.CreatePrimitive(
                null,
                new ObjectPath(name),
                "System.Int32",
                new EvaluationResult("1"),
                flags);
    }
}
