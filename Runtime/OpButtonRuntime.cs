using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace OpButton.SDK
{
    /// <summary>Coordinates one initialized SDK instance behind the public facade.</summary>
    internal sealed class OpButtonRuntime
    {
        private readonly object m_listenerLock = new object();
        private readonly OpButtonConfig m_config;
        private readonly OpButtonHttpClient m_httpClient;

        private volatile string m_sessionToken;
        private volatile string m_requestId;
        private volatile OpButtonSessionStatus m_status = OpButtonSessionStatus.Disconnected;
        private volatile bool m_running;
        private int m_actionPollInFlight;

        internal readonly OpButtonTelemetry m_telemetry;
        private readonly OpButtonHostState m_hostState;
        private readonly OpButtonPollDelivery m_delivery;
        private readonly string m_deviceId;
        private readonly string m_deviceName;

        private readonly List<IOpButtonActionListener> m_actionListeners = new List<IOpButtonActionListener>();
        private readonly List<IOpButtonSessionListener> m_sessionListeners = new List<IOpButtonSessionListener>();

        private Thread m_connectionThread;
        private OpButtonSessionWork m_sessionWork;
        private long m_currentActionPollIntervalMs;

        private readonly OpButtonUI m_ui = new OpButtonUI();
        private readonly OpButtonMetrics m_metrics = new OpButtonMetrics();
        private readonly OpButtonDefaultMetricCollector m_defaultCollector = new OpButtonDefaultMetricCollector();
        private readonly OpButtonLiveManager m_liveManager;
        private readonly OpButtonActionRouter m_actions;

        // Main-thread dispatch queue
        private readonly List<Action> m_mainThreadQueue = new List<Action>();
        private readonly object m_mainThreadLock = new object();

        internal OpButtonRuntime(OpButtonConfig config, OpButtonHttpClient httpClient)
        {
            m_config = config;
            m_deviceId = string.IsNullOrEmpty(config.DeviceId) ? SystemInfo.deviceUniqueIdentifier : config.DeviceId;
            m_deviceName = string.IsNullOrEmpty(config.DeviceName)
                ? SystemInfo.deviceModel + " (Unity " + Application.unityVersion + ")" : config.DeviceName;
            m_httpClient = httpClient ?? new OpButtonHttpClient(config.BaseUrl, config.ApiToken);
            m_telemetry = new OpButtonTelemetry(config.LogConfig, m_httpClient, m_metrics);
            m_hostState = new OpButtonHostState(config.SessionAttributes, m_ui);
            m_delivery = new OpButtonPollDelivery(m_httpClient, m_hostState, m_telemetry, IsCurrentSession,
                error => RunOnMainThread(() => NotifyError(error)));
            m_currentActionPollIntervalMs = config.ActionPollIntervalMs;
            OpButtonLog.SetLevel(config.LogConfig.GetLogLevel());
            OpButtonLog.SetLoggerListener(config.LoggerListener);

            m_liveManager = new OpButtonLiveManager(m_httpClient);
            var commands = new OpButtonLiveCommands(m_liveManager,
                work => ThreadPool.QueueUserWorkItem(_ => work()), IsCurrentSession, AcknowledgeForSession);
            m_actions = new OpButtonActionRouter(commands.Commands(), action => m_ui.DispatchAction(action),
                () => { lock (m_listenerLock) { return new List<IOpButtonActionListener>(m_actionListeners); } });
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
            if (config.SessionListener != null)
                m_sessionListeners.Add(config.SessionListener);
            if (config.ActionListener != null)
                m_actionListeners.Add(config.ActionListener);

            if (mc.IsEnabled())
            {
                m_metrics.AddCollector(m_defaultCollector);
                foreach (var collector in mc.GetCollectors())
                    m_metrics.AddCollector(collector);
            }

        }

        internal void LogConfiguration()
        {
            if (m_config.MetricConfig.IsEnabled())
                OpButtonLog.Info("Built-in metrics enabled (sample interval: {0}ms)", m_config.MetricConfig.GetSampleIntervalMs());
        }

        internal bool IsLoggingEnabled => m_config.LogConfig.IsEnabled();
        internal OpButtonSessionStatus Status => m_status;
        internal OpButtonUI UI => m_ui;
        internal OpButtonMetrics Metrics => m_metrics;
        internal void AddActionListener(IOpButtonActionListener listener) { lock (m_listenerLock) { m_actionListeners.Add(listener); } }
        internal void RemoveActionListener(IOpButtonActionListener listener) { lock (m_listenerLock) { m_actionListeners.Remove(listener); } }
        internal void AddSessionListener(IOpButtonSessionListener listener) { lock (m_listenerLock) { m_sessionListeners.Add(listener); } }
        internal void RemoveSessionListener(IOpButtonSessionListener listener) { lock (m_listenerLock) { m_sessionListeners.Remove(listener); } }

        internal void StartConnection()
        {
            if (m_running) { OpButtonLog.Warn("Already connecting/connected"); return; }
            m_running = true;
            var work = new OpButtonSessionWork();
            m_sessionWork = work;
            m_connectionThread = new Thread(() => ConnectAndWaitApproval(work)) { IsBackground = true, Name = "OpButtonSDK-Connect" };
            m_connectionThread.Start();
        }

        internal void StopConnection()
        {
            if (!m_running) return;
            m_running = false;
            m_sessionWork?.Dispose();

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
                        m_telemetry.FlushLogs(token);
                        m_telemetry.FlushMetrics(token);
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

        internal void DoShutdown()
        {
            OpButtonLog.Info("Shutting down OpButton SDK");
            m_liveManager.Shutdown();
            StopConnection();
            m_httpClient.Shutdown();
            lock (m_listenerLock)
            {
                m_actionListeners.Clear();
                m_sessionListeners.Clear();
            }
            m_telemetry.Clear();
            m_metrics.Clear();
        }

        private void ConnectAndWaitApproval(OpButtonSessionWork work)
        {
            var cancellation = work.Cancellation;
            int retryCount = 0;
            long retryDelayMs = 2000;
            const long maxRetryDelayMs = 30000;

            while (m_running && !cancellation.IsCancellationRequested)
            {
                try
                {
                    DoConnectAndWaitApproval(work);
                    return;
                }
                catch (OperationCanceledException) { return; }
                catch (OpButtonException e)
                {
                    if (cancellation.IsCancellationRequested) return;
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

                    if (cancellation.WaitHandle.WaitOne((int)retryDelayMs)) return;
                    retryDelayMs = Math.Min(retryDelayMs * 2, maxRetryDelayMs);
                }
            }
        }

        private void DoConnectAndWaitApproval(OpButtonSessionWork work)
        {
            var cancellation = work.Cancellation;
            SetStatus(OpButtonSessionStatus.Connecting);
            RunOnMainThread(() => NotifyConnecting());

            var attributes = m_hostState.ResolveAllSessionAttributes(cancellation);
            var resp = m_httpClient.Connect(m_deviceId, m_deviceName, attributes);
            cancellation.ThrowIfCancellationRequested();
            m_hostState.AcknowledgeAll(attributes);
            m_requestId = resp.RequestId;

            OpButtonLog.Info("Connect request sent, request_id={0}, status={1}", resp.RequestId, resp.Status);

            if (resp.IsApproved && resp.SessionToken != null)
            {
                OnApproved(resp.SessionToken, work);
                return;
            }

            if (resp.IsRejected)
            {
                OnRejected();
                return;
            }

            SetStatus(OpButtonSessionStatus.WaitingApproval);
            RunOnMainThread(() => NotifyWaitingApproval(resp.RequestId));

            while (m_running && !cancellation.IsCancellationRequested)
            {
                if (cancellation.WaitHandle.WaitOne((int)m_config.PollIntervalMs)) return;

                var pollResp = m_httpClient.PollStatus(resp.RequestId);
                cancellation.ThrowIfCancellationRequested();

                if (pollResp.IsApproved && pollResp.SessionToken != null)
                {
                    OnApproved(pollResp.SessionToken, work);
                    return;
                }

                if (pollResp.IsRejected)
                {
                    OnRejected();
                    return;
                }
            }
        }

        private void OnApproved(string sessionToken, OpButtonSessionWork work)
        {
            var cancellation = work.Cancellation;
            cancellation.ThrowIfCancellationRequested();
            m_sessionToken = sessionToken;
            SetStatus(OpButtonSessionStatus.Active);

            OpButtonLog.Info("Session approved and active");

            RunOnMainThread(() => NotifySessionStarted(sessionToken));

            // Send initial UI
            if (m_ui.HasTabs())
            {
                var snapshot = m_hostState.CapturePanel(false, cancellation);
                try
                {
                    m_httpClient.SubmitPanel(sessionToken, snapshot);
                    m_ui.MarkSent(System.Convert.ToInt32(snapshot["revision"]));
                    OpButtonLog.Debug("Initial UI submitted (revision={0})", m_ui.Revision);
                }
                catch (OpButtonException e)
                {
                    OpButtonLog.Error("Failed to submit initial UI", e);
                    RunOnMainThread(() => NotifyError(e));
                }
            }

            // Start action polling
            ScheduleActionPolling(sessionToken, m_config.ActionPollIntervalMs, work);

            // Start metric sampling
            if (m_config.MetricConfig.IsEnabled())
            {
                long sampleMs = m_metrics.GetSampleIntervalMs();
                work.ScheduleMetrics(m_metrics.Sample, sampleMs);
            }
        }

        private void OnRejected()
        {
            OpButtonLog.Warn("Connection request rejected");
            SetStatus(OpButtonSessionStatus.Rejected);
            m_running = false;
            RunOnMainThread(() => NotifyRejected());
        }

        private void DoPollActions(string sessionToken, CancellationToken cancellation)
        {
            if (!IsCurrentSession(sessionToken) || cancellation.IsCancellationRequested ||
                Interlocked.CompareExchange(ref m_actionPollInFlight, 1, 0) != 0) return;
            try
            {
                var response = m_delivery.Exchange(sessionToken, cancellation);
                ProcessPollResponse(sessionToken, response);
            }
            catch (OperationCanceledException) { }
            catch (OpButtonHostState.Unavailable error) { OpButtonLog.Warn("Host state unavailable: {0}", error.Message); }
            catch (OpButtonException error)
            {
                if (!IsCurrentSession(sessionToken)) return;
                if (error.IsNetworkError)
                    OpButtonLog.Warn("Network or host state unavailable during poll: {0}", error.Message);
                else
                    AttemptReconnect(sessionToken);
            }
            finally { Volatile.Write(ref m_actionPollInFlight, 0); }
        }

        private bool IsCurrentSession(string token) { return m_running && token == m_sessionToken; }

        private void ProcessPollResponse(string token, SimpleJson response)
        {
            if (!IsCurrentSession(token)) return;
            var actions = response.GetArray("actions");
            if (actions == null) return;
            foreach (var item in actions)
            {
                if (item is Dictionary<string, object> data)
                {
                    var action = OpButtonAction.FromDict(new SimpleJson(data));
                    RunOnMainThread(() => { if (IsCurrentSession(token)) m_actions.Dispatch(action, token); });
                }
            }
        }

        private void ScheduleActionPolling(string sessionToken, long intervalMs, OpButtonSessionWork work = null)
        {
            if (!IsCurrentSession(sessionToken)) return;
            var owner = work ?? m_sessionWork;
            var cancellation = owner.Cancellation;
            if (owner.ScheduleActions(() => DoPollActions(sessionToken, cancellation), intervalMs))
                m_currentActionPollIntervalMs = intervalMs;
        }

        private void CancelActionPolling() { m_sessionWork?.PauseActions(); }
        private void CancelScheduledTasks() { m_sessionWork?.Dispose(); }

        // ── Log buffering ──

        // ── Attribute resolution ──

        // ── Reconnect ──

        private void AttemptReconnect(string token)
        {
            CancelActionPolling();
            SetStatus(OpButtonSessionStatus.Connecting);

            long retryDelay = 2000;
            const long maxDelay = 30000;

            var cancellation = m_sessionWork.Cancellation;
            while (IsCurrentSession(token) && !cancellation.IsCancellationRequested)
            {
                if (cancellation.WaitHandle.WaitOne((int)retryDelay)) return;
                if (!IsCurrentSession(token)) return;

                try
                {
                    var response = m_delivery.Probe(token);
                    if (!IsCurrentSession(token)) return;
                    ProcessPollResponse(token, response);
                    OpButtonLog.Info("Reconnect successful, session still active");
                    SetStatus(OpButtonSessionStatus.Active);
                    ScheduleActionPolling(token, m_currentActionPollIntervalMs);
                    return;
                }
                catch (OpButtonException e)
                {
                    if (!IsCurrentSession(token)) return;
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
            m_sessionWork?.Dispose();
            CancelScheduledTasks();
            m_liveManager.OnSessionClosed();
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
            lock (m_listenerLock) { listeners = new List<IOpButtonSessionListener>(m_sessionListeners); }
            foreach (var l in listeners) { try { l.OnOpButtonSessionConnecting(); } catch { } }
        }

        private void NotifyWaitingApproval(string requestId)
        {
            List<IOpButtonSessionListener> listeners;
            lock (m_listenerLock) { listeners = new List<IOpButtonSessionListener>(m_sessionListeners); }
            foreach (var l in listeners) { try { l.OnOpButtonSessionWaitingApproval(requestId); } catch { } }
        }

        private void NotifySessionStarted(string sessionToken)
        {
            List<IOpButtonSessionListener> listeners;
            lock (m_listenerLock) { listeners = new List<IOpButtonSessionListener>(m_sessionListeners); }
            foreach (var l in listeners) { try { l.OnOpButtonSessionStarted(sessionToken); } catch { } }
        }

        private void NotifySessionClosed()
        {
            List<IOpButtonSessionListener> listeners;
            lock (m_listenerLock) { listeners = new List<IOpButtonSessionListener>(m_sessionListeners); }
            foreach (var l in listeners) { try { l.OnOpButtonSessionClosed(); } catch { } }
        }

        private void NotifyRejected()
        {
            List<IOpButtonSessionListener> listeners;
            lock (m_listenerLock) { listeners = new List<IOpButtonSessionListener>(m_sessionListeners); }
            foreach (var l in listeners) { try { l.OnOpButtonSessionRejected(); } catch { } }
        }

        private void NotifyAuthFailed()
        {
            List<IOpButtonSessionListener> listeners;
            lock (m_listenerLock) { listeners = new List<IOpButtonSessionListener>(m_sessionListeners); }
            foreach (var l in listeners) { try { l.OnOpButtonSessionAuthFailed(); } catch { } }
        }

        private void NotifyError(OpButtonException exception)
        {
            List<IOpButtonSessionListener> listeners;
            lock (m_listenerLock) { listeners = new List<IOpButtonSessionListener>(m_sessionListeners); }
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

        internal void Update()
        {
            // Flush deferred log messages from background threads
            OpButtonLog.FlushDeferred();

            // Update delta time for FPS tracking
            m_defaultCollector.UpdateDeltaTime(Time.unscaledDeltaTime);

            // Cache Unity API values that can only be read from main thread
            m_defaultCollector.UpdateFromMainThread();

            // Poll widget value providers on the main thread (providers may access Unity APIs)
            m_ui.PollValues();

            // Process main-thread dispatch queue
            ProcessMainThreadQueue();
        }

        internal void AcknowledgeAction(string actionId, Dictionary<string, object> payload)
        {
            string token = m_sessionToken;
            if (token != null) AcknowledgeForSession(token, actionId, payload);
        }

        private void AcknowledgeForSession(string token, string actionId, Dictionary<string, object> payload)
        {
            ThreadPool.QueueUserWorkItem(_ =>
            {
                if (!IsCurrentSession(token)) return;
                try { m_httpClient.AcknowledgeAction(token, actionId, payload); }
                catch (OpButtonException error) { OpButtonLog.Error("Failed to acknowledge action", error); }
            });
        }
    }
}
