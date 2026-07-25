using System;
using System.Threading;
using System.Threading.Tasks;
using UnityDebugger.Adapter.Backend;
using Xunit;

namespace UnityDebugger.Adapter.Tests.Backend
{
    public sealed class ReconnectControllerTests
    {
        [Fact]
        public async Task Succeeds_immediately_without_delay()
        {
            var attempts = 0;
            var delays = 0;
            var controller = new ReconnectController();

            var result = await controller.TryReconnect(
                () => true,
                () =>
                {
                    attempts++;
                    return true;
                },
                (_, __) =>
                {
                    delays++;
                    return Task.CompletedTask;
                },
                CancellationToken.None);

            Assert.True(result);
            Assert.Equal(1, attempts);
            Assert.Equal(0, delays);
        }

        [Fact]
        public async Task Stops_after_40_attempts_at_250_milliseconds()
        {
            var attempts = 0;
            var delays = 0;
            var controller = new ReconnectController();

            var result = await controller.TryReconnect(
                () => true,
                () =>
                {
                    attempts++;
                    return false;
                },
                (duration, _) =>
                {
                    Assert.Equal(
                        TimeSpan.FromMilliseconds(250),
                        duration);
                    delays++;
                    return Task.CompletedTask;
                },
                CancellationToken.None);

            Assert.False(result);
            Assert.Equal(40, attempts);
            Assert.Equal(39, delays);
        }

        [Fact]
        public async Task Process_exit_stops_before_another_attempt()
        {
            var alive = true;
            var attempts = 0;
            var controller = new ReconnectController();

            var result = await controller.TryReconnect(
                () => alive,
                () =>
                {
                    attempts++;
                    return false;
                },
                (_, __) =>
                {
                    alive = false;
                    return Task.CompletedTask;
                },
                CancellationToken.None);

            Assert.False(result);
            Assert.Equal(1, attempts);
        }

        [Fact]
        public async Task Explicit_disconnect_cancels_immediately()
        {
            using (var cancellation = new CancellationTokenSource())
            {
                var controller = new ReconnectController();
                var task = controller.TryReconnect(
                    () => true,
                    () => false,
                    (_, token) => Task.Delay(
                        Timeout.InfiniteTimeSpan,
                        token),
                    cancellation.Token);

                cancellation.Cancel();

                await Assert.ThrowsAnyAsync<OperationCanceledException>(
                    () => task);
            }
        }
    }
}
