using System;
using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;

namespace OpButton.SDK
{
    public sealed class RuntimeContractsTests
    {
        private OpButtonMetrics m_metrics;
        private OpButtonTelemetry m_telemetry;
        private State m_state;
        private Peer m_peer;
        private OpButtonPollDelivery m_delivery;
        private bool m_current;
        private int m_panelErrors;

        [SetUp]
        public void CreateRuntimePorts()
        {
            OpButtonLog.SetLoggerListener(new SilentLogger());
            m_metrics = new OpButtonMetrics();
            m_telemetry = new OpButtonTelemetry(new OpButtonLogConfigBuilder(), null, m_metrics);
            m_state = new State();
            m_peer = new Peer();
            m_current = true;
            m_panelErrors = 0;
            m_delivery = new OpButtonPollDelivery(m_peer, m_state, m_telemetry, token => m_current, error => m_panelErrors++);
        }

        [TearDown]
        public void RestoreLogger() { OpButtonLog.SetLoggerListener(null); }

        [Test]
        public void FailedPollRestoresTelemetryInOrderAndDoesNotCommitAttributes()
        {
            var first = OpButtonLogEntry.Info("first");
            var later = OpButtonLogEntry.Info("later");
            m_telemetry.BufferLog(first);
            m_metrics.Record(OpButtonMetricType.CpuUsage, 1);
            m_peer.OnPoll = () =>
            {
                m_telemetry.BufferLog(later);
                m_metrics.Record(OpButtonMetricType.CpuUsage, 2);
                m_state.Values["level"] = 2;
            };
            m_peer.FailPoll = true;
            Assert.Throws<OpButtonException>(() => m_delivery.Exchange("session", CancellationToken.None));
            Assert.AreEqual(0, m_state.AttributesAccepted);
            m_peer.OnPoll = null;
            m_peer.FailPoll = false;
            m_delivery.Exchange("session", CancellationToken.None);
            CollectionAssert.AreEqual(new[] { first, later }, m_peer.Logs);
            Assert.AreEqual(1, m_peer.Samples[0].Value);
            Assert.AreEqual(2, m_peer.Samples[1].Value);
            Assert.AreEqual(2, Convert.ToInt32(m_peer.Attributes["level"]));
            Assert.IsEmpty(m_state.Journal.Snapshot(m_state.Values));
            using (var empty = m_telemetry.Capture()) { Assert.IsEmpty(empty.Logs); }
        }

        [Test]
        public void FailedPanelDoesNotBlockPollingOrAcknowledgeThePanel()
        {
            m_state.Panel = new Dictionary<string, object> { ["revision"] = 7 };
            m_peer.FailPanel = true;
            Assert.AreSame(m_peer.Response, m_delivery.Exchange("session", CancellationToken.None));
            Assert.AreEqual(0, m_state.PanelsAccepted);
            Assert.AreEqual(1, m_state.AttributesAccepted);
            Assert.AreEqual(1, m_peer.Polls);
            Assert.AreEqual(1, m_panelErrors);
        }

        [Test]
        public void LateResponseDoesNotAcknowledgeAnotherSession()
        {
            m_peer.OnPoll = () => m_current = false;
            m_delivery.Exchange("old-session", CancellationToken.None);
            Assert.AreEqual(0, m_state.AttributesAccepted);
            Assert.IsNotEmpty(m_state.Journal.Snapshot(m_state.Values));
        }

        [Test]
        public void ProbePreservesTheOutboxAndReturnsTheResponse()
        {
            m_telemetry.BufferLog(OpButtonLogEntry.Info("pending"));
            Assert.AreSame(m_peer.Response, m_delivery.Probe("session"));
            Assert.AreEqual(0, m_state.Captures);
            Assert.IsNull(m_peer.Attributes);
            Assert.IsEmpty(m_peer.Logs);
            using (var pending = m_telemetry.Capture()) { Assert.AreEqual(1, pending.Logs.Count); }
        }

        [Test]
        public void BatchSettlementIsIdempotentAndCommittedDataStaysConsumed()
        {
            m_telemetry.BufferLog(OpButtonLogEntry.Info("pending"));
            var first = m_telemetry.Capture();
            first.Dispose();
            first.Dispose();
            using (var retry = m_telemetry.Capture())
            {
                Assert.AreEqual(1, retry.Logs.Count);
                retry.Commit();
                retry.Commit();
            }
            using (var empty = m_telemetry.Capture()) { Assert.IsEmpty(empty.Logs); }
        }

        [Test]
        public void UnavailableHostDoesNotDrainTheOutboxOrCallTheTransport()
        {
            m_telemetry.BufferLog(OpButtonLogEntry.Info("pending"));
            m_state.Unavailable = true;
            Assert.Throws<OpButtonHostState.Unavailable>(() => m_delivery.Exchange("session", CancellationToken.None));
            Assert.AreEqual(0, m_peer.Polls);
            using (var pending = m_telemetry.Capture()) { Assert.AreEqual(1, pending.Logs.Count); }
        }

        [Test]
        public void ReservedCommandsAreExclusiveAndKeepTheirCapturedSession()
        {
            var calls = new List<string>();
            var commands = new Dictionary<OpButtonActionType, OpButtonActionRouter.Command>
            {
                [OpButtonActionType.LiveStart] = (action, token) => calls.Add(token)
            };
            var router = new OpButtonActionRouter(commands, action => calls.Add("widget"),
                () => new List<IOpButtonActionListener> { new Listener(action => calls.Add("listener")) });
            commands.Clear();
            router.Dispatch(CreateAction(OpButtonActionType.LiveStart), "original-session");
            CollectionAssert.AreEqual(new[] { "original-session" }, calls);
        }

        [Test]
        public void CallbackFailuresAndSelfRemovalDoNotSkipOtherObservers()
        {
            var calls = new List<string>();
            var listeners = new List<IOpButtonActionListener>();
            listeners.Add(new Listener(action => { calls.Add("first"); listeners.RemoveAt(0); throw new InvalidOperationException("listener"); }));
            listeners.Add(new Listener(action => calls.Add("second")));
            var router = new OpButtonActionRouter(new Dictionary<OpButtonActionType, OpButtonActionRouter.Command>(),
                action => { calls.Add("widget"); throw new InvalidOperationException("widget"); },
                () => new List<IOpButtonActionListener>(listeners));
            router.Dispatch(CreateAction(OpButtonActionType.Click), "session");
            CollectionAssert.AreEqual(new[] { "widget", "first", "second" }, calls);
        }

        private static OpButtonAction CreateAction(OpButtonActionType type)
        {
            return new OpButtonAction("action", "session", 1, type, OpButtonActionStatus.Delivered, null);
        }

        private sealed class Listener : IOpButtonActionListener
        {
            private readonly Action<OpButtonAction> m_callback;
            internal Listener(Action<OpButtonAction> callback) { m_callback = callback; }
            public void OnOpButtonActionReceived(OpButtonAction action) { m_callback(action); }
        }

        private sealed class SilentLogger : IOpButtonLoggerListener
        {
            public void OnOpButtonLoggerMessage(OpButtonLogLevel level, string tag, string message, Exception error) { }
        }

        private sealed class State : OpButtonPollDelivery.IState
        {
            internal readonly OpButtonAttributeJournal Journal = new OpButtonAttributeJournal();
            internal readonly Dictionary<string, object> Values = new Dictionary<string, object> { ["level"] = 1 };
            internal Dictionary<string, object> Panel;
            internal int Captures;
            internal int PanelsAccepted;
            internal int AttributesAccepted;
            internal bool Unavailable;

            public OpButtonPollDelivery.Snapshot Capture(CancellationToken cancellation)
            {
                if (Unavailable) throw new OpButtonHostState.Unavailable(new OpButtonException("Host paused"));
                Captures++;
                return new OpButtonPollDelivery.Snapshot(Journal.Snapshot(Values), Panel);
            }
            public void PanelSent(OpButtonPollDelivery.Snapshot snapshot) { PanelsAccepted++; }
            public void AttributesSent(OpButtonPollDelivery.Snapshot snapshot)
            {
                AttributesAccepted++;
                Journal.Acknowledge(snapshot.Attributes);
            }
        }

        private sealed class Peer : OpButtonPollDelivery.ITransport
        {
            internal readonly SimpleJson Response = new SimpleJson(new Dictionary<string, object>());
            internal Action OnPoll;
            internal bool FailPanel;
            internal bool FailPoll;
            internal int Polls;
            internal Dictionary<string, object> Attributes;
            internal List<OpButtonLogEntry> Logs;
            internal List<OpButtonMetricEntry> Samples;

            public SimpleJson SubmitPanel(string token, Dictionary<string, object> panel)
            {
                if (FailPanel) throw new OpButtonException(503, "Panel unavailable");
                return Response;
            }
            public SimpleJson PollActions(string token, Dictionary<string, object> attributes,
                List<OpButtonLogEntry> logs, List<OpButtonMetricEntry> samples)
            {
                Polls++;
                Attributes = attributes;
                Logs = new List<OpButtonLogEntry>(logs);
                Samples = new List<OpButtonMetricEntry>(samples);
                OnPoll?.Invoke();
                if (FailPoll) throw new OpButtonException(503, "Poll unavailable");
                return Response;
            }
        }
    }
}
