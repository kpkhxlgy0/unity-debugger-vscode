using System;
using System.Threading;
using System.Threading.Tasks;

namespace UnityDebugger.Adapter.Backend
{
    internal sealed class ReconnectController
    {
        private const int MaximumAttempts = 40;
        private static readonly TimeSpan RetryDelay =
            TimeSpan.FromMilliseconds(250);

        public async Task<bool> TryReconnect(
            Func<bool> processIsAlive,
            Func<bool> attempt,
            Func<TimeSpan, CancellationToken, Task> delay,
            CancellationToken cancellationToken)
        {
            if (processIsAlive == null)
                throw new ArgumentNullException(nameof(processIsAlive));
            if (attempt == null)
                throw new ArgumentNullException(nameof(attempt));
            if (delay == null)
                throw new ArgumentNullException(nameof(delay));

            for (
                var attemptIndex = 0;
                attemptIndex < MaximumAttempts;
                attemptIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!processIsAlive())
                    return false;
                if (attempt())
                    return true;
                if (attemptIndex == MaximumAttempts - 1)
                    break;
                await delay(RetryDelay, cancellationToken)
                    .ConfigureAwait(false);
            }
            return false;
        }
    }
}
