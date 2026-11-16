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
        public const string VERSION = "1.0.32";

        private static volatile OpButtonRuntime s_instance;
        private static readonly object s_lock = new object();

        private OpButtonSDK() {}

        // ══════════════════════════════════════════════════════
        //  Static API
        // ══════════════════════════════════════════════════════

        public static void Initialize(OpButtonConfig config)
        {
            Initialize(config, null);
        }

        internal static void Initialize(OpButtonConfig config, OpButtonHttpClient httpClient)
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

                    var p = new OpButtonRuntime(config, httpClient);
                    s_instance = p;

                    // Ensure MonoBehaviour runner exists
                    OpButtonRunner.EnsureExists();

                    OpButtonLog.Info("OpButton SDK initialized (server: {0})", config.BaseUrl);
                    p.LogConfiguration();

                    if (config.AutoConnect)
                        p.StartConnection();
                }
            }
        }

        public static bool IsInitialized => s_instance != null;

        public static OpButtonSessionStatus GetStatus()
        {
            var p = s_instance;
            return p != null ? p.Status : OpButtonSessionStatus.Disconnected;
        }

        public static void AddActionListener(IOpButtonActionListener listener)
        {
            RequireInstance().AddActionListener(listener);
        }

        public static void RemoveActionListener(IOpButtonActionListener listener)
        {
            s_instance?.RemoveActionListener(listener);
        }

        public static void AddSessionListener(IOpButtonSessionListener listener)
        {
            RequireInstance().AddSessionListener(listener);
        }

        public static void RemoveSessionListener(IOpButtonSessionListener listener)
        {
            s_instance?.RemoveSessionListener(listener);
        }

        public static OpButtonUI GetUI()
        {
            return RequireInstance().UI;
        }

        public static OpButtonMetrics GetMetrics()
        {
            return RequireInstance().Metrics;
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
            if (p != null && p.IsLoggingEnabled)
                p.m_telemetry.BufferLog(new OpButtonLogEntry(level, message, null, null, null, p.m_telemetry.ResolveLogAttributes()));
        }

        public static void Log(OpButtonLogLevel level, string message, string category, string thread = null)
        {
            var p = s_instance;
            if (p != null && p.IsLoggingEnabled)
                p.m_telemetry.BufferLog(new OpButtonLogEntry(level, message, category, thread, null, p.m_telemetry.ResolveLogAttributes()));
        }

        public static void Log(OpButtonLogLevel level, string message, Dictionary<string, object> metadata)
        {
            var p = s_instance;
            if (p != null && p.IsLoggingEnabled)
                p.m_telemetry.BufferLog(new OpButtonLogEntry(level, message, null, null, metadata, p.m_telemetry.ResolveLogAttributes()));
        }

        public static void Log(OpButtonLogLevel level, string message, string category, string thread,
            Dictionary<string, object> metadata)
        {
            var p = s_instance;
            if (p != null && p.IsLoggingEnabled)
                p.m_telemetry.BufferLog(new OpButtonLogEntry(level, message, category, thread, metadata, p.m_telemetry.ResolveLogAttributes()));
        }

        public static void Log(OpButtonLogEntry entry)
        {
            var p = s_instance;
            if (p != null && p.IsLoggingEnabled)
                p.m_telemetry.BufferLog(entry);
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
            s_instance?.AcknowledgeAction(actionId, ackPayload);
        }

        public static void Shutdown()
        {
            OpButtonRuntime p;
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

        internal static void OnUpdate() { s_instance?.Update(); }

        internal static void OnApplicationQuit()
        {
            Shutdown();
        }

        // ══════════════════════════════════════════════════════
        //  Internal implementation
        // ══════════════════════════════════════════════════════

        private static OpButtonRuntime RequireInstance()
        {
            var p = s_instance;
            if (p == null)
                throw new InvalidOperationException("OpButton.Initialize() must be called first");
            return p;
        }

        private static void BufferStructuredLog(string kind, string message, string category,
            Dictionary<string, object> metadata, long clientTimestampMs)
        {
            var p = s_instance;
            if (p == null || !p.IsLoggingEnabled) return;

            string resolvedCategory = ResolveStructuredCategory(kind, category);
            var resolvedMetadata = MergeStructuredMetadata(kind, metadata);

            p.m_telemetry.BufferLog(new OpButtonLogEntry(
                OpButtonLogLevel.Info, message, resolvedCategory, null,
                resolvedMetadata, p.m_telemetry.ResolveLogAttributes(), clientTimestampMs));
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
    }
}
