using System;
using UnityDebugger.Adapter.Diagnostics;
using Xunit;

namespace UnityDebugger.Adapter.Tests.Diagnostics
{
    public sealed class PathRedactorTests
    {
        [Fact]
        public void Redact_removes_home_and_workspace_paths()
        {
            var redactor = new PathRedactor(
                @"C:\Users\alice",
                @"H:\secret-project");
            var text = redactor.Redact(
                @"path=C:\Users\alice\a.cs " +
                @"workspace=H:\secret-project\Assets\a.cs");

            Assert.DoesNotContain(
                "alice",
                text,
                StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(
                "secret-project",
                text,
                StringComparison.OrdinalIgnoreCase);
            Assert.Contains("<user-home>", text);
            Assert.Contains("<workspace>", text);
        }

        [Fact]
        public void Redact_is_case_insensitive_and_normalizes_slashes()
        {
            var redactor = new PathRedactor(
                @"C:\Users\Alice",
                @"H:\Work\Game");

            var text = redactor.Redact(
                "h:/work/game/Assets/Player.cs");

            Assert.Equal("<workspace>/Assets/Player.cs", text);
        }
    }
}
