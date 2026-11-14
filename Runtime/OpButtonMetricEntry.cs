using System;
using System.Collections.Generic;

namespace OpButton.SDK
{
    /// <summary>
    /// A single metric sample with a type, value, and timestamp.
    /// </summary>
    public sealed class OpButtonMetricEntry
    {
        public OpButtonMetricType Type { get; }
        public double Value { get; }
        public long TimestampMs { get; }

        public OpButtonMetricEntry(OpButtonMetricType type, double value)
        {
            Type = type;
            Value = value;
            TimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        public OpButtonMetricEntry(OpButtonMetricType type, double value, long timestampMs)
        {
            Type = type;
            Value = value;
            TimestampMs = timestampMs;
        }

        internal Dictionary<string, object> ToDict()
        {
            return new Dictionary<string, object>
            {
                ["metric_type"] = Type.Key,
                ["value"] = Value,
                ["client_timestamp"] = TimestampMs,
                ["aggregation"] = Type.Aggregation.ToKey()
            };
        }
    }
}
