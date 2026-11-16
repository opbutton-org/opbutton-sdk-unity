# Widgets

OpButton UI is built with tabs, layouts, and widgets. Each service in your app can add its own tab on the dashboard.

## Tabs

```csharp
OpButtonUI ui = OpButtonSDK.GetUI();

OpButtonTab tab = ui.AddTab("Game Controls");

// Retrieve or remove tabs later
OpButtonTab existing = ui.GetTab("tab-1");
ui.RemoveTab("tab-1");
```

## Layouts

Every tab starts with a layout direction:

```csharp
OpButtonLayout root = tab.Vertical();   // top-to-bottom
OpButtonLayout root = tab.Horizontal(); // left-to-right
```

Nest layouts for complex arrangements:

```csharp
OpButtonLayout row = root.AddHorizontal();
row.AddButton("Left");
row.AddPadding(1.0);                 // flexible spacer
row.AddButton("Right");
```

### Collapsible sections

```csharp
OpButtonLayout section = root.AddCollapsible("Advanced");
section.AddSwitch("Verbose logging");
```

## Widget types

### Button

```csharp
root.AddButton("Restart")
    .Variant("contained")   // "contained" | "outlined" | "text"
    .Color("error")          // MUI color: "primary" | "error" | "warning" | "info" | "success"
    .Disabled(false)
    .OnClick(() => RestartGame());
```

### Label

Static or dynamic text:

```csharp
root.AddLabel("Idle")
    .Color("info")
    .TextProvider(() => game.GetStatus());  // auto-updated
```

### Stat

Numeric value with unit:

```csharp
root.AddStat("FPS")
    .Unit("fps")
    .Value("60")                            // static
    .ValueProvider(() => game.GetFps());    // or dynamic
```

### Switch

Boolean toggle:

```csharp
root.AddSwitch("God mode")
    .DefaultValue(false)
    .OnChange(action => {
        game.SetGodMode(action.Value);
    });
```

### Input

Single-line text input:

```csharp
root.AddInput("Command")
    .InputType("text")       // "text" | "number" | "password"
    .Placeholder("type a command…")
    .DefaultValue("")
    .OnSubmit(action => {
        ExecuteCommand(action.Value);
    });
```

### Select

Dropdown:

```csharp
root.AddSelect("Level")
    .Options(new[] {
        new[] { "1", "Level 1" },
        new[] { "2", "Level 2" },
        new[] { "3", "Level 3" }
    })
    .DefaultValue("1")
    .OnChange(action => {
        LoadLevel(action.Value);
    });
```

### Textarea

Multi-line text input:

```csharp
root.AddTextarea("Notes")
    .Rows(4)
    .DefaultValue("")
    .OnSubmit(action => {
        SaveNotes(action.Value);
    });
```

### Table

Data table:

```csharp
root.AddTable("Inventory")
    .Columns(new[] {
        new[] { "item", "Item" },
        new[] { "count", "Count" }
    })
    .Rows(new List<Dictionary<string, object>> {
        new Dictionary<string, object> { { "item", "Sword" }, { "count", 1 } },
        new Dictionary<string, object> { { "item", "Potion" }, { "count", 5 } }
    });
```

### Logs

Log output display:

```csharp
root.AddLogs("App Logs")
    .MaxLines(200);
```

## Dynamic values

Use value providers for automatic updates without manual polling:

```csharp
root.AddLabel("Status")
    .TextProvider(() => gameManager.Status);

root.AddStat("Players")
    .ValueProvider(() => connectedPlayers.Count.ToString());
```

Providers are evaluated on each SDK poll cycle. The SDK only sends updates to the server when values actually change.

---

**See also:** [Unity Integration](unity-integration.md) · [Logging & Events](logging.md) · [Metrics](metrics.md)

## Retained layout options

Elements receive generated IDs automatically. Layout edits preserve IDs; removing
or replacing a tree detaches its callbacks and providers. Serialize tree edits on
the host UI thread. The SDK ID counter itself is thread-safe.

Rows and columns support gap, padding, align, justify, wrap and inherited density.
Use a grid for equal-width commands: columns is the maximum column count, and
minCellWidth allows automatic reduction on narrow panels. Placement supports
content width, numeric width, fill with a weight, minimum/maximum width and
alignSelf. Dimensions are dashboard CSS pixels. Radio options stay in one group;
slider changes are sent on release/keyboard commit, with a numeric value.

Install the updated backend and dashboard before using these extensions. Protocol
version 2 remains compatible with existing panels. Optional public ID aliases
retain their existing meaning and are unnecessary for creating elements.

```csharp
var root = ui.AddTab("Game").Vertical().Gap(12).Padding(8);
var commands = root.AddGrid().Columns(3).MinCellWidth(140).Gap(8);
commands.AddButton("Restart");
commands.AddButton("Clear logs");
var row = root.AddHorizontal().Gap(8).Align("end");
row.AddSelect("Language").Options(new[] { new[] { "en", "English" }, new[] { "ru", "Русский" } })
    .DefaultValue("en").Fill().MinWidth(160);
row.AddButton("Apply").Width(120);
root.AddRadioGroup("Difficulty").Options(new[] { new[] { "easy", "Easy" }, new[] { "hard", "Hard" } })
    .DefaultValue("easy").OptionDirection("horizontal");
root.AddSlider("Speed").Range(0.5, 3, 0.1).DefaultValue(1).Unit("x").Fill()
    .OnChange((OpButtonSliderAction action) => SetSpeed(action.Value));
```
