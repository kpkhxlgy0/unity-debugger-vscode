using System;
using System.Diagnostics;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityDebugger.Adapter.Backend;
using UnityDebugger.Adapter.Dap;
using UnityDebugger.Adapter.Tests.Fakes;
using Xunit;

namespace UnityDebugger.Adapter.Tests.Dap
{
    public sealed class ReferenceCapabilityTests
    {
        [Fact]
        public void InitializeMatchesReferenceDebuggerCapabilities()
        {
            var messages = DapTestProtocol.Run(
                new UnityDebugSession(() => new FakeDebuggerBackend()),
                DapTestProtocol.Request(
                    "initialize",
                    new
                    {
                        adapterID = "unity-debugger-pure",
                        linesStartAt1 = true,
                        columnsStartAt1 = true,
                        pathFormat = "path",
                    }));

            var body = DapTestProtocol.Response(
                messages,
                "initialize")["body"]!;
            Assert.True(body["supportsConditionalBreakpoints"]!.Value<bool>());
            Assert.True(body["supportsEvaluateForHovers"]!.Value<bool>());
            Assert.True(body["supportsFunctionBreakpoints"]!.Value<bool>());
            Assert.True(body["supportsLogPoints"]!.Value<bool>());
            Assert.True(body["supportsSetVariable"]!.Value<bool>());
            Assert.True(body["supportsStepInTargetsRequest"]!.Value<bool>());
            Assert.True(body["supportsGotoTargetsRequest"]!.Value<bool>());
            Assert.True(body["supportsTerminateRequest"]!.Value<bool>());
            Assert.False(body["supportsConfigurationDoneRequest"]!.Value<bool>());
            Assert.False(body["supportsDataBreakpoints"]!.Value<bool>());
            Assert.False(body["supportsDisassembleRequest"]!.Value<bool>());
            Assert.False(body["supportsInstructionBreakpoints"]!.Value<bool>());
            Assert.False(body["supportsHitConditionalBreakpoints"]!.Value<bool>());
            Assert.False(body["supportsSingleThreadExecutionRequests"]!.Value<bool>());
            Assert.False(body["supportsSteppingGranularity"]!.Value<bool>());
            Assert.False(body["supportsStepBack"]!.Value<bool>());
            Assert.False(body["supportsReadMemoryRequest"]!.Value<bool>());
            Assert.False(body["supportsRestartRequest"]!.Value<bool>());
            Assert.False(body["supportsWriteMemoryRequest"]!.Value<bool>());
        }

        [Fact]
        public void InitializeBodyMatchesObservedReference()
        {
            var messages = DapTestProtocol.Run(
                new UnityDebugSession(() => new FakeDebuggerBackend()),
                DapTestProtocol.Request(
                    "initialize",
                    new
                    {
                        adapterID = "unity-debugger-pure",
                        linesStartAt1 = true,
                        columnsStartAt1 = true,
                        pathFormat = "path",
                    }));

            var body = DapTestProtocol.Response(
                messages,
                "initialize")["body"]!;
            Assert.Null(body["supportsExceptionOptions"]);
            Assert.True(body["supportsExceptionFilterOptions"]!.Value<bool>());
            Assert.True(body["supportsExceptionConditions"]!.Value<bool>());
            Assert.False(
                body["supportsHitConditionalBreakpoints"]!.Value<bool>());

            var filters = (JArray)body["exceptionBreakpointFilters"]!;
            Assert.Equal(2, filters.Count);
            var expectedFilterFields = new[]
            {
                "conditionDescription",
                "default",
                "description",
                "filter",
                "label",
                "supportsCondition",
            };
            Assert.All(
                filters,
                filter => Assert.Equal(
                    expectedFilterFields,
                    filter.Children<JProperty>()
                        .Select(property => property.Name)
                        .OrderBy(name => name, StringComparer.Ordinal)
                        .ToArray()));

            Assert.Equal("all", filters[0]["filter"]!.Value<string>());
            Assert.Equal(
                "All Exceptions",
                filters[0]["label"]!.Value<string>());
            Assert.Equal(
                "Break on all exceptions",
                filters[0]["description"]!.Value<string>());
            Assert.True(filters[0]["supportsCondition"]!.Value<bool>());
            Assert.Equal(
                "List of exceptions ...",
                filters[0]["conditionDescription"]!.Value<string>());
            Assert.False(filters[0]["default"]!.Value<bool>());

            Assert.Equal("unhandled", filters[1]["filter"]!.Value<string>());
            Assert.Equal(
                "User-Unhandled Exceptions",
                filters[1]["label"]!.Value<string>());
            Assert.Equal(
                "Break only on unhandled exceptions",
                filters[1]["description"]!.Value<string>());
            Assert.True(filters[1]["supportsCondition"]!.Value<bool>());
            Assert.Equal(
                "List of exceptions ...",
                filters[1]["conditionDescription"]!.Value<string>());
            Assert.False(filters[1]["default"]!.Value<bool>());

            Assert.Equal(
                Process.GetCurrentProcess().Id,
                DapTestProtocol.Required<int>(body["pid"]));
        }

        [Theory]
        [InlineData("stepInTargets")]
        [InlineData("gotoTargets")]
        [InlineData("goto")]
        [InlineData("exceptionInfo")]
        public void AdvertisedRequestHookReturnsOneSuccessfulResponse(
            string command)
        {
            var backend = new FakeDebuggerBackend();
            backend.Threads.Add(new BackendThread(42, "Main Thread"));
            var session = new UnityDebugSession(() => backend);
            DapTestProtocol.Run(
                session,
                DapTestProtocol.Request(
                    "initialize",
                    new
                    {
                        linesStartAt1 = true,
                        pathFormat = "path",
                    }),
                DapTestProtocol.Request(
                    "attach",
                    new
                    {
                        __processId = 1234,
                        __host = "127.0.0.1",
                        __port = 56234,
                        __workspaceRoot = @"H:\fixture",
                        __projectVersion = "2022.3.62t11",
                    }),
                DapTestProtocol.Request("threads", new { }));
            var messages = DapTestProtocol.Run(
                session,
                Request(command));

            var response = DapTestProtocol.Response(messages, command);
            Assert.True(response["success"]!.Value<bool>());
            Assert.Single(DapTestProtocol.Responses(messages, command));
        }

        private static JObject Request(string command)
        {
            switch (command)
            {
                case "stepInTargets":
                    return DapTestProtocol.Request(
                        command,
                        new { frameId = 999 });
                case "gotoTargets":
                    return DapTestProtocol.Request(
                        command,
                        new
                        {
                            source = new
                            {
                                path = @"H:\fixture\Assets\Player.cs",
                            },
                            line = 10,
                            column = 1,
                        });
                case "goto":
                    return DapTestProtocol.Request(
                        command,
                        new { threadId = 1, targetId = 999 });
                default:
                    return DapTestProtocol.Request(
                        command,
                        new { threadId = 1 });
            }
        }
    }
}
