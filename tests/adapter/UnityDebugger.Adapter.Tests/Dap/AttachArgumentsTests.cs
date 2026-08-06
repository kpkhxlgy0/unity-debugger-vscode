using System;
using System.IO;
using System.Net;
using Newtonsoft.Json.Linq;
using UnityDebugger.Adapter.Dap;
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
        [InlineData("192.168.1.10")]
        [InlineData("8.8.8.8")]
        [InlineData("0.0.0.0")]
        public void Parse_rejects_non_loopback_hosts(string host)
        {
            var json = ValidArguments();
            json["__host"] = host;

            var error = Assert.Throws<AttachArgumentException>(
                () => AttachArguments.Parse(json));

            Assert.Contains(
                "loopback",
                error.Message,
                StringComparison.OrdinalIgnoreCase);
        }

        [Theory]
        [InlineData("__processId", 0)]
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
                "outside the version 0.4.0 compatibility policy",
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

        private static JObject ValidArguments()
        {
            return JObject.Parse(@"{
              '__processId': 1234,
              '__host': '127.0.0.1',
              '__port': 56234,
              '__workspaceRoot': 'H:\\fixture',
              '__projectVersion': '2022.3.62t11'
            }");
        }
    }
}
