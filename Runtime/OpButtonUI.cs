using System.Collections.Generic;
using System.Threading;

namespace OpButton.SDK
{
    /// <summary>
    /// Central UI data source for the OpButton SDK.
    /// </summary>
    public sealed class OpButtonUI
    {
        private readonly List<OpButtonTab> m_tabs = new List<OpButtonTab>();
        private readonly Dictionary<int, OpButtonWidgetCallback> m_callbacks = new Dictionary<int, OpButtonWidgetCallback>();
        private readonly HashSet<IOpButtonPolledWidget> m_providers = new HashSet<IOpButtonPolledWidget>();
        private int m_idCounter;
        private int m_version = 2;
        private int m_revision = 1;
        private volatile int m_sentRevision;
        private readonly object m_lock = new object();

        internal OpButtonUI() { }

        internal int NextId()
        {
            return Interlocked.Increment(ref m_idCounter);
        }

        // ── Tab management ──

        public OpButtonTab AddTab(string title)
        {
            lock (m_lock)
            {
                m_tabs.RemoveAll(t => { if (t.Title != title) return false; t.Detach(); return true; });
                var tab = new OpButtonTab(this, title);
                m_tabs.Add(tab);
                Interlocked.Increment(ref m_revision);
                return tab;
            }
        }

        public OpButtonTab GetTab(string id)
        {
            lock (m_lock)
            {
                foreach (var tab in m_tabs)
                {
                    if (tab.Id == id)
                        return tab;
                }
                return null;
            }
        }

        public void RemoveTab(string id)
        {
            lock (m_lock)
            {
                m_tabs.RemoveAll(t => { if (t.Id != id) return false; t.Detach(); return true; });
                Interlocked.Increment(ref m_revision);
            }
        }

        // ── Widget callbacks ──

        internal void RegisterCallback(int widgetId, OpButtonWidgetCallback callback)
        {
            lock (m_lock)
            {
                if (callback != null)
                    m_callbacks[widgetId] = callback;
                else
                    m_callbacks.Remove(widgetId);
            }
        }

        internal bool DispatchAction(OpButtonAction action)
        {
            OpButtonWidgetCallback cb;
            lock (m_lock)
            {
                if (!m_callbacks.TryGetValue(action.WidgetId, out cb))
                    return false;
            }
            cb(action);
            return true;
        }

        // ── Value providers ──

        internal void RegisterProvider<T>(OpButtonWidget<T> widget) where T : OpButtonWidget<T>
        {
            lock (m_lock) { m_providers.Add(widget); }
        }

        internal void UnregisterProvider<T>(OpButtonWidget<T> widget) where T : OpButtonWidget<T>
        {
            lock (m_lock) { m_providers.Remove(widget); }
        }

        internal void PollValues()
        {
            List<IOpButtonPolledWidget> providers;
            lock (m_lock) { providers = new List<IOpButtonPolledWidget>(m_providers); }
            foreach (var provider in providers)
            {
                if (provider.PollProvider()) Interlocked.Increment(ref m_revision);
            }
        }

        // ── Revision tracking ──

        internal void IncrementRevision()
        {
            Interlocked.Increment(ref m_revision);
        }

        internal bool HasUnsent()
        {
            return m_revision != m_sentRevision;
        }

        internal void MarkSent(int revision)
        {
            lock (m_lock) { m_sentRevision = System.Math.Max(m_sentRevision, revision); }
        }

        internal bool HasTabs()
        {
            lock (m_lock) { return m_tabs.Count > 0; }
        }

        public int Revision => m_revision;

        // ── Serialization ──

        internal Dictionary<string, object> ToDict()
        {
            lock (m_lock)
            {
                var tabsList = new List<object>();
                foreach (var tab in m_tabs)
                    tabsList.Add(tab.ToDict());

                return new Dictionary<string, object>
                {
                    ["version"] = m_version,
                    ["revision"] = m_revision,
                    ["tabs"] = tabsList
                };
            }
        }

        internal string ToJson()
        {
            return SimpleJson.Serialize(ToDict());
        }
    }
}
