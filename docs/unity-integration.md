# Unity Integration

## Requirements

- Unity 2020.3 or later
- .NET Standard 2.1 or .NET Framework 4.x

## Installation

### Unity Package Manager (Git URL)

In Unity Editor: **Window → Package Manager → + → Add package from git URL** and enter:

```
https://github.com/opbutton-org/opbutton-sdk-unity.git
```

Or add directly to `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.opbutton.sdk": "https://github.com/opbutton-org/opbutton-sdk-unity.git"
  }
}
```

### Local package

Clone or download the repository, then add as a local package:

**Window → Package Manager → + → Add package from disk** and select the `package.json` in the `opbutton-sdk-unity` folder.

## Initialization

Add a script to your first scene or use `[RuntimeInitializeOnLoadMethod]`:

```csharp
using OpButton.SDK;
using UnityEngine;

public class OpButtonSetup : MonoBehaviour
{
    [SerializeField] private string serverUrl = "https://opbutton.example.com";
    [SerializeField] private string apiToken = "opbutton_your_token";

    void Awake()
    {
        var config = new OpButtonConfig.Builder(serverUrl, apiToken)
            .SetDeviceName(SystemInfo.deviceModel)
            .SetAutoConnect(true)
            .Build();

        OpButtonSDK.Initialize(config);
    }
}
```

Or without MonoBehaviour:

```csharp
public static class OpButtonBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        var config = new OpButtonConfig.Builder("https://opbutton.example.com", "opbutton_token")
            .Build();

        OpButtonSDK.Initialize(config);
    }
}
```

## Configuration

```csharp
var sessionAttrs = new OpButtonSessionAttributeBuilder()
    .Put("app_version", Application.version)
    .Put("platform", Application.platform.ToString())
    .PutProvider("scene", () => SceneManager.GetActiveScene().name);

var logConfig = new OpButtonLogConfigBuilder()
    .SetEnabled(true)
    .SetLogLevel(OpButtonLogLevel.Info)
    .SetAttributes(new OpButtonLogAttributeBuilder()
        .Put("build", Application.version)
        .PutProvider("scene", () => SceneManager.GetActiveScene().name));

var metricConfig = new OpButtonMetricConfigBuilder()
    .SetEnabled(true)
    .SetSampleIntervalMs(200);

var config = new OpButtonConfig.Builder(url, token)
    .SetDeviceId(SystemInfo.deviceUniqueIdentifier)
    .SetDeviceName(SystemInfo.deviceModel + " (Unity " + Application.unityVersion + ")")
    .SetSessionAttributes(sessionAttrs)
    .SetLogConfig(logConfig)
    .SetMetricConfig(metricConfig)
    .SetAutoConnect(true)
    .Build();

OpButtonSDK.Initialize(config);
```

## Built-in Metrics

The Unity SDK automatically collects:

| Metric | Source |
|--------|--------|
| FPS | `1 / Time.unscaledDeltaTime` |
| Frame Time | `Time.unscaledDeltaTime * 1000` |
| Memory | `Profiler.GetTotalAllocatedMemoryLong()` |
| Video Memory | `Profiler.GetAllocatedMemoryForGraphicsDriver()` |
| Battery Level | `SystemInfo.batteryLevel` |
| Battery Charging | `SystemInfo.batteryStatus` |

## Thread Safety

- Logging and event APIs (`OpButtonSDK.Log()`, `OpButtonSDK.Event()`, etc.) are thread-safe. Custom log-attribute providers run on their calling thread and must be thread-safe too.
- Action callbacks and session listener methods are dispatched on the **Unity main thread**.
- Initialize the SDK and build or modify panel widgets on the Unity main thread. The SDK captures panel and session-attribute providers there.
- Custom metric collectors run on a background thread; cache Unity API values on the main thread before exposing them to a collector.
- Panel and attribute snapshots are acknowledged only after successful delivery. Pending changes survive failures; metric sampling continues during session recovery.
- See [LiveKit setup](live-streaming.md#install-the-optional-adapter) for the optional media adapter.

## Shutdown

```csharp
void OnApplicationQuit()
{
    OpButtonSDK.Shutdown();
}
```

The SDK also hooks `Application.quitting` automatically via the internal `OpButtonRunner` MonoBehaviour.

---

**See also:** [Widgets](widgets.md) · [Logging & Events](logging.md) · [Metrics](metrics.md)
