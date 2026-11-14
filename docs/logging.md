# Logging & Events

OpButton streams structured logs from your app to the dashboard in real time. Logs are buffered locally and flushed in batches.

## Log configuration

```csharp
var logConfig = new OpButtonLogConfigBuilder()
    .SetEnabled(true)                    // default: true
    .SetLogLevel(OpButtonLogLevel.Info)     // minimum level to capture
    .SetBatchSize(100)                   // logs per flush (default: 100)
    .SetBufferSize(1000)                 // max buffered logs (default: 1000)
    .SetAttributes(logAttrs);            // attributes attached to every entry

var config = new OpButtonConfig.Builder(url, token)
    .SetLogConfig(logConfig)
    .Build();
```

## Log levels

`Debug` · `Info` · `Warning` · `Error` · `Critical` · `Exception`

## Basic logging

```csharp
OpButtonSDK.Log(OpButtonLogLevel.Info, "Player spawned");
OpButtonSDK.Log(OpButtonLogLevel.Error, "Failed to load asset");
```

With category and thread:

```csharp
OpButtonSDK.Log(OpButtonLogLevel.Warning, "Texture missing", "rendering", "GLThread");
```

With metadata (arbitrary key-value data per entry):

```csharp
var meta = new Dictionary<string, object> {
    { "asset", "player.png" },
    { "size", 1024 }
};
OpButtonSDK.Log(OpButtonLogLevel.Error, "Load failed", meta);
```

## Structured log entries

For full control, build a `OpButtonLogEntry`:

```csharp
OpButtonSDK.Log(OpButtonLogEntry.Error("Crash detected"));
OpButtonSDK.Log(OpButtonLogEntry.Info("Level loaded"));
```

Static factory methods: `Debug()`, `Info()`, `Warning()`, `Error()`, `Critical()`.

## Log attributes

Attributes are key-value pairs attached to **every** log entry:

```csharp
var logAttrs = new OpButtonLogAttributeBuilder()
    .Put("app_version", Application.version)          // static
    .PutProvider("scene", () => SceneManager.GetActiveScene().name)  // dynamic
    .PutProvider("fps", () => (1f / Time.unscaledDeltaTime).ToString("F0"));
```

## Events

Events are high-level structured messages:

```csharp
OpButtonSDK.Event("level_completed");
OpButtonSDK.Event("level_completed", new Dictionary<string, object> {
    { "level", 5 }, { "time", 42.3 }
});
OpButtonSDK.Event("level_completed", "gameplay", new Dictionary<string, object> {
    { "level", 5 }
});
```

## Revenue events

Track monetization:

```csharp
OpButtonSDK.Revenue("purchase_completed", new Dictionary<string, object> {
    { "amount", 4.99 }, { "currency", "USD" }
});
```

## Screen changes

Record scene/screen transitions:

```csharp
OpButtonSDK.ChangeScreen("gameplay", "Level 5");
OpButtonSDK.ChangeScreen("menu", "Main Menu");
```

## In-App Purchases

```csharp
// Publish product catalog
OpButtonSDK.SetInAppProducts(new List<Dictionary<string, object>> {
    new Dictionary<string, object> {
        { "product_id", "gems_100" }, { "price", "$0.99" }
    }
});

// Set owned products
OpButtonSDK.SetOwnedInAppProducts(new List<string> { "remove_ads" });

// Record purchase
OpButtonSDK.PurchaseInApp("txn_abc", new List<string> { "gems_100" });
```

---

**See also:** [Unity Integration](unity-integration.md) · [Widgets](widgets.md) · [Metrics](metrics.md)
