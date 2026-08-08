using System;
using System.IO;
using UnityDebugger.Adapter.Source;
using Xunit;

namespace UnityDebugger.Adapter.Tests.Source
{
    public sealed class SourceMapperTests
    {
        [Fact]
        public void Maps_existing_source_case_insensitively()
        {
            var expected = Path.GetFullPath(
                @"H:\fixture\Assets\Player.cs");
            var mapper = new SourceMapper(
                @"H:\FIXTURE",
                path => string.Equals(
                    path,
                    expected,
                    StringComparison.OrdinalIgnoreCase));

            var source = mapper.ToClientPath(
                @"h:\fixture\Assets\Player.cs");

            Assert.True(source.Available);
            Assert.Equal(
                expected,
                source.Path!,
                ignoreCase: true);
            Assert.Equal("Player.cs", source.Name);
            Assert.Null(source.Message);
        }

        [Fact]
        public void Normalizes_forward_slashes()
        {
            var expected = Path.GetFullPath(
                @"H:\fixture\Assets\Player.cs");
            var mapper = new SourceMapper(
                @"H:\fixture",
                _ => true);

            var source = mapper.ToClientPath(
                "H:/fixture/Assets/Player.cs");

            Assert.Equal(expected, source.Path);
        }

        [Fact]
        public void Resolves_relative_runtime_source_from_workspace_root()
        {
            var expected = Path.GetFullPath(
                @"H:\fixture\FilePackages\package\Editor\Receiver.cs");
            var mapper = new SourceMapper(
                @"H:\fixture",
                path => string.Equals(
                    path,
                    expected,
                    StringComparison.OrdinalIgnoreCase));

            var source = mapper.ToClientPath(
                @".\FilePackages\package\Editor\Receiver.cs");

            Assert.True(source.Available);
            Assert.Equal(
                expected,
                source.Path!,
                ignoreCase: true);
            Assert.Equal("Receiver.cs", source.Name);
        }

        [Fact]
        public void Rejects_runtime_path_outside_workspace_without_name()
        {
            const string Secret = "SecretUserFile.cs";
            var mapper = new SourceMapper(@"H:\fixture", _ => true);

            var source = mapper.ToClientPath(
                $@"C:\Users\secret\{Secret}");

            Assert.False(source.Available);
            Assert.Null(source.Path);
            Assert.Equal("Unavailable source", source.Name);
            Assert.DoesNotContain(Secret, source.Name);
            Assert.Equal(0, source.SourceReference);
            Assert.Equal(
                "Source file is unavailable in this workspace.",
                source.Message);
        }

        [Fact]
        public void Rejects_missing_file_inside_workspace()
        {
            var mapper = new SourceMapper(
                @"H:\fixture",
                _ => false);

            var source = mapper.ToClientPath(
                @"H:\fixture\Assets\Missing.cs");

            Assert.False(source.Available);
            Assert.Null(source.Path);
            Assert.Equal("Unavailable source", source.Name);
        }

        [Fact]
        public void Rejects_prefix_collision_workspace()
        {
            var mapper = new SourceMapper(@"H:\fixture", _ => true);

            var source = mapper.ToClientPath(
                @"H:\fixture-other\Assets\Player.cs");

            Assert.False(source.Available);
            Assert.Null(source.Path);
        }
    }
}
