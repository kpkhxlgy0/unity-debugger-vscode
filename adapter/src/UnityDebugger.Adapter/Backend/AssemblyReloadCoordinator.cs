using System;
using System.Threading;
using System.Threading.Tasks;

namespace UnityDebugger.Adapter.Backend
{
    internal sealed class AssemblyReloadCoordinator : IDisposable
    {
        private static readonly TimeSpan QuietWindow =
            TimeSpan.FromMilliseconds(500);
        private readonly object stateLock = new object();
        private readonly Func<TimeSpan, CancellationToken, Task> delay;
        private readonly CancellationTokenSource lifetime =
            new CancellationTokenSource();
        private CancellationTokenSource? quietWindow;
        private bool reloading;
        private bool disposed;

        public AssemblyReloadCoordinator(
            Func<TimeSpan, CancellationToken, Task> delay)
        {
            this.delay = delay ??
                throw new ArgumentNullException(nameof(delay));
        }

        public event EventHandler? ReloadStarted;
        public event EventHandler? AssemblyLoaded;
        public event EventHandler? ReloadCompleted;

        public void OnAssemblyUnloaded()
        {
            var raise = false;
            lock (stateLock)
            {
                ThrowIfDisposed();
                CancelQuietWindow();
                if (!reloading)
                {
                    reloading = true;
                    raise = true;
                }
            }
            if (raise)
                ReloadStarted?.Invoke(this, EventArgs.Empty);
        }

        public void OnAssemblyLoaded()
        {
            CancellationTokenSource current;
            lock (stateLock)
            {
                ThrowIfDisposed();
                if (!reloading)
                    return;
                CancelQuietWindow();
                current = CancellationTokenSource.CreateLinkedTokenSource(
                    lifetime.Token);
                quietWindow = current;
            }
            AssemblyLoaded?.Invoke(this, EventArgs.Empty);
            _ = CompleteAfterQuietWindowAsync(current);
        }

        public void Disconnect()
        {
            lock (stateLock)
            {
                if (disposed)
                    return;
                reloading = false;
                CancelQuietWindow();
                lifetime.Cancel();
            }
        }

        public void Dispose()
        {
            lock (stateLock)
            {
                if (disposed)
                    return;
                disposed = true;
                reloading = false;
                CancelQuietWindow();
                lifetime.Cancel();
                lifetime.Dispose();
            }
        }

        private async Task CompleteAfterQuietWindowAsync(
            CancellationTokenSource current)
        {
            try
            {
                await delay(QuietWindow, current.Token)
                    .ConfigureAwait(false);
                current.Token.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException)
            {
                current.Dispose();
                return;
            }

            var raise = false;
            lock (stateLock)
            {
                if (
                    !disposed &&
                    reloading &&
                    ReferenceEquals(quietWindow, current))
                {
                    quietWindow = null;
                    reloading = false;
                    raise = true;
                }
            }
            current.Dispose();
            if (raise)
                ReloadCompleted?.Invoke(this, EventArgs.Empty);
        }

        private void CancelQuietWindow()
        {
            var current = quietWindow;
            quietWindow = null;
            if (current == null)
                return;
            current.Cancel();
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
                throw new ObjectDisposedException(
                    nameof(AssemblyReloadCoordinator));
        }
    }
}
