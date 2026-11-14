using System.Collections.Generic;

namespace OpButton.SDK
{
    /// <summary>
    /// Builder for metric subsystem configuration.
    /// </summary>
    public sealed class OpButtonMetricConfigBuilder
    {
        private bool m_enabled = true;
        private long m_sampleIntervalMs = 200;
        private int m_bufferSize = 2000;
        private int m_batchSize = 200;
        private readonly List<IOpButtonMetricCollector> m_collectors = new List<IOpButtonMetricCollector>();

        public OpButtonMetricConfigBuilder SetEnabled(bool enabled) { m_enabled = enabled; return this; }

        public OpButtonMetricConfigBuilder SetSampleIntervalMs(long ms)
        {
            m_sampleIntervalMs = System.Math.Max(100, System.Math.Min(1000, ms));
            return this;
        }

        public OpButtonMetricConfigBuilder SetBufferSize(int size) { m_bufferSize = size; return this; }
        public OpButtonMetricConfigBuilder SetBatchSize(int size) { m_batchSize = size; return this; }

        public OpButtonMetricConfigBuilder AddCollector(IOpButtonMetricCollector collector)
        {
            m_collectors.Add(collector);
            return this;
        }

        internal bool IsEnabled() => m_enabled;
        internal long GetSampleIntervalMs() => m_sampleIntervalMs;
        internal int GetBufferSize() => m_bufferSize;
        internal int GetBatchSize() => m_batchSize;
        internal List<IOpButtonMetricCollector> GetCollectors() => m_collectors;
    }
}
