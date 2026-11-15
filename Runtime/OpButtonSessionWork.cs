using System;
using System.Threading;

namespace OpButton.SDK
{
    /// <summary>Owns one connection's cancellation and periodic subscriptions.</summary>
    internal sealed class OpButtonSessionWork : IDisposable
    {
        private readonly object m_lock = new object();
        private readonly CancellationTokenSource m_cancellation = new CancellationTokenSource();
        private Timer m_actions;
        private Timer m_metrics;
        private bool m_disposed;

        internal CancellationToken Cancellation => m_cancellation.Token;

        internal bool ScheduleActions(Action action, long intervalMs)
        {
            lock (m_lock)
            {
                if (m_disposed) return false;
                m_actions?.Dispose();
                m_actions = new Timer(_ => { if (!Cancellation.IsCancellationRequested) action(); }, null, 0, intervalMs);
                return true;
            }
        }

        internal bool ScheduleMetrics(Action sample, long intervalMs)
        {
            lock (m_lock)
            {
                if (m_disposed) return false;
                m_metrics?.Dispose();
                m_metrics = new Timer(_ => { if (!Cancellation.IsCancellationRequested) sample(); }, null, intervalMs, intervalMs);
                return true;
            }
        }

        internal void PauseActions()
        {
            lock (m_lock)
            {
                m_actions?.Dispose();
                m_actions = null;
            }
        }

        public void Dispose()
        {
            lock (m_lock)
            {
                if (m_disposed) return;
                m_disposed = true;
                m_actions?.Dispose();
                m_metrics?.Dispose();
                m_actions = null;
                m_metrics = null;
            }
            // Release pending host-thread requests before the host stops Update.
            // The source remains readable by callbacks already holding its token.
            m_cancellation.Cancel();
        }
    }
}
