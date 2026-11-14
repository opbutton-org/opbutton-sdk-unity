using System.Collections.Generic;

namespace OpButton.SDK
{
    /// <summary>
    /// Builder for session attributes — both static values and dynamic providers.
    /// </summary>
    public sealed class OpButtonSessionAttributeBuilder
    {
        private readonly Dictionary<string, object> m_static = new Dictionary<string, object>();
        private readonly Dictionary<string, OpButtonValueProvider> m_dynamic = new Dictionary<string, OpButtonValueProvider>();

        public OpButtonSessionAttributeBuilder Put(string key, object value)
        {
            m_static[key] = value;
            return this;
        }

        public OpButtonSessionAttributeBuilder PutProvider(string key, OpButtonValueProvider provider)
        {
            m_dynamic[key] = provider;
            return this;
        }

        internal Dictionary<string, object> GetStaticAttributes() => m_static;
        internal Dictionary<string, OpButtonValueProvider> GetDynamicAttributes() => m_dynamic;
    }
}
