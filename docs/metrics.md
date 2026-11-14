# Metrics

OpButton collects time-series performance metrics from your app and displays them as charts on the dashboard.

## Configuration

```csharp
var metricConfig = new OpButtonMetricConfigBuilder()
    .SetEnabled(true)                // default: true
    .SetSampleIntervalMs(200)        // 100–1000 ms (default: 200)
    .SetBufferSize(2000)             // max buffered samples (default: 2000)
    .SetBatchSize(200)               // samples per flush (default: 200)
    .AddCollector(myCollector);

var config = new OpButtonConfig.Builder(url, token)
    .SetMetricConfig(metricConfig)
    .Build();
```

## Built-in metric types

| Type | Unit | Aggregation | Description |
|------|------|-------------|-------------|
| `FPS` | fps | Gauge | Frames per second |
| `FrameTime` | ms | Gauge | Frame duration |
| `Memory` | bytes | Gauge | Total allocated memory |
| `VideoMemory` | bytes | Gauge | GPU driver allocated memory |
| `CpuUsage` | % | Gauge | CPU utilization |
| `NetworkRx` | bytes/s | Rate | Network received |
| `NetworkTx` | bytes/s | Rate | Network transmitted |
| `BatteryLevel` | % | Gauge | Battery charge |
| `BatteryCharging` | — | Gauge | Charging state |
| `DrawCalls` | — | Gauge | Render draw calls |
| `ThreadCount` | — | Gauge | Active threads |

## Unity built-in collectors

The Unity SDK automatically provides:

| Metric | Source |
|--------|--------|
| FPS | `1 / Time.unscaledDeltaTime` |
| Frame Time | `Time.unscaledDeltaTime * 1000` |
| Memory | `Profiler.GetTotalAllocatedMemoryLong()` |
| Video Memory | `Profiler.GetAllocatedMemoryForGraphicsDriver()` |
| Battery Level | `SystemInfo.batteryLevel * 100` |
| Battery Charging | `SystemInfo.batteryStatus == Charging` |

## Custom metric types

```csharp
var loadTime = OpButtonMetricType.Create("scene_load_time");
var loadTimeMs = OpButtonMetricType.Create("scene_load_time", "ms");
var errors = OpButtonMetricType.Create("error_count", "errors", OpButtonMetricAggregation.Counter);
```

### Aggregation modes

| Mode | Behaviour |
|------|-----------|
| `Gauge` | Last value in the window (FPS, memory) |
| `Counter` | Sum of values (error counts, events) |
| `Rate` | Average over the window (bytes/s) |

## Recording metrics

### Manual recording

```csharp
OpButtonMetrics metrics = OpButtonSDK.GetMetrics();

metrics.Record(OpButtonMetricType.FPS, 60.0);
metrics.Record(OpButtonMetricType.Memory, Profiler.GetTotalAllocatedMemoryLong());
metrics.Record(myCustomType, 42.0, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
```

### Collectors

Collectors are called automatically on each sample interval:

```csharp
public class GameMetricCollector : IOpButtonMetricCollector
{
    public void Collect(List<OpButtonMetricEntry> output)
    {
        output.Add(new OpButtonMetricEntry(OpButtonMetricType.DrawCalls, GetDrawCalls()));
        output.Add(new OpButtonMetricEntry(
            OpButtonMetricType.Create("entities"), GetEntityCount()));
    }
}

// Add via config
new OpButtonMetricConfigBuilder().AddCollector(new GameMetricCollector());

// Or at runtime
OpButtonSDK.GetMetrics().AddCollector(new GameMetricCollector());
OpButtonSDK.GetMetrics().RemoveCollector(collector);
```

## Runtime adjustments

```csharp
OpButtonMetrics metrics = OpButtonSDK.GetMetrics();

metrics.SetSampleIntervalMs(500);  // slow down sampling
metrics.SetBufferSize(5000);       // increase buffer
metrics.SetBatchSize(500);         // larger batches
```

---

**See also:** [Unity Integration](unity-integration.md) · [Widgets](widgets.md) · [Logging & Events](logging.md)
