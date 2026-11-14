using System.Collections.Generic;

namespace OpButton.SDK
{
    /// <summary>
    /// Select (dropdown) widget.
    /// </summary>
    public sealed class OpButtonSelect : OpButtonWidget<OpButtonSelect>
    {
        internal OpButtonSelect(OpButtonUI ui, string label) : base(ui, "select")
        {
            Put("label", label);
        }

        public OpButtonSelect Options(string[][] options)
        {
            var list = new List<object>();
            foreach (var opt in options)
            {
                list.Add(new Dictionary<string, object>
                {
                    ["value"] = opt[0],
                    ["label"] = opt[1]
                });
            }
            Put("options", list);
            return this;
        }

        public OpButtonSelect DefaultValue(string value) { Put("defaultValue", value); return this; }

        /// <summary>
        /// Register a typed change handler that auto-acknowledges after the callback completes.
        /// </summary>
        public OpButtonSelect OnChange(System.Action<OpButtonSelectAction> callback)
        {
            m_ui.RegisterCallback(m_internalId, action =>
            {
                callback(new OpButtonSelectAction(action));
                OpButtonSDK.AcknowledgeAction(action.Id);
            });
            return this;
        }

        /// <summary>
        /// Register a change handler that auto-acknowledges after the callback completes.
        /// </summary>
        public OpButtonSelect OnChange(System.Action<OpButtonAction> callback)
        {
            m_ui.RegisterCallback(m_internalId, action =>
            {
                callback(action);
                OpButtonSDK.AcknowledgeAction(action.Id);
            });
            return this;
        }

        /// <summary>
        /// Register a change handler where the caller must acknowledge manually.
        /// </summary>
        public OpButtonSelect OnChangeAcknowledge(OpButtonWidgetCallback callback)
        {
            m_ui.RegisterCallback(m_internalId, callback);
            return this;
        }
    }
}
