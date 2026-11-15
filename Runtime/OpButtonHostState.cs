using System;
using System.Collections.Generic;
using System.Threading;

namespace OpButton.SDK
{
    /// <summary>Reads host-owned state and acknowledges only the captured values.</summary>
    internal sealed class OpButtonHostState : OpButtonPollDelivery.IState
    {
        internal sealed class Unavailable : Exception
        {
            internal Unavailable(OpButtonException cause) : base(cause.Message, cause) { }
        }

        private readonly OpButtonSessionAttributeBuilder m_attributes;
        private readonly OpButtonUI m_ui;
        private readonly OpButtonAttributeJournal m_attributeJournal = new OpButtonAttributeJournal();

        internal OpButtonHostState(OpButtonSessionAttributeBuilder attributes, OpButtonUI ui)
        {
            m_attributes = attributes;
            m_ui = ui;
        }

        public OpButtonPollDelivery.Snapshot Capture(CancellationToken cancellation)
        {
            try
            {
                return new OpButtonPollDelivery.Snapshot(ResolveChangedSessionAttributes(cancellation), CapturePanel(true, cancellation));
            }
            catch (OpButtonException error) { throw new Unavailable(error); }
        }

        public void PanelSent(OpButtonPollDelivery.Snapshot snapshot) { m_ui.MarkSent(Convert.ToInt32(snapshot.Panel["revision"])); }
        public void AttributesSent(OpButtonPollDelivery.Snapshot snapshot) { AcknowledgeAll(snapshot.Attributes); }
        internal void AcknowledgeAll(Dictionary<string, object> attributes) { m_attributeJournal.Acknowledge(attributes); }
        internal Dictionary<string, object> CapturePanel(bool onlyIfChanged, CancellationToken cancellation)
        {
            return OpButtonRunner.InvokeOnMainThread(() =>
            {
                if (onlyIfChanged && !m_ui.HasUnsent()) return null;
                // Serialize on the host thread, detaching nested widget dictionaries.
                return SimpleJson.Parse(SimpleJson.Serialize(m_ui.ToDict())).ToDictionary();
            }, cancellation: cancellation);
        }

        private Dictionary<string, object> ResolveDynamicSessionAttributes()
        {
            var values = new Dictionary<string, object>();
            foreach (var entry in m_attributes.GetDynamicAttributes())
            {
                try { values[entry.Key] = entry.Value(); }
                catch (Exception e) { OpButtonLog.Error("Session attribute provider failed: " + entry.Key, e); }
            }
            return values;
        }

        internal Dictionary<string, object> ResolveAllSessionAttributes(CancellationToken cancellation)
        {
            return OpButtonRunner.InvokeOnMainThread(() =>
            {
                var values = new Dictionary<string, object>(m_attributes.GetStaticAttributes());
                foreach (var entry in ResolveDynamicSessionAttributes()) values[entry.Key] = entry.Value;
                return OpButtonAttributeJournal.Capture(values);
            }, cancellation: cancellation);
        }

        private Dictionary<string, object> ResolveChangedSessionAttributes(CancellationToken cancellation)
        {
            return OpButtonRunner.InvokeOnMainThread(() => m_attributeJournal.Snapshot(ResolveDynamicSessionAttributes()), cancellation: cancellation);
        }
    }
}
