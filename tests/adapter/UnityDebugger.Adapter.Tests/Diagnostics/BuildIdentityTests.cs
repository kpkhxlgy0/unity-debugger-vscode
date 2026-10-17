using System.IO;
using UnityDebugger.Adapter.Diagnostics;
using UnityDebugger.Adapter.Tests.Support;
using Xunit;

namespace UnityDebugger.Adapter.Tests.Diagnostics
{
    public sealed class BuildIdentityTests
    {
        [Fact]
        public void ReadFromDirectory_returns_unknown_when_file_is_missing()
        {
            using (var directory = new TemporaryDirectory())
            {
                Assert.Equal(
                    "0.3.2+gunknown",
                    BuildIdentity.ReadFromDirectory(directory.Path));
            }
        }

        [Fact]
        public void ReadFromDirectory_returns_unknown_when_file_is_malformed()
        {
            using (var directory = new TemporaryDirectory())
            {
                File.WriteAllText(
                    System.IO.Path.Combine(directory.Path, "build-info.json"),
                    "{not-json");

                Assert.Equal(
                    "0.3.2+gunknown",
                    BuildIdentity.ReadFromDirectory(directory.Path));
            }
        }

        [Fact]
        public void ReadFromDirectory_returns_exact_valid_build_id()
        {
            using (var directory = new TemporaryDirectory())
            {
                File.WriteAllText(
                    System.IO.Path.Combine(directory.Path, "build-info.json"),
                    "{\"version\":\"0.3.2\"," +
                    "\"commit\":\"0123456789abcdef0123456789abcdef01234567\"," +
                    "\"buildId\":\"0.3.2+g0123456789ab\"}");

                Assert.Equal(
                    "0.3.2+g0123456789ab",
                    BuildIdentity.ReadFromDirectory(directory.Path));
            }
        }
    }
}
