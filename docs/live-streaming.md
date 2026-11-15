# Live Streaming

OpButton supports real-time screen streaming from your Unity app to the dashboard via [LiveKit](https://livekit.io/).

## Requirements

- Unity 2022.3+ for the optional LiveKit adapter
- LiveKit Unity SDK (`io.livekit.livekit-sdk`) 2.1.1, installed explicitly as described below
- Unity Audio and Screen Capture modules enabled (`com.unity.modules.audio`, `com.unity.modules.screencapture`); these are dependencies of LiveKit's capture code
- LiveKit server configured on the backend (`livekit_url`, `livekit_api_key`, `livekit_api_secret`)

## Install the optional adapter

The base OpButton package includes UI, logs, metrics and sessions without a LiveKit dependency.
It declares its uGUI dependency automatically. Add LiveKit to the **host project's** `Packages/manifest.json` when screen streaming is needed:

```json
{
  "dependencies": {
    "com.opbutton.sdk": "https://github.com/opbutton-org/opbutton-sdk-unity.git",
    "io.livekit.livekit-sdk": "https://github.com/livekit/client-sdk-unity.git#v2.1.1",
    "com.unity.modules.audio": "1.0.0",
    "com.unity.modules.screencapture": "1.0.0"
  }
}
```

Git dependencies belong in the host manifest; Unity does not resolve Git URLs in a package's dependency list.
`OpButton.LiveKit` is a separate assembly enabled when LiveKit is installed. It registers an `IOpButtonLivePublisher` adapter before scene initialization. Without it, streaming commands return an actionable error while other SDK features remain available.

## How it works

1. A dashboard operator clicks **Start Live** on a session
2. The backend creates a `live_start` action and generates a LiveKit room + publisher token
3. The Unity SDK receives the action, fetches publisher credentials from `GET /api/client/session/live/publisher`
4. `OpButtonLiveKitPublisher` connects to the LiveKit room and publishes a screen capture track using `ScreenVideoSource`
5. The dashboard connects to the same room as a viewer (subscribe-only) and displays the video
6. The operator can send remote tap and long-press actions, which the SDK dispatches via Unity's `EventSystem`

## Architecture

```
Dashboard (viewer)  ←→  LiveKit Server  ←→  Unity App (publisher)
                              ↑
                       Backend (tokens)
```

### Key classes

| Class | Role |
|-------|------|
| `OpButtonLiveKitPublisher` | Manages LiveKit Room connection, screen capture, and track publishing |
| `OpButtonLiveManager` | Orchestrates live lifecycle: handles start/stop/update/tap/longpress actions |
| `OpButtonLiveOverlayView` | Renders visual touch feedback (white outer circle + orange inner circle) |

## Action types

| Action | Direction | Description |
|--------|-----------|-------------|
| `live_start` | Dashboard → SDK | Start live streaming with quality preset |
| `live_update` | Dashboard → SDK | Change quality preset while streaming |
| `live_stop` | Dashboard → SDK | Stop live streaming |
| `live_tap` | Dashboard → SDK | Simulate a tap at normalized (x, y) coordinates |
| `live_long_press` | Dashboard → SDK | Simulate a long press with duration |

## Quality presets

| Preset | Max Dimension | FPS | Max Bitrate | Action Poll Interval |
|--------|--------------|-----|-------------|---------------------|
| `low` | 540px | 2 | 300 Kbps | 500ms |
| `balanced` | 720px | 3 | 600 Kbps | 400ms |
| `high` | 1080px | 4 | 1.2 Mbps | 300ms |

Quality can be adjusted dynamically without restarting the stream.

## Screen capture

The adapter uses LiveKit's GPU-based `ScreenVideoSource` to capture the Unity render output in players.
In the Editor it uses the dedicated camera source. It does not capture the operating system desktop or request Android MediaProjection/iOS ReplayKit permission.

## Remote touch

When live streaming is active, the dashboard can send normalized coordinates (0.0–1.0) for tap and long-press:

```json
{
  "action_type": "live_tap",
  "payload": {
    "normalized_x": 0.5,
    "normalized_y": 0.3
  }
}
```

The SDK:
1. Converts normalized coordinates to pixel coordinates based on `Screen.width` / `Screen.height`
2. Uses `EventSystem.RaycastAll()` to find the UI element at that position
3. Dispatches pointer down/up/click events via `ExecuteEvents`
4. Shows a visual indicator via `OpButtonLiveOverlayView`

## Events emitted

The SDK emits the following structured events during live sessions:

| Event | Category | Description |
|-------|----------|-------------|
| `live_started` | `live` | Stream started successfully |
| `live_start_failed` | `live` | Failed to start stream |
| `live_updated` | `live` | Quality changed successfully |
| `live_update_failed` | `live` | Failed to change quality |
| `live_stopped` | `live` | Stream stopped |
| `live_tap` | `live_input` | Remote tap dispatched |
| `live_long_press` | `live_input` | Remote long press dispatched |

---

**See also:** [Unity Integration](unity-integration.md) · [Widgets](widgets.md) · [Logging & Events](logging.md) · [Metrics](metrics.md)
