namespace OpButton.SDK
{
    /// <summary>
    /// Configuration for OpButton SDK initialization.
    /// </summary>
    public sealed class OpButtonConfig
    {
        internal readonly string BaseUrl;
        internal readonly string ApiToken;
        internal readonly string DeviceId;
        internal readonly string DeviceName;
        internal readonly long PollIntervalMs;
        internal readonly long ActionPollIntervalMs;
        internal readonly bool AutoConnect;
        internal readonly IOpButtonLoggerListener LoggerListener;
        internal readonly IOpButtonSessionListener SessionListener;
        internal readonly IOpButtonActionListener ActionListener;
        internal readonly OpButtonSessionAttributeBuilder SessionAttributes;
        internal readonly OpButtonLogConfigBuilder LogConfig;
        internal readonly OpButtonMetricConfigBuilder MetricConfig;

        private OpButtonConfig(Builder builder)
        {
            BaseUrl = builder.m_baseUrl;
            ApiToken = builder.m_apiToken;
            DeviceId = builder.m_deviceId;
            DeviceName = builder.m_deviceName;
            PollIntervalMs = builder.m_pollIntervalMs;
            ActionPollIntervalMs = builder.m_actionPollIntervalMs;
            AutoConnect = builder.m_autoConnect;
            LoggerListener = builder.m_loggerListener;
            SessionListener = builder.m_sessionListener;
            ActionListener = builder.m_actionListener;
            SessionAttributes = builder.m_sessionAttributes;
            LogConfig = builder.m_logConfig;
            MetricConfig = builder.m_metricConfig;
        }

        public string GetBaseUrl() => BaseUrl;
        public string GetApiToken() => ApiToken;
        public string GetDeviceId() => DeviceId;
        public string GetDeviceName() => DeviceName;

        public sealed class Builder
        {
            internal string m_baseUrl;
            internal string m_apiToken;
            internal string m_deviceId;
            internal string m_deviceName;
            internal long m_pollIntervalMs = 10000;
            internal long m_actionPollIntervalMs = 2000;
            internal bool m_autoConnect = true;
            internal IOpButtonLoggerListener m_loggerListener;
            internal IOpButtonSessionListener m_sessionListener;
            internal IOpButtonActionListener m_actionListener;
            internal OpButtonSessionAttributeBuilder m_sessionAttributes = new OpButtonSessionAttributeBuilder();
            internal OpButtonLogConfigBuilder m_logConfig = new OpButtonLogConfigBuilder();
            internal OpButtonMetricConfigBuilder m_metricConfig = new OpButtonMetricConfigBuilder();

            /// <param name="baseUrl">OpButton server URL, e.g. "https://opbutton.example.com"</param>
            /// <param name="apiToken">Project API token starting with "opbutton_"</param>
            public Builder(string baseUrl, string apiToken)
            {
                m_baseUrl = baseUrl.EndsWith("/") ? baseUrl.Substring(0, baseUrl.Length - 1) : baseUrl;
                m_apiToken = apiToken;
            }

            public Builder SetDeviceId(string deviceId) { m_deviceId = deviceId; return this; }
            public Builder SetDeviceName(string deviceName) { m_deviceName = deviceName; return this; }
            public Builder SetPollIntervalMs(long ms) { m_pollIntervalMs = ms; return this; }
            public Builder SetActionPollIntervalMs(long ms) { m_actionPollIntervalMs = ms; return this; }
            public Builder SetAutoConnect(bool autoConnect) { m_autoConnect = autoConnect; return this; }
            public Builder SetLoggerListener(IOpButtonLoggerListener listener) { m_loggerListener = listener; return this; }
            public Builder SetSessionListener(IOpButtonSessionListener listener) { m_sessionListener = listener; return this; }
            public Builder SetActionListener(IOpButtonActionListener listener) { m_actionListener = listener; return this; }
            public Builder SetSessionAttributes(OpButtonSessionAttributeBuilder builder) { m_sessionAttributes = builder; return this; }
            public Builder SetLogConfig(OpButtonLogConfigBuilder config) { m_logConfig = config; return this; }
            public Builder SetMetricConfig(OpButtonMetricConfigBuilder config) { m_metricConfig = config; return this; }

            public OpButtonConfig Build()
            {
                if (string.IsNullOrEmpty(m_baseUrl))
                    throw new System.ArgumentException("baseUrl is required");
                if (string.IsNullOrEmpty(m_apiToken))
                    throw new System.ArgumentException("apiToken is required");
                return new OpButtonConfig(this);
            }
        }
    }
}
