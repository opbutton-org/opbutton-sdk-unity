namespace OpButton.SDK
{
    public enum OpButtonMetricAggregation
    {
        Gauge,
        Counter,
        Rate
    }

    internal static class OpButtonMetricAggregationExtensions
    {
        public static string ToKey(this OpButtonMetricAggregation aggregation)
        {
            switch (aggregation)
            {
                case OpButtonMetricAggregation.Gauge: return "gauge";
                case OpButtonMetricAggregation.Counter: return "counter";
                case OpButtonMetricAggregation.Rate: return "rate";
                default: return "gauge";
            }
        }
    }
}
