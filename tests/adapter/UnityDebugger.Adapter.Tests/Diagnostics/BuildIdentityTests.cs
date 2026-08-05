using System.IO;
using UnityDebugger.Adapter.Diagnostics;
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
                    "0.3.0+gunknown",
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
                    "0.3.0+gunknown",
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
                    "{\"version\":\"0.3.0\"," +
                    "\"commit\":\"0123456789abcdef0123456789abcdef01234567\"," +
                    "\"buildId\":\"0.3.0+g0123456789ab\"}");

                Assert.Equal(
                    "0.3.0+g0123456789ab",
                    BuildIdentity.ReadFromDirectory(directory.Path));
            }
        }

        private sealed class TemporaryDirectory : System.IDisposable
        {
            public TemporaryDirectory()
            {
                Path = System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(),
                    "unity-debugger-pure-build-identity-" +
                    System.Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(Path);
            }

            public string Path { get; }

            public void Dispose()
            {
                Directory.Delete(Path, true);
            }
        }
    }
}
