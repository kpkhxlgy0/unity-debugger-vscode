using System.Linq;
using Newtonsoft.Json.Linq;
using UnityDebugger.Adapter.Backend;
using UnityDebugger.Adapter.Dap;
using UnityDebugger.Adapter.Tests.Fakes;
using Xunit;

namespace UnityDebugger.Adapter.Tests.Dap
{
    public sealed class ExceptionBreakpointTests
    {
        [Fact]
        public void Initialize_advertises_supported_exception_filters()
        {
            var messages = DapTestProtocol.Run(
                new UnityDebugSession(() => new FakeDebuggerBackend()),
                DapTestProtocol.Request(
                    "initialize",
                    new
                    {
                        linesStartAt1 = true,
                        pathFormat = "path",
                    }));

            var response = DapTestProtocol.Response(
                messages,
                "initialize");
            Assert.True(DapTestProtocol.Required<bool>(
                response.SelectToken("body.supportsExceptionOptions")));
            Assert.Equal(
                new[] { "all", "uncaught" },
                response
                    .SelectTokens(
                        "body.exceptionBreakpointFilters[*].filter")
                    .Select(item => item.Value<string>())
                    .ToArray());
            Assert.Equal(
                new[] { "All Exceptions", "User-Unhandled Exceptions" },
                response
                    .SelectTokens(
                        "body.exceptionBreakpointFilters[*].label")
                    .Select(item => item.Value<string>())
                    .ToArray());
            Assert.All(
                response.SelectTokens(
                    "body.exceptionBreakpointFilters[*].default"),
                item => Assert.False(item.Value<bool>()));
        }

        [Fact]
        public void Known_filters_map_to_exact_backend_modes()
        {
            var backend = new FakeDebuggerBackend();
            var session = AttachedSession(backend);

            var messages = DapTestProtocol.Run(
                session,
                DapTestProtocol.Request(
                    "setExceptionBreakpoints",
                    new { filters = new[] { "all" } }),
                DapTestProtocol.Request(
                    "setExceptionBreakpoints",
                    new { filters = new[] { "uncaught" } }),
                DapTestProtocol.Request(
                    "setExceptionBreakpoints",
                    new { filters = new string[0] }));

            Assert.All(
                DapTestProtocol.Responses(
                    messages,
                    "setExceptionBreakpoints"),
                response => Assert.True(
                    DapTestProtocol.Required<bool>(
                        response["success"])));
            Assert.Equal(
                new[]
                {
                    ExceptionBreakMode.All,
                    ExceptionBreakMode.Uncaught,
                    ExceptionBreakMode.None,
                },
                backend.ExceptionModes);
        }

        [Fact]
        public void Unknown_filter_fails_without_reconfiguring_backend()
        {
            var backend = new FakeDebuggerBackend();
            var session = AttachedSession(backend);

            var messages = DapTestProtocol.Run(
                session,
                DapTestProtocol.Request(
                    "setExceptionBreakpoints",
                    new { filters = new[] { "SECRET_UNKNOWN_FILTER" } }));

            var response = DapTestProtocol.Response(
                messages,
                "setExceptionBreakpoints");
            Assert.False(DapTestProtocol.Required<bool>(
                response["success"]));
            Assert.Contains(
                "Unknown exception breakpoint filter",
                DapTestProtocol.Required<string>(response["message"]));
            Assert.Empty(backend.ExceptionModes);
        }

        private static UnityDebugSession AttachedSession(
            FakeDebuggerBackend backend)
        {
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
                    }));
            return session;
        }
    }
}
