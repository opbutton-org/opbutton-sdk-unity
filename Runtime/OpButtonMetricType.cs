namespace OpButton.SDK
{
    /// <summary>
    /// Describes a type of metric that can be recorded.
    /// Use the built-in constants for standard system metrics,
    /// or create custom types for application-specific metrics.
    /// </summary>
    public sealed class OpButtonMetricType
    {
        public string Key { get; }
        public string Unit { get; }
        public OpButtonMetricAggregation Aggregation { get; }

        // Built-in metric types
        public static readonly OpButtonMetricType FPS = new OpButtonMetricType("fps", "", OpButtonMetricAggregation.Gauge);
        public static readonly OpButtonMetricType FrameTime = new OpButtonMetricType("frame_time", "ms", OpButtonMetricAggregation.Gauge);
        public static readonly OpButtonMetricType Memory = new OpButtonMetricType("memory", "bytes", OpButtonMetricAggregation.Gauge);
        public static readonly OpButtonMetricType VideoMemory = new OpButtonMetricType("video_memory", "bytes", OpButtonMetricAggregation.Gauge);
        public static readonly OpButtonMetricType CpuUsage = new OpButtonMetricType("cpu_usage", "%", OpButtonMetricAggregation.Gauge);
        public static readonly OpButtonMetricType NetworkRx = new OpButtonMetricType("network_rx", "bytes/s", OpButtonMetricAggregation.Rate);
        public static readonly OpButtonMetricType NetworkTx = new OpButtonMetricType("network_tx", "bytes/s", OpButtonMetricAggregation.Rate);
        public static readonly OpButtonMetricType BatteryLevel = new OpButtonMetricType("battery_level", "%", OpButtonMetricAggregation.Gauge);
        public static readonly OpButtonMetricType BatteryCharging = new OpButtonMetricType("battery_charging", "", OpButtonMetricAggregation.Gauge);
        public static readonly OpButtonMetricType DrawCalls = new OpButtonMetricType("draw_calls", "", OpButtonMetricAggregation.Gauge);
        public static readonly OpButtonMetricType ThreadCount = new OpButtonMetricType("thread_count", "", OpButtonMetricAggregation.Gauge);

        private OpButtonMetricType(string key, string unit, OpButtonMetricAggregation aggregation)
        {
            Key = key;
            Unit = unit;
            Aggregation = aggregation;
        }

        public static OpButtonMetricType Create(string key)
        {
            return new OpButtonMetricType(key, "", OpButtonMetricAggregation.Gauge);
        }

        public static OpButtonMetricType Create(string key, string unit)
        {
            return new OpButtonMetricType(key, unit, OpButtonMetricAggregation.Gauge);
        }

        public static OpButtonMetricType Create(string key, string unit, OpButtonMetricAggregation aggregation)
        {
            return new OpButtonMetricType(key, unit, aggregation);
        }

        public override bool Equals(object obj)
        {
            return obj is OpButtonMetricType other && Key == other.Key;
        }

        public override int GetHashCode()
        {
            return Key.GetHashCode();
        }

        public override string ToString()
        {
            return Key;
        }
    }
}
