using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityDebugger.Adapter.Backend;
using Xunit;

namespace UnityDebugger.Adapter.Tests.Backend
{
    public sealed class AssemblyReloadCoordinatorTests
    {
        [Fact]
        public async Task Coalesces_unloads_and_restarts_load_quiet_window()
        {
            var delays = new List<PendingDelay>();
            using (var coordinator = new AssemblyReloadCoordinator(
                (duration, token) =>
                {
                    var delay = new PendingDelay(duration, token);
                    delays.Add(delay);
                    return delay.Completion.Task;
                }))
            {
                var starts = 0;
                var progress = 0;
                var completions = 0;
                var completed =
                    new TaskCompletionSource<bool>(
                        TaskCreationOptions.RunContinuationsAsynchronously);
                coordinator.ReloadStarted += (_, __) => starts++;
                coordinator.AssemblyLoaded += (_, __) => progress++;
                coordinator.ReloadCompleted += (_, __) =>
                {
                    completions++;
                    completed.TrySetResult(true);
                };

                coordinator.OnAssemblyUnloaded();
                coordinator.OnAssemblyUnloaded();
                Assert.Equal(1, starts);

                coordinator.OnAssemblyLoaded();
                Assert.Single(delays);
                Assert.Equal(
                    TimeSpan.FromMilliseconds(500),
                    delays[0].Duration);
                coordinator.OnAssemblyLoaded();
                Assert.Equal(2, delays.Count);
                Assert.True(delays[0].Token.IsCancellationRequested);
                Assert.Equal(2, progress);

                delays[0].Completion.TrySetResult(true);
                await DrainAsync();
                Assert.Equal(0, completions);

                delays[1].Completion.TrySetResult(true);
                var winner = await Task.WhenAny(
                    completed.Task,
                    Task.Delay(TimeSpan.FromSeconds(2)));
                Assert.Same(completed.Task, winner);
                Assert.Equal(1, completions);
            }
        }

        [Fact]
        public async Task Disconnect_cancels_pending_completion()
        {
            PendingDelay? pending = null;
            using (var coordinator = new AssemblyReloadCoordinator(
                (duration, token) =>
                {
                    pending = new PendingDelay(duration, token);
                    return pending.Completion.Task;
                }))
            {
                var completions = 0;
                coordinator.ReloadCompleted += (_, __) => completions++;
                coordinator.OnAssemblyUnloaded();
                coordinator.OnAssemblyLoaded();

                coordinator.Disconnect();
                Assert.True(pending!.Token.IsCancellationRequested);
                pending.Completion.TrySetResult(true);
                await DrainAsync();

                Assert.Equal(0, completions);
            }
        }

        private static async Task DrainAsync()
        {
            await Task.Yield();
            await Task.Yield();
            await Task.Yield();
        }

        private sealed class PendingDelay
        {
            public PendingDelay(
                TimeSpan duration,
                CancellationToken token)
            {
                Duration = duration;
                Token = token;
            }

            public TimeSpan Duration { get; }
            public CancellationToken Token { get; }
            public TaskCompletionSource<bool> Completion { get; } =
                new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }
}
