using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace OpButton.SDK
{
    /// <summary>
    /// OpButton SDK — remote debug panel, logging, and metrics for Unity applications.
    ///
    /// <code>
    /// // 1. Initialize
    /// var config = new OpButtonConfig.Builder("https://opbutton.example.com", "opbutton_your_token")
    ///     .SetDeviceId("my-device")
    ///     .SetDeviceName("Unity Editor")
    ///     .Build();
    /// OpButton.Initialize(config);
    ///
    /// // 2. Listen for actions
    /// OpButton.AddActionListener(new MyActionListener());
    ///
    /// // 3. Build UI
    /// OpButtonUI ui = OpButton.GetUI();
    /// OpButtonTab tab = ui.AddTab("Controls");
    /// OpButtonLayout root = tab.Vertical();
    /// root.AddButton("Restart")
    ///     .Variant("contained").Color("error")
    ///     .OnClick(action => RestartGame());
    ///
    /// // 4. Connect
    /// OpButton.Connect();
    ///
    /// // 5. Send logs anytime
    /// OpButton.Log(OpButtonLogLevel.Info, "Game started");
    ///
    /// // 6. Shutdown on exit
    /// OpButton.Shutdown();
    /// </code>
    /// </summary>
    public sealed class OpButtonSDK
    {
        public const string VERSION = "1.0.0";

        private static volatile OpButtonSDK s_instance;
        private static readonly object s_lock = new object();

        private readonly OpButtonConfig m_config;
        private readonly OpButtonHttpClient m_httpClient;

        private volatile string m_sessionToken;
        private volatile string m_requestId;
        private volatile OpButtonSessionStatus m_status = OpButtonSessionStatus.Disconnected;
        private volatile bool m_running;
        private volatile bool m_actionPollInFlight;

        private readonly List<OpButtonLogEntry> m_logBuffer = new List<OpButtonLogEntry>();
        private readonly object m_logLock = new object();
        private bool m_logOverflowWarned;
        private readonly Dictionary<string, object> m_sessionAttributeCache = new Dictionary<string, object>();

        private readonly List<IOpButtonActionListener> m_actionListeners = new List<IOpButtonActionListener>();
        private readonly List<IOpButtonSessionListener> m_sessionListeners = new List<IOpButtonSessionListener>();
        private readonly List<OpButtonWidgetCallback> m_actionCallbacks = new List<OpButtonWidgetCallback>();

        private Thread m_connectionThread;
        private Timer m_actionPollTimer;
        private Timer m_metricSampleTimer;
        private long m_currentActionPollIntervalMs;

        private readonly OpButtonUI m_ui = new OpButtonUI();
        private readonly OpButtonMetrics m_metrics = new OpButtonMetrics();
        private readonly OpButtonDefaultMetricCollector m_defaultCollector = new OpButtonDefaultMetricCollector();
        private readonly OpButtonLiveManager m_liveManager;

        // Main-thread dispatch queue
        private readonly List<Action> m_mainThreadQueue = new List<Action>();
        private readonly object m_mainThreadLock = new object();

        private OpButtonSDK(OpButtonConfig config)
        {
            m_config = config;
            m_httpClient = new OpButtonHttpClient(config.BaseUrl, config.ApiToken);
            m_currentActionPollIntervalMs = config.ActionPollIntervalMs;
            OpButtonLog.SetLevel(config.LogConfig.GetLogLevel());
            OpButtonLog.SetLoggerListener(config.LoggerListener);

            m_liveManager = new OpButtonLiveManager(m_httpClient);
            m_liveManager.OnLiveModeChanged = (enabled, pollMs) =>
            {
                if (enabled && pollMs > 0)
                {
                    var token = m_sessionToken;
                    if (token != null)
                        ScheduleActionPolling(token, pollMs);
                }
                else if (!enabled)
                {
                    var token = m_sessionToken;
                    if (token != null)
                        ScheduleActionPolling(token, m_config.ActionPollIntervalMs);
                }
            };

            var mc = config.MetricConfig;
            if (mc.IsEnabled())
            {
                m_metrics.SetSampleIntervalMs(mc.GetSampleIntervalMs());
                m_metrics.SetBufferSize(mc.GetBufferSize());
                m_metrics.SetBatchSize(mc.GetBatchSize());
            }
        }

        // ══════════════════════════════════════════════════════
        //  Static API
        // ══════════════════════════════════════════════════════

        public static void Initialize(OpButtonConfig config)
        {
            if (s_instance != null)
            {
                OpButtonLog.Warn("OpButton.Initialize() called more than once, ignoring");
                return;
            }

            lock (s_lock)
            {
                if (s_instance == null)
                {
                    OpButtonLog.SetMainThreadId(System.Threading.Thread.CurrentThread.ManagedThreadId);

                    var p = new OpButtonSDK(config);
                    s_instance = p;

                    // Ensure MonoBehaviour runner exists
                    OpButtonRunner.EnsureExists();

                    OpButtonLog.Info("OpButton SDK initialized (server: {0})", config.BaseUrl);

                    if (config.SessionListener != null)
                        p.m_sessionListeners.Add(config.SessionListener);
                    if (config.ActionListener != null)
                        p.m_actionListeners.Add(config.ActionListener);

                    var mc = config.MetricConfig;
                    if (mc.IsEnabled())
                    {
                        p.m_metrics.AddCollector(p.m_defaultCollector);
                        foreach (var collector in mc.GetCollectors())
                            p.m_metrics.AddCollector(collector);
                        OpButtonLog.Info("Built-in metrics enabled (sample interval: {0}ms)", mc.GetSampleIntervalMs());
                    }

                    if (config.AutoConnect)
                        p.StartConnection();
                }
            }
        }

        public static bool IsInitialized => s_instance != null;

        public static OpButtonSessionStatus GetStatus()
        {
            var p = s_instance;
            return p != null ? p.m_status : OpButtonSessionStatus.Disconnected;
        }

        public static void AddActionListener(IOpButtonActionListener listener)
        {
            var p = RequireInstance();
            lock (s_lock) { p.m_actionListeners.Add(listener); }
        }

        public static void RemoveActionListener(IOpButtonActionListener listener)
        {
            var p = s_instance;
            if (p != null)
                lock (s_lock) { p.m_actionListeners.Remove(listener); }
        }

        public static void AddSessionListener(IOpButtonSessionListener listener)
        {
            var p = RequireInstance();
            lock (s_lock) { p.m_sessionListeners.Add(listener); }
        }

        public static void RemoveSessionListener(IOpButtonSessionListener listener)
        {
            var p = s_instance;
            if (p != null)
                lock (s_lock) { p.m_sessionListeners.Remove(listener); }
        }

        public static OpButtonUI GetUI()
        {
            return RequireInstance().m_ui;
        }

        public static OpButtonMetrics GetMetrics()
        {
            return RequireInstance().m_metrics;
        }

        public static void Connect()
        {
            RequireInstance().StartConnection();
        }

        public static void Disconnect()
        {
            var p = s_instance;
            if (p != null) p.StopConnection();
        }

        // ── Logging API ──

        public static void Log(OpButtonLogLevel level, string message)
        {
            var p = s_instance;
            if (p != null && p.m_config.LogConfig.IsEnabled())
                p.BufferLog(new OpButtonLogEntry(level, message, null, null, null, p.ResolveLogAttributes()));
        }

        public static void Log(OpButtonLogLevel level, string message, string category, string thread = null)
        {
            var p = s_instance;
            if (p != null && p.m_config.LogConfig.IsEnabled())
                p.BufferLog(new OpButtonLogEntry(level, message, category, thread, null, p.ResolveLogAttributes()));
        }

        public static void Log(OpButtonLogLevel level, string message, Dictionary<string, object> metadata)
        {
            var p = s_instance;
            if (p != null && p.m_config.LogConfig.IsEnabled())
                p.BufferLog(new OpButtonLogEntry(level, message, null, null, metadata, p.ResolveLogAttributes()));
        }

        public static void Log(OpButtonLogLevel level, string message, string category, string thread,
            Dictionary<string, object> metadata)
        {
            var p = s_instance;
            if (p != null && p.m_config.LogConfig.IsEnabled())
                p.BufferLog(new OpButtonLogEntry(level, message, category, thread, metadata, p.ResolveLogAttributes()));
        }

        public static void Log(OpButtonLogEntry entry)
        {
            var p = s_instance;
            if (p != null && p.m_config.LogConfig.IsEnabled())
                p.BufferLog(entry);
        }

        // ── Structured event/revenue API ──

        public static void Event(string message, Dictionary<string, object> metadata = null)
        {
            Event(message, null, metadata, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        }

        public static void Event(string message, string category,
            Dictionary<string, object> metadata = null)
        {
            Event(message, category, metadata, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        }

        public static void Event(string message, string category,
            Dictionary<string, object> metadata, long clientTimestampMs)
        {
            BufferStructuredLog("event", message, category, metadata, clientTimestampMs);
        }

        public static void Revenue(string message, Dictionary<string, object> metadata = null)
        {
            Revenue(message, null, metadata, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        }

        public static void Revenue(string message, string category,
            Dictionary<string, object> metadata = null)
        {
            Revenue(message, category, metadata, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        }

        public static void Revenue(string message, string category,
            Dictionary<string, object> metadata, long clientTimestampMs)
        {
            BufferStructuredLog("revenue", message, category, metadata, clientTimestampMs);
        }

        public static void ChangeScreen(string screenType, string screenName)
        {
            var metadata = new Dictionary<string, object>
            {
                ["opbutton_command"] = "change_screen",
                ["opbutton_slice_type"] = "screen",
                ["opbutton_slice_name"] = screenName,
                ["screen_type"] = screenType,
                ["screen_name"] = screenName
            };
            Event("change_screen", "change_screen", metadata);
        }

        public static void SetInAppProducts(List<Dictionary<string, object>> products)
        {
            var normalized = new List<object>();
            foreach (var product in products)
                normalized.Add(product != null ? new Dictionary<string, object>(product) : new Dictionary<string, object>());

            var metadata = new Dictionary<string, object>
            {
                ["opbutton_command"] = "set_in_app_products",
                ["opbutton_purchase_entry_type"] = "catalog",
                ["in_app_products"] = normalized,
                ["in_app_product_count"] = normalized.Count
            };
            BufferStructuredLog("purchase", "set_in_app_products", "catalog", metadata,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        }

        public static void SetOwnedInAppProducts(List<string> productIds)
        {
            var normalized = new List<object>(productIds);
            var metadata = new Dictionary<string, object>
            {
                ["opbutton_command"] = "set_owned_in_app_products",
                ["opbutton_purchase_entry_type"] = "owned",
                ["owned_in_app_products"] = normalized,
                ["owned_in_app_product_count"] = normalized.Count
            };
            BufferStructuredLog("purchase", "set_owned_in_app_products", "owned", metadata,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        }

        public static void PurchaseInApp(string transactionId, List<string> productIds,
            Dictionary<string, object> metadata = null)
        {
            var normalized = new List<object>(productIds);
            var purchaseMetadata = new Dictionary<string, object>();
            if (metadata != null)
                foreach (var kvp in metadata)
                    purchaseMetadata[kvp.Key] = kvp.Value;

            purchaseMetadata["opbutton_command"] = "purchase_in_app";
            purchaseMetadata["opbutton_purchase_entry_type"] = "purchase";
            purchaseMetadata["in_app_products"] = normalized;
            purchaseMetadata["in_app_product_count"] = normalized.Count;

            if (!string.IsNullOrEmpty(transactionId))
                purchaseMetadata["transaction_id"] = transactionId;

            string message = normalized.Count == 0 ? "purchase_in_app" : productIds[0];
            BufferStructuredLog("purchase", message, "purchase", purchaseMetadata,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        }

        public static void AcknowledgeAction(string actionId, Dictionary<string, object> ackPayload = null)
        {
            var p = s_instance;
            if (p == null) return;
            string token = p.m_sessionToken;
            if (token == null) return;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try { p.m_httpClient.AcknowledgeAction(token, actionId, ackPayload); }
                catch (OpButtonException e) { OpButtonLog.Error("Failed to acknowledge action", e); }
            });
        }

        public static void Shutdown()
        {
            OpButtonSDK p;
            lock (s_lock)
            {
                p = s_instance;
                s_instance = null;
            }
            if (p != null) p.DoShutdown();
        }

        // ══════════════════════════════════════════════════════
        //  Internal (called by OpButtonRunner MonoBehaviour)
        // ══════════════════════════════════════════════════════

        internal static void OnUpdate()
        {
            var p = s_instance;
            if (p == null) return;

            // Flush deferred log messages from background threads
            OpButtonLog.FlushDeferred();

            // Update delta time for FPS tracking
            p.m_defaultCollector.UpdateDeltaTime(Time.unscaledDeltaTime);

            // Cache Unity API values that can only be read from main thread
            p.m_defaultCollector.UpdateFromMainThread();

            // Poll widget value providers on the main thread (providers may access Unity APIs)
            p.m_ui.PollValues();

            // Process main-thread dispatch queue
            p.ProcessMainThreadQueue();
        }

        internal static void OnApplicationQuit()
        {
            Shutdown();
        }

        // ══════════════════════════════════════════════════════
        //  Internal implementation
        // ══════════════════════════════════════════════════════

        private static OpButtonSDK RequireInstance()
        {
            var p = s_instance;
            if (p == null)
                throw new InvalidOperationException("OpButton.Initialize() must be called first");
            return p;
        }

        private void StartConnection()
        {
            if (m_running) { OpButtonLog.Warn("Already connecting/connected"); return; }
            m_running = true;

            m_connectionThread = new Thread(ConnectAndWaitApproval) { IsBackground = true, Name = "OpButtonSDK-Connect" };
            m_connectionThread.Start();
        }

        private void StopConnection()
        {
            if (!m_running) return;
            m_running = false;

            CancelScheduledTasks();

            m_liveManager.OnSessionClosed();

            string token = m_sessionToken;
            m_sessionToken = null;

            if (token != null)
            {
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        FlushLogs(token);
                        FlushMetrics(token);
                        m_httpClient.CloseSession(token);
                    }
                    catch (OpButtonException) { }

                    SetStatus(OpButtonSessionStatus.Closed);
                    RunOnMainThread(() => NotifySessionClosed());
                });
            }
            else
            {
                SetStatus(OpButtonSessionStatus.Disconnected);
            }
        }

        private void DoShutdown()
        {
            OpButtonLog.Info("Shutting down OpButton SDK");
            m_liveManager.Shutdown();
            StopConnection();
            m_httpClient.Shutdown();
            lock (s_lock)
            {
                m_actionListeners.Clear();
                m_sessionListeners.Clear();
            }
            lock (m_logLock) { m_logBuffer.Clear(); }
            m_metrics.Clear();
        }

        private void ConnectAndWaitApproval()
        {
            int retryCount = 0;
            long retryDelayMs = 2000;
            const long maxRetryDelayMs = 30000;

            while (m_running)
            {
                try
                {
                    DoConnectAndWaitApproval();
                    return;
                }
                catch (OpButtonException e)
                {
                    if (e.IsUnauthorized)
                    {
                        OpButtonLog.Error("Authentication failed", e);
                        SetStatus(OpButtonSessionStatus.AuthFailed);
                        m_running = false;
                        RunOnMainThread(() => NotifyAuthFailed());
                        return;
                    }

                    if (!e.IsNetworkError)
                    {
                        OpButtonLog.Error("Server error, stopping connection: {0}", e.Message);
                        SetStatus(OpButtonSessionStatus.Error);
                        m_running = false;
                        RunOnMainThread(() => NotifyError(e));
                        return;
                    }

                    retryCount++;
                    OpButtonLog.Warn("Connection attempt {0} failed (network): {1}, retrying in {2}ms",
                        retryCount, e.Message, retryDelayMs);
                    SetStatus(OpButtonSessionStatus.Connecting);

                    Thread.Sleep((int)retryDelayMs);
                    retryDelayMs = Math.Min(retryDelayMs * 2, maxRetryDelayMs);
                }
            }
        }

        private void DoConnectAndWaitApproval()
        {
            SetStatus(OpButtonSessionStatus.Connecting);
            RunOnMainThread(() => NotifyConnecting());

            string deviceId = m_config.DeviceId;
            string deviceName = m_config.DeviceName;

            if (string.IsNullOrEmpty(deviceId))
                deviceId = SystemInfo.deviceUniqueIdentifier;
            if (string.IsNullOrEmpty(deviceName))
                deviceName = SystemInfo.deviceModel + " (Unity " + Application.unityVersion + ")";

            var resp = m_httpClient.Connect(deviceId, deviceName, ResolveAllSessionAttributes());
            m_requestId = resp.RequestId;

            OpButtonLog.Info("Connect request sent, request_id={0}, status={1}", resp.RequestId, resp.Status);

            if (resp.IsApproved && resp.SessionToken != null)
            {
                OnApproved(resp.SessionToken);
                return;
            }

            if (resp.IsRejected)
            {
                OnRejected();
                return;
            }

            SetStatus(OpButtonSessionStatus.WaitingApproval);
            RunOnMainThread(() => NotifyWaitingApproval(resp.RequestId));

            while (m_running)
            {
                Thread.Sleep((int)m_config.PollIntervalMs);

                var pollResp = m_httpClient.PollStatus(resp.RequestId);

                if (pollResp.IsApproved && pollResp.SessionToken != null)
                {
                    OnApproved(pollResp.SessionToken);
                    return;
                }

                if (pollResp.IsRejected)
                {
                    OnRejected();
                    return;
                }
            }
        }

        private void OnApproved(string sessionToken)
        {
            m_sessionToken = sessionToken;
            SetStatus(OpButtonSessionStatus.Active);

            OpButtonLog.Info("Session approved and active");

            RunOnMainThread(() => NotifySessionStarted(sessionToken));

            // Send initial UI
            if (m_ui.HasTabs())
            {
                var snapshot = m_ui.ToDict();
                try
                {
                    m_httpClient.SubmitPanel(sessionToken, snapshot);
                    m_ui.MarkSent();
                    OpButtonLog.Debug("Initial UI submitted (revision={0})", m_ui.Revision);
                }
                catch (OpButtonException e)
                {
                    OpButtonLog.Error("Failed to submit initial UI", e);
                    RunOnMainThread(() => NotifyError(e));
                }
            }

            // Start action polling
            ScheduleActionPolling(sessionToken, m_config.ActionPollIntervalMs);

            // Start metric sampling
            if (m_config.MetricConfig.IsEnabled())
            {
                long sampleMs = m_metrics.GetSampleIntervalMs();
                m_metricSampleTimer = new Timer(_ => m_metrics.Sample(), null, sampleMs, sampleMs);
            }
        }

        private void OnRejected()
        {
            OpButtonLog.Warn("Connection request rejected");
            SetStatus(OpButtonSessionStatus.Rejected);
            m_running = false;
            RunOnMainThread(() => NotifyRejected());
        }

        private void DoPollActions(string sessionToken)
        {
            if (!m_running || m_actionPollInFlight) return;
            m_actionPollInFlight = true;

            try
            {
                var changedAttrs = ResolveChangedSessionAttributes();
                var logChunk = DrainLogChunk();
                var metricChunk = m_metrics.Drain();

                // Poll value providers and snapshot UI
                // NOTE: PollValues() is called from OnUpdate() on the main thread
                Dictionary<string, object> uiSnapshot = m_ui.HasUnsent() ? m_ui.ToDict() : null;

                if (uiSnapshot != null)
                {
                    try
                    {
                        m_httpClient.SubmitPanel(sessionToken, uiSnapshot);
                        m_ui.MarkSent();
                        OpButtonLog.Debug("UI submitted (revision={0})", m_ui.Revision);
                    }
                    catch (OpButtonException e)
                    {
                        OpButtonLog.Error("Failed to submit UI", e);
                        RunOnMainThread(() => NotifyError(e));
                    }
                }

                try
                {
                    var json = m_httpClient.PollActions(sessionToken, changedAttrs, logChunk, metricChunk);
                    var actionsArr = json.GetArray("actions");
                    if (actionsArr != null && actionsArr.Count > 0)
                    {
                        foreach (var actionObj in actionsArr)
                        {
                            if (actionObj is Dictionary<string, object> actionDict)
                            {
                                var action = OpButtonAction.FromDict(new SimpleJson(actionDict));
                                RunOnMainThread(() => DispatchAction(action));
                            }
                        }
                    }

                    if (logChunk.Count > 0)
                        m_logOverflowWarned = false;
                }
                catch (OpButtonException e)
                {
                    RequeueLogs(logChunk);
                    m_metrics.Requeue(metricChunk);

                    if (e.IsNetworkError)
                    {
                        OpButtonLog.Warn("Network error during action poll, will retry");
                    }
                    else
                    {
                        OpButtonLog.Warn("Action poll failed with server error, attempting reconnect");
                        AttemptReconnect(sessionToken);
                    }
                }
            }
            finally
            {
                m_actionPollInFlight = false;
            }
        }

        private void DispatchAction(OpButtonAction action)
        {
            bool handled = HandleInternalAction(action);

            if (!handled)
                m_ui.DispatchAction(action);

            if (!handled)
            {
                List<IOpButtonActionListener> listeners;
                lock (s_lock) { listeners = new List<IOpButtonActionListener>(m_actionListeners); }
                foreach (var listener in listeners)
                {
                    try { listener.OnOpButtonActionReceived(action); }
                    catch (Exception ex) { OpButtonLog.Error("Action listener threw exception", ex); }
                }
            }
        }

        private bool HandleInternalAction(OpButtonAction action)
        {
            switch (action.ActionType)
            {
                case OpButtonActionType.LiveStart:
                {
                    string token = m_sessionToken;
                    if (token == null) return false;
                    ThreadPool.QueueUserWorkItem(_ =>
                    {
                        var ack = m_liveManager.Start(token, action.Payload?.ToDictionary());
                        AcknowledgeAction(action.Id, ack);
                    });
                    return true;
                }
                case OpButtonActionType.LiveUpdate:
                {
                    string token = m_sessionToken;
                    if (token == null) return false;
                    ThreadPool.QueueUserWorkItem(_ =>
                    {
                        var ack = m_liveManager.Update(token, action.Payload?.ToDictionary());
                        AcknowledgeAction(action.Id, ack);
                    });
                    return true;
                }
                case OpButtonActionType.LiveStop:
                {
                    var ack = m_liveManager.Stop();
                    AcknowledgeAction(action.Id, ack);
                    return true;
                }
                case OpButtonActionType.LiveTap:
                {
                    var ack = m_liveManager.Tap(action.Payload?.ToDictionary());
                    AcknowledgeAction(action.Id, ack);
                    return true;
                }
                case OpButtonActionType.LiveLongPress:
                {
                    var ack = m_liveManager.LongPress(action.Payload?.ToDictionary());
                    AcknowledgeAction(action.Id, ack);
                    return true;
                }
                default:
                    return false;
            }
        }

        private void ScheduleActionPolling(string sessionToken, long intervalMs)
        {
            if (!m_running) return;

            m_actionPollTimer?.Dispose();
            m_currentActionPollIntervalMs = intervalMs;
            m_actionPollTimer = new Timer(_ => DoPollActions(sessionToken), null, 0, intervalMs);
        }

        private void CancelScheduledTasks()
        {
            m_actionPollTimer?.Dispose();
            m_actionPollTimer = null;
            m_metricSampleTimer?.Dispose();
            m_metricSampleTimer = null;
        }

        // ── Log buffering ──

        private void BufferLog(OpButtonLogEntry entry)
        {
            lock (m_logLock)
            {
                if (m_logBuffer.Count >= m_config.LogConfig.GetBufferSize())
                {
                    m_logBuffer.RemoveAt(0);
                    if (!m_logOverflowWarned)
                    {
                        m_logOverflowWarned = true;
                        OpButtonLog.Warn("Log buffer overflow ({0}), dropping oldest entries",
                            m_config.LogConfig.GetBufferSize());
                    }
                }
                m_logBuffer.Add(entry);
            }
        }

        private List<OpButtonLogEntry> DrainLogChunk()
        {
            lock (m_logLock)
            {
                if (m_logBuffer.Count == 0)
                    return new List<OpButtonLogEntry>();

                int count = Math.Min(m_logBuffer.Count, m_config.LogConfig.GetBatchSize());
                var chunk = new List<OpButtonLogEntry>(m_logBuffer.GetRange(0, count));
                m_logBuffer.RemoveRange(0, count);
                return chunk;
            }
        }

        private void RequeueLogs(List<OpButtonLogEntry> chunk)
        {
            if (chunk.Count == 0) return;
            lock (m_logLock)
            {
                m_logBuffer.InsertRange(0, chunk);
                while (m_logBuffer.Count > m_config.LogConfig.GetBufferSize())
                    m_logBuffer.RemoveAt(m_logBuffer.Count - 1);
            }
        }

        private void FlushLogs(string sessionToken)
        {
            var chunk = DrainLogChunk();
            if (chunk.Count == 0) return;
            try { m_httpClient.SendLogs(sessionToken, chunk); }
            catch (OpButtonException) { RequeueLogs(chunk); }
        }

        private void FlushMetrics(string sessionToken)
        {
            var chunk = m_metrics.Drain();
            if (chunk.Count == 0) return;
            try { m_httpClient.SendMetrics(sessionToken, chunk); }
            catch (OpButtonException) { m_metrics.Requeue(chunk); }
        }

        // ── Attribute resolution ──

        private Dictionary<string, object> ResolveLogAttributes()
        {
            var builder = m_config.LogConfig.GetAttributes();
            var staticAttrs = builder.GetStaticAttributes();
            var dynamicAttrs = builder.GetDynamicAttributes();

            if (staticAttrs.Count == 0 && dynamicAttrs.Count == 0)
                return null;

            var attributes = new Dictionary<string, object>();
            foreach (var kvp in staticAttrs)
                attributes[kvp.Key] = kvp.Value;
            foreach (var kvp in dynamicAttrs)
            {
                try { attributes[kvp.Key] = kvp.Value(); }
                catch { }
            }

            return attributes.Count == 0 ? null : attributes;
        }

        private Dictionary<string, object> ResolveAllSessionAttributes()
        {
            var builder = m_config.SessionAttributes;
            var merged = new Dictionary<string, object>(builder.GetStaticAttributes());

            foreach (var kvp in builder.GetDynamicAttributes())
            {
                try
                {
                    object value = kvp.Value();
                    merged[kvp.Key] = value;
                    m_sessionAttributeCache[kvp.Key] = value;
                }
                catch (Exception e)
                {
                    OpButtonLog.Error("Session attribute provider failed: " + kvp.Key, e);
                }
            }

            return merged;
        }

        private Dictionary<string, object> ResolveChangedSessionAttributes()
        {
            var dynamicAttrs = m_config.SessionAttributes.GetDynamicAttributes();
            if (dynamicAttrs.Count == 0) return null;

            Dictionary<string, object> changed = null;
            foreach (var kvp in dynamicAttrs)
            {
                try
                {
                    object value = kvp.Value();
                    object cached;
                    m_sessionAttributeCache.TryGetValue(kvp.Key, out cached);

                    if (!Equals(value, cached))
                    {
                        m_sessionAttributeCache[kvp.Key] = value;
                        if (changed == null) changed = new Dictionary<string, object>();
                        changed[kvp.Key] = value;
                    }
                }
                catch (Exception e)
                {
                    OpButtonLog.Error("Session attribute provider failed: " + kvp.Key, e);
                }
            }

            return changed;
        }

        private static void BufferStructuredLog(string kind, string message, string category,
            Dictionary<string, object> metadata, long clientTimestampMs)
        {
            var p = s_instance;
            if (p == null || !p.m_config.LogConfig.IsEnabled()) return;

            string resolvedCategory = ResolveStructuredCategory(kind, category);
            var resolvedMetadata = MergeStructuredMetadata(kind, metadata);

            p.BufferLog(new OpButtonLogEntry(
                OpButtonLogLevel.Info, message, resolvedCategory, null,
                resolvedMetadata, p.ResolveLogAttributes(), clientTimestampMs));
        }

        private static string ResolveStructuredCategory(string kind, string category)
        {
            if (string.IsNullOrEmpty(category)) return kind;
            if (category == kind || category.StartsWith(kind + "_")) return category;
            return kind + "_" + category;
        }

        private static Dictionary<string, object> MergeStructuredMetadata(string kind,
            Dictionary<string, object> metadata)
        {
            var merged = new Dictionary<string, object>();
            if (metadata != null)
                foreach (var kvp in metadata)
                    merged[kvp.Key] = kvp.Value;
            merged["opbutton_kind"] = kind;
            return merged;
        }

        // ── Reconnect ──

        private void AttemptReconnect(string token)
        {
            CancelScheduledTasks();
            SetStatus(OpButtonSessionStatus.Connecting);

            long retryDelay = 2000;
            const long maxDelay = 30000;

            while (m_running)
            {
                Thread.Sleep((int)retryDelay);
                if (!m_running) return;

                try
                {
                    m_httpClient.PollActions(token, null, new List<OpButtonLogEntry>(), new List<OpButtonMetricEntry>());
                    OpButtonLog.Info("Reconnect successful, session still active");
                    SetStatus(OpButtonSessionStatus.Active);
                    ScheduleActionPolling(token, m_currentActionPollIntervalMs);
                    return;
                }
                catch (OpButtonException e)
                {
                    if (e.IsNetworkError)
                    {
                        OpButtonLog.Warn("Reconnect: no network, retrying in {0}ms", retryDelay);
                        retryDelay = Math.Min(retryDelay * 2, maxDelay);
                        continue;
                    }

                    OpButtonLog.Warn("Session lost (server error), starting fresh connection");
                    ResetAndRestart();
                    return;
                }
            }
        }

        private void ResetAndRestart()
        {
            m_running = false;
            m_sessionToken = null;
            m_requestId = null;
            SetStatus(OpButtonSessionStatus.Disconnected);

            StartConnection();
        }

        // ── Status and notifications ──

        private void SetStatus(OpButtonSessionStatus status) { m_status = status; }

        private void NotifyConnecting()
        {
            List<IOpButtonSessionListener> listeners;
            lock (s_lock) { listeners = new List<IOpButtonSessionListener>(m_sessionListeners); }
            foreach (var l in listeners) { try { l.OnOpButtonSessionConnecting(); } catch { } }
        }

        private void NotifyWaitingApproval(string requestId)
        {
            List<IOpButtonSessionListener> listeners;
            lock (s_lock) { listeners = new List<IOpButtonSessionListener>(m_sessionListeners); }
            foreach (var l in listeners) { try { l.OnOpButtonSessionWaitingApproval(requestId); } catch { } }
        }

        private void NotifySessionStarted(string sessionToken)
        {
            List<IOpButtonSessionListener> listeners;
            lock (s_lock) { listeners = new List<IOpButtonSessionListener>(m_sessionListeners); }
            foreach (var l in listeners) { try { l.OnOpButtonSessionStarted(sessionToken); } catch { } }
        }

        private void NotifySessionClosed()
        {
            List<IOpButtonSessionListener> listeners;
            lock (s_lock) { listeners = new List<IOpButtonSessionListener>(m_sessionListeners); }
            foreach (var l in listeners) { try { l.OnOpButtonSessionClosed(); } catch { } }
        }

        private void NotifyRejected()
        {
            List<IOpButtonSessionListener> listeners;
            lock (s_lock) { listeners = new List<IOpButtonSessionListener>(m_sessionListeners); }
            foreach (var l in listeners) { try { l.OnOpButtonSessionRejected(); } catch { } }
        }

        private void NotifyAuthFailed()
        {
            List<IOpButtonSessionListener> listeners;
            lock (s_lock) { listeners = new List<IOpButtonSessionListener>(m_sessionListeners); }
            foreach (var l in listeners) { try { l.OnOpButtonSessionAuthFailed(); } catch { } }
        }

        private void NotifyError(OpButtonException exception)
        {
            List<IOpButtonSessionListener> listeners;
            lock (s_lock) { listeners = new List<IOpButtonSessionListener>(m_sessionListeners); }
            foreach (var l in listeners) { try { l.OnOpButtonSessionError(exception); } catch { } }
        }

        // ── Main-thread dispatch ──

        private void RunOnMainThread(Action action)
        {
            lock (m_mainThreadLock) { m_mainThreadQueue.Add(action); }
        }

        private void ProcessMainThreadQueue()
        {
            List<Action> actions;
            lock (m_mainThreadLock)
            {
                if (m_mainThreadQueue.Count == 0) return;
                actions = new List<Action>(m_mainThreadQueue);
                m_mainThreadQueue.Clear();
            }

            foreach (var action in actions)
            {
                try { action(); }
                catch (Exception e) { OpButtonLog.Error("Main thread dispatch failed", e); }
            }
        }
    }
}
