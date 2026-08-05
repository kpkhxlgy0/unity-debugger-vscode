using Newtonsoft.Json.Linq;
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

        [Theory]
        [InlineData("stepInTargets")]
        [InlineData("gotoTargets")]
        [InlineData("goto")]
        [InlineData("exceptionInfo")]
        public void AdvertisedRequestHookReturnsOneSuccessfulResponse(
            string command)
        {
            var messages = DapTestProtocol.Run(
                new UnityDebugSession(() => new FakeDebuggerBackend()),
                DapTestProtocol.Request(command, new { }));

            var response = DapTestProtocol.Response(messages, command);
            Assert.True(response["success"]!.Value<bool>());
            Assert.Single(DapTestProtocol.Responses(messages, command));
        }
    }
}
