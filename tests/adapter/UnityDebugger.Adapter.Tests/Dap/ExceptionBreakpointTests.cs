using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityDebugger.Adapter.Backend;
using UnityDebugger.Adapter.Dap;
using UnityDebugger.Adapter.Diagnostics;
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
            Assert.Null(response.SelectToken("body.supportsExceptionOptions"));
            Assert.Equal(
                new[] { "all", "unhandled" },
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
                    new { filters = new[] { "unhandled" } }),
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
                    ExceptionBreakMode.Uncaught,
                    ExceptionBreakMode.None,
                },
                backend.ExceptionModes);
        }

        [Fact]
        public void Filter_options_map_to_exact_backend_modes()
        {
            var backend = new FakeDebuggerBackend();
            var session = AttachedSession(backend);

            var messages = DapTestProtocol.Run(
                session,
                DapTestProtocol.Request(
                    "setExceptionBreakpoints",
                    FilterOptions(new { filterId = "all" })),
                DapTestProtocol.Request(
                    "setExceptionBreakpoints",
                    FilterOptions(new
                    {
                        // A condition is carried but not interpreted: the
                        // filter still selects its own break mode.
                        filterId = "unhandled",
                        condition = "System.NullReferenceException",
                    })),
                DapTestProtocol.Request(
                    "setExceptionBreakpoints",
                    FilterOptions(
                        new { filterId = "SECRET_UNKNOWN_FILTER" })));

            var responses = DapTestProtocol.Responses(
                messages,
                "setExceptionBreakpoints");
            Assert.True(DapTestProtocol.Required<bool>(
                responses[0]["success"]));
            Assert.True(DapTestProtocol.Required<bool>(
                responses[1]["success"]));
            Assert.False(DapTestProtocol.Required<bool>(
                responses[2]["success"]));
            Assert.Contains(
                "Unknown exception breakpoint filter",
                DapTestProtocol.Required<string>(responses[2]["message"]));
            Assert.Equal(
                new[] { ExceptionBreakMode.All, ExceptionBreakMode.Uncaught },
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

        [Fact]
        public void Exception_stop_logs_only_opaque_runtime_identity()
        {
            var backend = new FakeDebuggerBackend();
            using (var writer = new StringWriter())
            using (var log = new DiagnosticLog(
                writer,
                new PathRedactor(
                    @"C:\Users\alice",
                    @"H:\secret-project")))
            {
                var session = new UnityDebugSession(() => backend, log);
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
                var stopped = new BackendStoppedEventArgs(
                    BackendStopReason.Exception,
                    42,
                    "SECRET_EXCEPTION_TEXT",
                    exceptionInfo: new BackendExceptionInfo(
                        "SECRET_EXCEPTION_TYPE",
                        "SECRET_EXCEPTION_MESSAGE",
                        "always",
                        null),
                    exceptionObjectId: 1001L,
                    exceptionRequestId: 7,
                    exceptionEventCount: 1);

                backend.RaiseStopped(stopped);

                var text = writer.ToString();
                Assert.Contains(
                    "event=debugger.exception.stop",
                    text);
                Assert.Contains("threadId=42", text);
                Assert.Contains("exceptionObjectId=1001", text);
                Assert.Contains("exceptionRequestId=7", text);
                Assert.Contains("eventCount=1", text);
                Assert.Contains("stopKind=always", text);
                Assert.DoesNotContain("SECRET", text);
            }
        }

        // Two client shapes the tests above do not reach. FilterOptions above
        // always sends `filters` as an empty array, so the branch that reads a
        // request with no `filters` member at all is never taken; and a request
        // carrying ids in both shapes is what a client with stale UI state
        // sends while the selected filters move into filterOptions. The second
        // shape is also where the precedence rule has to hold: `all` selects
        // every exception whichever array it arrives in.
        [Fact]
        public void Filter_options_alone_and_both_shapes_resolve_by_precedence()
        {
            var backend = new FakeDebuggerBackend();
            var session = AttachedSession(backend);

            var messages = DapTestProtocol.Run(
                session,
                // No `filters` member at all.
                DapTestProtocol.Request(
                    "setExceptionBreakpoints",
                    new
                    {
                        filterOptions = new[]
                        {
                            new { filterId = "unhandled" },
                        },
                    }),
                // `all` in the newer shape outranks `unhandled` in the older.
                DapTestProtocol.Request(
                    "setExceptionBreakpoints",
                    new
                    {
                        filters = new[] { "unhandled" },
                        filterOptions = new[]
                        {
                            new { filterId = "all" },
                        },
                    }),
                // `all` in the older shape outranks `unhandled` in the newer.
                DapTestProtocol.Request(
                    "setExceptionBreakpoints",
                    new
                    {
                        filters = new[] { "all" },
                        filterOptions = new[]
                        {
                            new { filterId = "unhandled" },
                        },
                    }),
                // The legacy id loses to `all` the same way.
                DapTestProtocol.Request(
                    "setExceptionBreakpoints",
                    new
                    {
                        filters = new[] { "uncaught" },
                        filterOptions = new[]
                        {
                            new { filterId = "all" },
                        },
                    }));

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
                    ExceptionBreakMode.Uncaught,
                    ExceptionBreakMode.All,
                    ExceptionBreakMode.All,
                    ExceptionBreakMode.All,
                },
                backend.ExceptionModes);
        }

        // VS Code moves the selected filters into filterOptions (with an empty
        // filters array) once supportsExceptionFilterOptions is advertised.
        private static object FilterOptions(object option) =>
            new
            {
                filters = new string[0],
                filterOptions = new[] { option },
            };

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
