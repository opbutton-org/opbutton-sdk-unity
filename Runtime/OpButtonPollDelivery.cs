using System;
using System.Collections.Generic;
using System.Threading;

namespace OpButton.SDK
{
    /// <summary>A poll is a delivery transaction; an unfinished telemetry receipt rolls back.</summary>
    internal sealed class OpButtonPollDelivery
    {
        internal interface ITransport
        {
            SimpleJson SubmitPanel(string token, Dictionary<string, object> panel);
            SimpleJson PollActions(string token, Dictionary<string, object> attributes,
                List<OpButtonLogEntry> logs, List<OpButtonMetricEntry> metrics);
        }

        internal interface IState
        {
            Snapshot Capture(CancellationToken cancellation);
            void PanelSent(Snapshot snapshot);
            void AttributesSent(Snapshot snapshot);
        }

        internal sealed class Snapshot
        {
            internal readonly Dictionary<string, object> Attributes;
            internal readonly Dictionary<string, object> Panel;

            internal Snapshot(Dictionary<string, object> attributes, Dictionary<string, object> panel)
            {
                Attributes = attributes;
                Panel = panel;
            }
        }

        private readonly ITransport m_transport;
        private readonly IState m_state;
        private readonly OpButtonTelemetry m_telemetry;
        private readonly Func<string, bool> m_isCurrent;
        private readonly Action<OpButtonException> m_onPanelError;

        internal OpButtonPollDelivery(ITransport transport, IState state, OpButtonTelemetry telemetry,
            Func<string, bool> isCurrent, Action<OpButtonException> onPanelError)
        {
            m_transport = transport;
            m_state = state;
            m_telemetry = telemetry;
            m_isCurrent = isCurrent;
            m_onPanelError = onPanelError;
        }

        internal SimpleJson Exchange(string token, CancellationToken cancellation)
        {
            var snapshot = m_state.Capture(cancellation);
            cancellation.ThrowIfCancellationRequested();
            if (snapshot.Panel != null)
            {
                try
                {
                    m_transport.SubmitPanel(token, snapshot.Panel);
                    if (m_isCurrent(token)) m_state.PanelSent(snapshot);
                }
                catch (OpButtonException error)
                {
                    OpButtonLog.Error("Failed to submit UI", error);
                    m_onPanelError(error);
                }
            }

            using (var batch = m_telemetry.Capture())
            {
                cancellation.ThrowIfCancellationRequested();
                var response = m_transport.PollActions(token, snapshot.Attributes, batch.Logs, batch.Metrics);
                if (m_isCurrent(token)) m_state.AttributesSent(snapshot);
                batch.Commit();
                return response;
            }
        }

        internal SimpleJson Probe(string token)
        {
            return m_transport.PollActions(token, null, new List<OpButtonLogEntry>(), new List<OpButtonMetricEntry>());
        }
    }
}
