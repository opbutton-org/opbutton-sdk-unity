using System.Collections.Generic;

namespace OpButton.SDK
{
    /// <summary>
    /// A tab in the OpButton dashboard UI.
    /// </summary>
    public sealed class OpButtonTab
    {
        private readonly OpButtonUI m_ui;
        private readonly int m_internalId;
        private string m_publicId;
        private readonly string m_title;
        private OpButtonLayout m_layout;

        internal OpButtonTab(OpButtonUI ui, string title)
        {
            m_ui = ui;
            m_internalId = ui.NextId();
            m_publicId = "tab-" + m_internalId;
            m_title = title;
        }

        public string Id => m_publicId;

        public OpButtonTab SetId(string id)
        {
            m_publicId = id;
            return this;
        }

        public int InternalId => m_internalId;
        public string Title => m_title;
        public OpButtonLayout Layout => m_layout;

        public OpButtonLayout Vertical()
        {
            m_layout = new OpButtonLayout(m_ui, OpButtonLayout.Direction.Vertical);
            return m_layout;
        }

        public OpButtonLayout Horizontal()
        {
            m_layout = new OpButtonLayout(m_ui, OpButtonLayout.Direction.Horizontal);
            return m_layout;
        }

        internal Dictionary<string, object> ToDict()
        {
            var dict = new Dictionary<string, object>
            {
                ["id"] = m_internalId,
                ["title"] = m_title
            };

            if (m_layout != null)
                dict["layout"] = m_layout.ToDict();

            return dict;
        }
    }
}
