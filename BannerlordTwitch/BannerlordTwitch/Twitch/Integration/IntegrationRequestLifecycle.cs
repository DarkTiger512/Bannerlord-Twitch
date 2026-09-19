using System;
using System.Collections.Generic;
using System.Threading;

namespace BannerlordTwitch.Integration
{
    public sealed class IntegrationRequestLifecycle : IDisposable
    {
        private readonly object gate = new();
        private readonly Dictionary<Guid, CancellationTokenSource> pending = new();
        private readonly HashSet<Guid> terminal = new();
        private bool disposed;

        public bool IsPending(Guid requestId)
        {
            lock (gate) return !disposed && pending.ContainsKey(requestId);
        }

        public bool TryAccept(Guid requestId, out CancellationToken timeoutToken)
        {
            lock (gate)
            {
                timeoutToken = default;
                if (disposed || terminal.Contains(requestId) || pending.ContainsKey(requestId)) return false;
                var source = new CancellationTokenSource();
                pending.Add(requestId, source);
                timeoutToken = source.Token;
                return true;
            }
        }

        public bool TryComplete(Guid requestId)
        {
            lock (gate)
            {
                if (disposed || !terminal.Add(requestId)) return false;
                CancelPending(requestId);
                return true;
            }
        }

        public bool TryExpire(Guid requestId)
        {
            lock (gate)
            {
                if (disposed || !pending.TryGetValue(requestId, out var source)) return false;
                pending.Remove(requestId);
                source.Dispose();
                return terminal.Add(requestId);
            }
        }

        public void Forget(Guid requestId) { lock (gate) terminal.Remove(requestId); }

        private void CancelPending(Guid requestId)
        {
            if (!pending.TryGetValue(requestId, out var source)) return;
            pending.Remove(requestId);
            source.Cancel();
            source.Dispose();
        }

        public void Dispose()
        {
            lock (gate)
            {
                if (disposed) return;
                disposed = true;
                foreach (var source in pending.Values) { source.Cancel(); source.Dispose(); }
                pending.Clear();
                terminal.Clear();
            }
        }
    }
}
