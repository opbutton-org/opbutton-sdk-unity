# OpButton SDK for Unity

Remote debug panel, logging, and metrics for Unity applications.

## Requirements

- Unity 2020.3+
- .NET Standard 2.1 or .NET Framework 4.x

## Installation

### Unity Package Manager (Git URL)

Add the following to your `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.opbutton.sdk": "https://github.com/opbutton-org/opbutton-sdk-unity.git"
  }
}
```

Or in Unity Editor: **Window → Package Manager → + → Add package from git URL** and enter:
```
https://github.com/opbutton-org/opbutton-sdk-unity.git
```

## Quick Start

```csharp
using OpButton.SDK;
using UnityEngine;

public class OpButtonSetup : MonoBehaviour
{
    void Awake()
    {
        // 1. Initialize
        var config = new OpButtonConfig.Builder("https://opbutton.example.com", "opbutton_your_token")
            .SetDeviceId("my-device")
            .SetDeviceName("Unity Editor")
            .Build();
        OpButtonSDK.Initialize(config);

        // 2. Build UI
        OpButtonUI ui = OpButtonSDK.GetUI();
        OpButtonTab tab = ui.AddTab("Game Controls");
        OpButtonLayout root = tab.Vertical();

        root.AddButton("Restart")
            .Variant("contained").Color("error")
            .OnClick(action => {
                Debug.Log("Restart clicked!");
                OpButtonSDK.AcknowledgeAction(action.Id);
            });

        root.AddStat("FPS")
            .Unit("fps")
            .ValueProvider(() => (1f / Time.unscaledDeltaTime).ToString("F0"));

        root.AddSwitch("God Mode")
            .DefaultValue(false)
            .OnChange(action => {
                bool value = action.Payload?.GetBool("value") ?? false;
                Debug.Log("God mode: " + value);
            });

        // 3. Send logs
        OpButtonSDK.Log(OpButtonLogLevel.Info, "Game started");

        // 4. Connect (if autoConnect is false)
        // OpButtonSDK.Connect();
    }

    void OnDestroy()
    {
        OpButtonSDK.Shutdown();
    }
}
```

## Features

### Remote UI Panel

Build interactive panels with declarative widget builders:

```csharp
OpButtonUI ui = OpButtonSDK.GetUI();
OpButtonTab tab = ui.AddTab("Controls");
OpButtonLayout root = tab.Vertical();

// Buttons
root.AddButton("Action").OnClick(action => DoAction());

// Labels with dynamic providers
root.AddLabel("Status: Idle")
    .TextProvider(() => "Status: " + currentStatus);

// Stat displays
root.AddStat("Memory").Unit("MB")
    .ValueProvider(() => (GC.GetTotalMemory(false) / 1048576.0).ToString("F1"));

// Toggle switches
root.AddSwitch("Debug Mode").DefaultValue(false)
    .OnChange(action => debugMode = action.Payload?.GetBool("value") ?? false);

// Text inputs
root.AddInput("Command")
    .Placeholder("Type a command...")
    .OnSubmit(action => ExecuteCommand(action.Payload?.GetString("value")));

// Select dropdowns
root.AddSelect("Level")
    .Options(new[] {
        new[] { "1", "Level 1" },
        new[] { "2", "Level 2" },
        new[] { "3", "Level 3" }
    })
    .DefaultValue("1")
    .OnChange(action => LoadLevel(action.Payload?.GetString("value")));

// Nested layouts
OpButtonLayout row = root.AddHorizontal();
row.AddButton("A").OnClick(action => DoA());
row.AddPadding(1.0);
row.AddButton("B").OnClick(action => DoB());

// Collapsible sections
OpButtonLayout debug = root.AddCollapsible("Debug Options");
debug.AddButton("Clear Cache").OnClick(action => ClearCache());
```

### Logging

```csharp
OpButtonSDK.Log(OpButtonLogLevel.Info, "Player spawned");
OpButtonSDK.Log(OpButtonLogLevel.Warning, "Low FPS detected", "performance");
OpButtonSDK.Log(OpButtonLogLevel.Error, "Failed to load asset", new Dictionary<string, object> {
    { "asset", "player_model.fbx" },
    { "error_code", 404 }
});
```

### Structured Events

```csharp
OpButtonSDK.Event("level_complete", new Dictionary<string, object> {
    { "level", 5 },
    { "score", 12500 },
    { "time_seconds", 45.3 }
});

OpButtonSDK.ChangeScreen("gameplay", "Level 5");
```

### Metrics

Built-in metrics collected automatically:
- **FPS** and frame time
- **Memory** (total allocated)
- **Video memory** (GPU driver allocated)
- **Battery** level and charging status

Add custom metrics:

```csharp
OpButtonSDK.GetMetrics().Record(OpButtonMetricType.DrawCalls, Camera.main.GetDrawCallsCount());

// Custom metric type
var customMetric = OpButtonMetricType.Create("enemies_alive");
OpButtonSDK.GetMetrics().Record(customMetric, activeEnemies.Count);
```

Custom collectors:

```csharp
public class GameMetricCollector : IOpButtonMetricCollector
{
    public void Collect(List<OpButtonMetricEntry> output)
    {
        output.Add(new OpButtonMetricEntry(OpButtonMetricType.DrawCalls, GetDrawCalls()));
    }
}

// In config
var metricConfig = new OpButtonMetricConfigBuilder()
    .SetEnabled(true)
    .SetSampleIntervalMs(200)
    .AddCollector(new GameMetricCollector());

var config = new OpButtonConfig.Builder(url, token)
    .SetMetricConfig(metricConfig)
    .Build();
```

### In-App Purchases

```csharp
// Publish product catalog
OpButtonSDK.SetInAppProducts(new List<Dictionary<string, object>> {
    new Dictionary<string, object> {
        { "product_id", "coins_100" },
        { "price", "$0.99" },
        { "title", "100 Coins" }
    }
});

// Track purchases
OpButtonSDK.PurchaseInApp("txn_123", new List<string> { "coins_100" });
```

### Session Attributes

```csharp
var sessionAttrs = new OpButtonSessionAttributeBuilder()
    .Put("app_version", Application.version)
    .Put("platform", Application.platform.ToString())
    .PutProvider("scene", () => UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);

var config = new OpButtonConfig.Builder(url, token)
    .SetSessionAttributes(sessionAttrs)
    .Build();
```

### Session Lifecycle

```csharp
public class MySessionListener : IOpButtonSessionListener
{
    public void OnOpButtonSessionConnecting() => Debug.Log("Connecting...");
    public void OnOpButtonSessionWaitingApproval(string requestId) => Debug.Log("Waiting for approval");
    public void OnOpButtonSessionStarted(string sessionToken) => Debug.Log("Session active!");
    public void OnOpButtonSessionClosed() => Debug.Log("Session closed");
    public void OnOpButtonSessionRejected() => Debug.Log("Connection rejected");
    public void OnOpButtonSessionAuthFailed() => Debug.Log("Auth failed - check API token");
    public void OnOpButtonSessionError(OpButtonException e) => Debug.LogError("Error: " + e.Message);
}

var config = new OpButtonConfig.Builder(url, token)
    .SetSessionListener(new MySessionListener())
    .Build();
```

## Configuration Reference

| Parameter | Default | Description |
|-----------|---------|-------------|
| `baseUrl` | required | OpButton server URL |
| `apiToken` | required | Project API token (opbutton_...) |
| `deviceId` | auto | Device identifier (defaults to SystemInfo.deviceUniqueIdentifier) |
| `deviceName` | auto | Device display name (defaults to model + Unity version) |
| `pollIntervalMs` | 10000 | Approval polling interval (ms) |
| `actionPollIntervalMs` | 2000 | Action polling interval (ms) |
| `autoConnect` | true | Connect automatically on Initialize() |

## License

MIT
