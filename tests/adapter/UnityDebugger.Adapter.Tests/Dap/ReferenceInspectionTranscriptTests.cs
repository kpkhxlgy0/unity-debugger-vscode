using System;
using Newtonsoft.Json.Linq;
using UnityDebugger.Adapter.Backend;
using UnityDebugger.Adapter.Dap;
using UnityDebugger.Adapter.Tests.Fakes;
using Xunit;

namespace UnityDebugger.Adapter.Tests.Dap
{
    public sealed class ReferenceInspectionTranscriptTests
    {
        [Fact]
        public void MissingPropertyHandleReturnsEmptyVariablesSuccess()
        {
            var backend = new FakeDebuggerBackend();

            var messages = RunAttached(
                backend,
                DapTestProtocol.Request(
                    "variables",
                    new { variablesReference = 404 }));

            var response = DapTestProtocol.Response(messages, "variables");
            Assert.True(response["success"]!.Value<bool>());
            Assert.Empty(response["body"]!["variables"]!);
            Assert.Equal(1, backend.VariablesCount);
        }

        [Fact]
        public void MissingFrameReturnsEmptyScopesAndEvaluateSuccess()
        {
            var backend = new FakeDebuggerBackend
            {
                EvaluationResult = null,
            };

            var messages = RunAttached(
                backend,
                DapTestProtocol.Request("scopes", new { frameId = 404 }),
                DapTestProtocol.Request(
                    "evaluate",
                    new
                    {
                        frameId = 404,
                        expression = "value",
                        context = "hover",
                    }));

            var scopes = DapTestProtocol.Response(messages, "scopes");
            Assert.True(scopes["success"]!.Value<bool>());
            Assert.Empty(scopes["body"]!["scopes"]!);
            var evaluate = DapTestProtocol.Response(messages, "evaluate");
            Assert.True(evaluate["success"]!.Value<bool>());
            Assert.True(
                evaluate["body"] is null || !evaluate["body"]!.HasValues);
        }

        [Fact]
        public void HoverEnumComparisonReturnsTrue()
        {
            var backend = new FakeDebuggerBackend
            {
                EvaluationResult = new BackendEvaluationResult(
                    "true",
                    "System.Boolean",
                    0),
            };

            var messages = RunAttached(
                backend,
                DapTestProtocol.Request(
                    "evaluate",
                    new
                    {
                        frameId = 1,
                        expression =
                            "currentState == ButtonState.Normal",
                        context = "hover",
                    }));

            var response = DapTestProtocol.Response(messages, "evaluate");
            Assert.Equal(
                "true",
                response["body"]!["result"]!.Value<string>());
            Assert.Equal(
                BackendEvaluationMode.Explicit,
                backend.LastEvaluationMode);
            Assert.Equal(10000, backend.LastEvaluateTimeoutMilliseconds);
        }

        [Fact]
        public void VariablesTimeoutReturnsEmptyVariablesWithoutTerminatingSession()
        {
            var backend = new FakeDebuggerBackend
            {
                VariablesException = new OperationCanceledException(),
            };
            backend.Threads.Add(new BackendThread(1, "Main Thread"));

            var messages = RunAttached(
                backend,
                DapTestProtocol.Request(
                    "variables",
                    new { variablesReference = 1, timeout = 1 }),
                DapTestProtocol.Request("threads", new { }));

            var variables = DapTestProtocol.Response(messages, "variables");
            Assert.True(variables["success"]!.Value<bool>());
            Assert.Empty(variables["body"]!["variables"]!);
            Assert.Equal(1, backend.LastVariablesTimeoutMilliseconds);
            var threads = DapTestProtocol.Response(messages, "threads");
            Assert.True(threads["success"]!.Value<bool>());
            Assert.Equal(
                "Main Thread",
                threads["body"]!["threads"]![0]!["name"]!.Value<string>());
        }

        [Fact]
        public void DisabledImplicitEvaluationKeepsOnlyAutomaticInspectionSafe()
        {
            var backend = new FakeDebuggerBackend();

            RunAttached(
                backend,
                false,
                DapTestProtocol.Request("scopes", new { frameId = 1 }),
                DapTestProtocol.Request(
                    "variables",
                    new { variablesReference = 1 }),
                DapTestProtocol.Request(
                    "evaluate",
                    new
                    {
                        frameId = 1,
                        expression = "player.Health",
                        context = "hover",
                    }));

            Assert.Equal(BackendEvaluationMode.Safe, backend.LastScopesMode);
            Assert.Equal(BackendEvaluationMode.Safe, backend.LastVariablesMode);
            Assert.Equal(
                BackendEvaluationMode.Safe,
                backend.LastEvaluationMode);

            RunAttached(
                backend,
                false,
                DapTestProtocol.Request(
                    "evaluate",
                    new
                    {
                        frameId = 1,
                        expression = "player.Health",
                        context = "watch",
                    }),
                DapTestProtocol.Request(
                    "evaluate",
                    new
                    {
                        frameId = 1,
                        expression = "player.Health",
                        context = "repl",
                    }));

            Assert.Equal(
                BackendEvaluationMode.Explicit,
                backend.LastEvaluationMode);
        }

        [Fact]
        public void SetVariableMapsValueTypeAndExpandableReference()
        {
            var backend = new FakeDebuggerBackend
            {
                SetVariableResult = new BackendSetVariableResult(
                    "43",
                    "System.Int32",
                    44),
            };

            var messages = RunAttached(
                backend,
                DapTestProtocol.Request(
                    "setVariable",
                    new
                    {
                        variablesReference = 7,
                        name = "health",
                        value = "43",
                    }));

            var response = DapTestProtocol.Response(messages, "setVariable");
            Assert.True(response["success"]!.Value<bool>());
            Assert.Equal("43", response["body"]!["value"]!.Value<string>());
            Assert.Equal(
                "System.Int32",
                response["body"]!["type"]!.Value<string>());
            Assert.Equal(
                44,
                response["body"]!["variablesReference"]!.Value<int>());
            Assert.Equal(7, backend.LastSetVariableReference);
            Assert.Equal("health", backend.LastSetVariableName);
            Assert.Equal("43", backend.LastSetVariableExpression);
            Assert.Equal(10000, backend.LastSetVariableTimeoutMilliseconds);
        }

        private static System.Collections.Generic.IReadOnlyList<JObject>
            RunAttached(
                FakeDebuggerBackend backend,
                params JObject[] requests) =>
            RunAttached(backend, true, requests);

        private static System.Collections.Generic.IReadOnlyList<JObject>
            RunAttached(
                FakeDebuggerBackend backend,
                bool enableImplicitEvaluation,
                params JObject[] requests)
        {
            var session = new UnityDebugSession(() => backend);
            var all = new JObject[requests.Length + 2];
            all[0] = DapTestProtocol.Request(
                "initialize",
                new
                {
                    linesStartAt1 = true,
                    pathFormat = "path",
                });
            all[1] = DapTestProtocol.Request(
                "attach",
                new
                {
                    __processId = 1234,
                    __host = "127.0.0.1",
                    __port = 56234,
                    __workspaceRoot = @"H:\fixture",
                    __projectVersion = "2022.3.62t11",
                    __enableImplicitEvaluation = enableImplicitEvaluation,
                });
            Array.Copy(requests, 0, all, 2, requests.Length);
            return DapTestProtocol.Run(session, all);
        }
    }
}
