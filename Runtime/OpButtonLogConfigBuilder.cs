namespace OpButton.SDK
{
    /// <summary>
    /// Builder for log subsystem configuration.
    /// </summary>
    public sealed class OpButtonLogConfigBuilder
    {
        private bool m_enabled = true;
        private OpButtonLogLevel m_logLevel = OpButtonLogLevel.Info;
        private int m_batchSize = 100;
        private int m_bufferSize = 1000;
        private OpButtonLogAttributeBuilder m_attributes = new OpButtonLogAttributeBuilder();

        public OpButtonLogConfigBuilder SetEnabled(bool enabled) { m_enabled = enabled; return this; }
        public OpButtonLogConfigBuilder SetLogLevel(OpButtonLogLevel level) { m_logLevel = level; return this; }
        public OpButtonLogConfigBuilder SetBatchSize(int size) { m_batchSize = size; return this; }
        public OpButtonLogConfigBuilder SetBufferSize(int size) { m_bufferSize = size; return this; }
        public OpButtonLogConfigBuilder SetAttributes(OpButtonLogAttributeBuilder attributes) { m_attributes = attributes; return this; }

        internal bool IsEnabled() => m_enabled;
        internal OpButtonLogLevel GetLogLevel() => m_logLevel;
        internal int GetBatchSize() => m_batchSize;
        internal int GetBufferSize() => m_bufferSize;
        internal OpButtonLogAttributeBuilder GetAttributes() => m_attributes;
    }
}
