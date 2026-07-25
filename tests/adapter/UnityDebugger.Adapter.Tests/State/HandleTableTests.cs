using System;
using System.Reflection;
using UnityDebugger.Adapter.State;
using Xunit;

namespace UnityDebugger.Adapter.Tests.State
{
    public sealed class HandleTableTests
    {
        [Fact]
        public void Reset_invalidates_every_previous_handle()
        {
            var table = new HandleTable<string>();
            var handle = table.Create("value");
            Assert.True(table.TryGet(handle, out var value));
            Assert.Equal("value", value);

            table.Reset();

            Assert.False(table.TryGet(handle, out _));
        }

        [Fact]
        public void Create_starts_at_one_and_reuses_one_after_reset()
        {
            var table = new HandleTable<string>();

            Assert.Equal(1, table.Create("first"));
            Assert.Equal(2, table.Create("second"));
            table.Reset();
            Assert.Equal(1, table.Create("third"));
        }

        [Fact]
        public void Create_throws_before_integer_overflow()
        {
            var table = new HandleTable<string>();
            typeof(HandleTable<string>)
                .GetField(
                    "next",
                    BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(table, int.MaxValue);

            var error = Assert.Throws<InvalidOperationException>(
                () => table.Create("value"));

            Assert.Equal("DAP handle space is exhausted.", error.Message);
        }
    }
}
