using System;
using System.Reflection;
using UnityDebugger.Adapter.State;
using Xunit;

namespace UnityDebugger.Adapter.Tests.State
{
    public sealed class ThreadIdMapTests
    {
        [Fact]
        public void Maps_large_backend_ids_to_stable_positive_dap_ids()
        {
            var map = new ThreadIdMap();
            var backendId = (long)int.MaxValue + 200L;

            var dapId = map.GetOrCreate(backendId);

            Assert.True(dapId > 0);
            Assert.Equal(dapId, map.GetOrCreate(backendId));
            Assert.True(map.TryGetDapId(backendId, out var forward));
            Assert.Equal(dapId, forward);
            Assert.True(map.TryGetBackendId(dapId, out var roundTrip));
            Assert.Equal(backendId, roundTrip);
        }

        [Fact]
        public void Remove_invalidates_only_the_selected_thread()
        {
            var map = new ThreadIdMap();
            var first = map.GetOrCreate(10);
            var second = map.GetOrCreate(20);

            map.Remove(10);

            Assert.False(map.TryGetBackendId(first, out _));
            Assert.True(map.TryGetBackendId(second, out var remaining));
            Assert.Equal(20, remaining);
        }

        [Fact]
        public void Reset_invalidates_all_ids_and_restarts_counter()
        {
            var map = new ThreadIdMap();
            var old = map.GetOrCreate(10);

            map.Reset();

            Assert.False(map.TryGetBackendId(old, out _));
            Assert.Equal(1, map.GetOrCreate(30));
        }

        [Fact]
        public void GetOrCreate_throws_before_integer_overflow()
        {
            var map = new ThreadIdMap();
            typeof(ThreadIdMap)
                .GetField(
                    "next",
                    BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(map, int.MaxValue);

            var error = Assert.Throws<InvalidOperationException>(
                () => map.GetOrCreate(10));

            Assert.Equal(
                "DAP thread ID space is exhausted.",
                error.Message);
        }
    }
}
