using System;
using System.IO;

namespace UnityDebugger.Adapter.Tests.Support
{
    // One copy for the whole suite. DiagnosticLogTests and BuildIdentityTests
    // each carried a near-verbatim private version of this fixture, so a fix
    // to the create or delete behaviour landed in one and not the other.
    internal sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "unity-debugger-pure-test-" +
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            Directory.Delete(Path, true);
        }
    }
}
