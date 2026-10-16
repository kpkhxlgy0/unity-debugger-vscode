using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using Newtonsoft.Json.Linq;
using UnityDebugger.Adapter.Backend;
using UnityDebugger.Adapter.Dap;
using UnityDebugger.Adapter.Tests.Backend;
using Xunit;

namespace UnityDebugger.Adapter.Tests.Dap
{
    public sealed class AttachArgumentsTests
    {
        [Fact]
        public void Parse_accepts_loopback_target()
        {
            var target = AttachArguments.Parse(ValidArguments());

            Assert.Equal(1234, target.ProcessId);
            Assert.Equal(IPAddress.Loopback, target.Address);
            Assert.Equal(56234, target.Port);
            Assert.Equal(
                Path.GetFullPath(@"H:\fixture"),
                target.WorkspaceRoot);
            Assert.Equal("2022.3.62t11", target.ProjectVersion);
        }

        [Fact]
        public void Parse_ignores_legacy_implicit_evaluation_argument()
        {
            var json = ValidArguments();
            json["__enableImplicitEvaluation"] = "true";

            var target = AttachArguments.Parse(json);

            Assert.Equal(1234, target.ProcessId);
        }

        [Theory]
        [InlineData("127.0.0.1")]
        [InlineData("192.168.1.50")]
        [InlineData("10.30.154.73")]
        public void Parse_accepts_any_address_literal_host(string host)
        {
            var target = AttachArguments.Parse(
                ValidArguments(host: host, port: 56234));

            Assert.Equal(host, target.Address.ToString());
        }

        // Deliberately stricter than the reference adapter, which answers
        // success and silently attaches to nothing when endPoint is not an
        // address literal — and stricter than the framework parser, which
        // rewrites a short or zero-padded form (1.2.3, 010.1.1.1) into a
        // different host than the user wrote.
        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("unity-box.local")]
        [InlineData("localhost")]
        [InlineData("not a host")]
        [InlineData("192.168.1.50:56234")]
        [InlineData("1.2.3")]
        [InlineData("1")]
        [InlineData("010.1.1.1")]
        [InlineData("192.168.001.001")]
        public void Parse_rejects_a_host_that_is_not_a_canonical_address_literal(
            string host)
        {
            var json = ValidArguments();
            json["__host"] = host;

            var error = Assert.Throws<AttachArgumentException>(
                () => AttachArguments.Parse(json));

            Assert.Equal(
                "__host must be an IP address literal, such as " +
                "192.168.1.50.",
                error.Message);
        }

        // The reference adapter answers an unreachable endpoint with
        // "Failed to attach debugger to <host>:<port>".
        [Theory]
        [InlineData("127.0.0.1", 1)]
        [InlineData("192.0.2.10", 56234)]
        [InlineData("198.51.100.7", 56000)]
        public void Attach_reports_the_reference_message_for_an_unreachable_endpoint(
            string host,
            int port)
        {
            var target = AttachArguments.Parse(ValidArguments(host, port));
            var facade = new FakeSoftDebuggerSessionFacade
            {
                ConnectException = new SocketException(
                    (int)SocketError.ConnectionRefused),
            };
            using (var backend = new MonoDebuggingBackend(() => facade))
            {
                var error = Assert.Throws<DebuggerBackendException>(
                    () => backend.Attach(target));

                Assert.Equal(
                    $"Failed to attach debugger to {host}:{port}",
                    error.Message);
                Assert.False(backend.IsAttached);
            }
        }

        // `__processId: 0` used to be a row here. Zero is now the "unknown
        // process id" sentinel that the explicit-endPoint and Player attach
        // paths send, so it is accepted (see
        // Parse_accepts_an_unknown_process_id). The port range is still
        // enforced, and a negative process id is still rejected.
        [Theory]
        [InlineData("__port", 0)]
        [InlineData("__port", 65536)]
        public void Parse_rejects_invalid_numeric_fields(
            string property,
            int value)
        {
            var json = ValidArguments();
            json[property] = value;

            Assert.Throws<AttachArgumentException>(
                () => AttachArguments.Parse(json));
        }

        // An explicit endPoint skips discovery and a Player target has no
        // process id on this machine, so both send 0 and the adapter must
        // accept it as "unknown".
        [Fact]
        public void Parse_accepts_an_unknown_process_id()
        {
            var json = ValidArguments();
            json["__processId"] = 0;

            var target = AttachArguments.Parse(json);

            Assert.Equal(0, target.ProcessId);
            Assert.Equal(IPAddress.Loopback, target.Address);
            Assert.Equal(56234, target.Port);
        }

        [Fact]
        public void Parse_rejects_a_negative_process_id()
        {
            var json = ValidArguments();
            json["__processId"] = -1;

            var error = Assert.Throws<AttachArgumentException>(
                () => AttachArguments.Parse(json));

            Assert.Equal("__processId must not be negative.", error.Message);
        }

        // The explicit-endPoint and Player attach paths have no Editor version
        // to send, so an empty value means "unknown" and must not block the
        // attach. Every check still applies to a real version, which the three
        // version theories around this test pin.
        [Fact]
        public void Parse_accepts_an_unknown_project_version()
        {
            var json = ValidArguments();
            json["__projectVersion"] = "";

            var target = AttachArguments.Parse(json);

            Assert.Equal(string.Empty, target.ProjectVersion);
            Assert.Equal("127.0.0.1", target.Address.ToString());
            Assert.Equal(56234, target.Port);
        }

        [Fact]
        public void Parse_rejects_empty_workspace_root()
        {
            var json = ValidArguments();
            json["__workspaceRoot"] = "";

            Assert.Throws<AttachArgumentException>(
                () => AttachArguments.Parse(json));
        }

        [Theory]
        [InlineData("2022.3.61f1")]
        [InlineData("6000.0.50f1")]
        [InlineData("6000.1.4f1")]
        public void Parse_accepts_unverified_compatible_versions(
            string version)
        {
            var json = ValidArguments();
            json["__projectVersion"] = version;

            Assert.Equal(
                version,
                AttachArguments.Parse(json).ProjectVersion);
        }

        [Theory]
        [InlineData("2021.3.45f1")]
        [InlineData("2023.2.20f1")]
        public void Parse_rejects_unsupported_versions(
            string version)
        {
            var json = ValidArguments();
            json["__projectVersion"] = version;

            var error = Assert.Throws<AttachArgumentException>(
                () => AttachArguments.Parse(json));
            Assert.Contains(
                "outside the version 0.3.2 compatibility policy",
                error.Message);
        }

        [Fact]
        public void Parse_rejects_malformed_version()
        {
            var json = ValidArguments();
            json["__projectVersion"] = "latest";

            Assert.Throws<AttachArgumentException>(
                () => AttachArguments.Parse(json));
        }

        [Fact]
        public void Parse_requires_every_internal_property()
        {
            foreach (var property in new[]
            {
                "__processId",
                "__host",
                "__port",
                "__workspaceRoot",
                "__projectVersion",
            })
            {
                var json = ValidArguments();
                json.Remove(property);
                Assert.Throws<AttachArgumentException>(
                    () => AttachArguments.Parse(json));
            }
        }

        private static JObject ValidArguments(
            string host = "127.0.0.1",
            int port = 56234)
        {
            var arguments = JObject.Parse(@"{
              '__processId': 1234,
              '__host': '127.0.0.1',
              '__port': 56234,
              '__workspaceRoot': 'H:\\fixture',
              '__projectVersion': '2022.3.62t11'
            }");
            arguments["__host"] = host;
            arguments["__port"] = port;
            return arguments;
        }
    }
}
