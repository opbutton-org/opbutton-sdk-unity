using System.Collections.Generic;

namespace OpButton.SDK
{
    /// <summary>Commits delivery state only after its detached snapshot is accepted.</summary>
    internal sealed class OpButtonAttributeJournal
    {
        private readonly Dictionary<string, string> m_acknowledged = new Dictionary<string, string>();
        private readonly object m_lock = new object();

        internal Dictionary<string, object> Snapshot(Dictionary<string, object> values)
        {
            lock (m_lock)
            {
                var changed = new Dictionary<string, object>();
                foreach (var entry in Capture(values))
                {
                    string encoded = Encode(entry.Value);
                    if (!m_acknowledged.TryGetValue(entry.Key, out var previous) || encoded != previous)
                        changed[entry.Key] = entry.Value;
                }
                return changed;
            }
        }

        internal static Dictionary<string, object> Capture(Dictionary<string, object> values)
        {
            return SimpleJson.Parse(SimpleJson.Serialize(values)).ToDictionary();
        }

        internal void Acknowledge(Dictionary<string, object> snapshot)
        {
            if (snapshot == null) return;
            lock (m_lock)
                foreach (var entry in snapshot) m_acknowledged[entry.Key] = Encode(entry.Value);
        }

        private static string Encode(object value)
        {
            return SimpleJson.Serialize(new Dictionary<string, object> { ["value"] = value });
        }
    }
}
