using System.Collections.Generic;

namespace OpButton.SDK
{
    /// <summary>
    /// Interface for custom metric collectors.
    /// Collectors are called on a background thread at the configured sample interval.
    /// </summary>
    public interface IOpButtonMetricCollector
    {
        void Collect(List<OpButtonMetricEntry> output);
    }
}
