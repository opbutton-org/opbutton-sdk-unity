using System;
using System.Collections.Generic;

namespace OpButton.SDK
{
    /// <summary>Owns the telemetry outbox and its delivery receipts.</summary>
    internal sealed class OpButtonTelemetry
    {
        private readonly OpButtonLogConfigBuilder m_logConfig;
        private readonly OpButtonHttpClient m_httpClient;
        private readonly OpButtonMetrics m_metrics;
        private readonly List<OpButtonLogEntry> m_logBuffer = new List<OpButtonLogEntry>();
        private readonly object m_logLock = new object();
        private bool m_logOverflowWarned;

        internal OpButtonTelemetry(OpButtonLogConfigBuilder config, OpButtonHttpClient http, OpButtonMetrics metrics)
        {
            m_logConfig = config;
            m_httpClient = http;
            m_metrics = metrics;
        }

        internal void Clear() { lock (m_logLock) { m_logBuffer.Clear(); } }
        internal Batch Capture() { return new Batch(this, DrainLogChunk(), m_metrics.Drain()); }

        internal sealed class Batch : IDisposable
        {
            internal readonly List<OpButtonLogEntry> Logs;
            internal readonly List<OpButtonMetricEntry> Metrics;
            private readonly OpButtonTelemetry m_owner;
            private readonly object m_lock = new object();
            private bool m_settled;

            internal Batch(OpButtonTelemetry owner, List<OpButtonLogEntry> logs, List<OpButtonMetricEntry> metrics)
            {
                m_owner = owner;
                Logs = logs;
                Metrics = metrics;
            }

            internal void Commit()
            {
                lock (m_lock)
                {
                    if (m_settled) return;
                    m_settled = true;
                    if (Logs.Count > 0) lock (m_owner.m_logLock) { m_owner.m_logOverflowWarned = false; }
                }
            }

            public void Dispose()
            {
                lock (m_lock)
                {
                    if (m_settled) return;
                    m_settled = true;
                    m_owner.RequeueLogs(Logs);
                    m_owner.m_metrics.Requeue(Metrics);
                }
            }
        }
        internal void BufferLog(OpButtonLogEntry entry)
        {
            lock (m_logLock)
            {
                if (m_logBuffer.Count >= m_logConfig.GetBufferSize())
                {
                    m_logBuffer.RemoveAt(0);
                    if (!m_logOverflowWarned)
                    {
                        m_logOverflowWarned = true;
                        OpButtonLog.Warn("Log buffer overflow ({0}), dropping oldest entries",
                            m_logConfig.GetBufferSize());
                    }
                }
                m_logBuffer.Add(entry);
            }
        }

        private List<OpButtonLogEntry> DrainLogChunk()
        {
            lock (m_logLock)
            {
                if (m_logBuffer.Count == 0)
                    return new List<OpButtonLogEntry>();

                int count = Math.Min(m_logBuffer.Count, m_logConfig.GetBatchSize());
                var chunk = new List<OpButtonLogEntry>(m_logBuffer.GetRange(0, count));
                m_logBuffer.RemoveRange(0, count);
                return chunk;
            }
        }

        private void RequeueLogs(List<OpButtonLogEntry> chunk)
        {
            if (chunk.Count == 0) return;
            lock (m_logLock)
            {
                m_logBuffer.InsertRange(0, chunk);
                while (m_logBuffer.Count > m_logConfig.GetBufferSize())
                    m_logBuffer.RemoveAt(m_logBuffer.Count - 1);
            }
        }

        internal void FlushLogs(string sessionToken)
        {
            var chunk = DrainLogChunk();
            if (chunk.Count == 0) return;
            try { m_httpClient.SendLogs(sessionToken, chunk); }
            catch (OpButtonException) { RequeueLogs(chunk); }
        }

        internal void FlushMetrics(string sessionToken)
        {
            var chunk = m_metrics.Drain();
            if (chunk.Count == 0) return;
            try { m_httpClient.SendMetrics(sessionToken, chunk); }
            catch (OpButtonException) { m_metrics.Requeue(chunk); }
        }

        internal Dictionary<string, object> ResolveLogAttributes()
        {
            var builder = m_logConfig.GetAttributes();
            var staticAttrs = builder.GetStaticAttributes();
            var dynamicAttrs = builder.GetDynamicAttributes();

            if (staticAttrs.Count == 0 && dynamicAttrs.Count == 0)
                return null;

            var attributes = new Dictionary<string, object>();
            foreach (var kvp in staticAttrs)
                attributes[kvp.Key] = kvp.Value;
            foreach (var kvp in dynamicAttrs)
            {
                try { attributes[kvp.Key] = kvp.Value(); }
                catch { }
            }

            return attributes.Count == 0 ? null : attributes;
        }
    }
}
