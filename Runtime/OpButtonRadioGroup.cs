using System.Collections.Generic;

namespace OpButton.SDK
{
    /// <summary>
    /// Radio group with a single selected string value.
    /// </summary>
    public sealed class OpButtonRadioGroup : OpButtonWidget<OpButtonRadioGroup>
    {
        internal OpButtonRadioGroup(OpButtonUI ui, string label) : base(ui, "radio")
        {
            Put("label", label);
        }

        public OpButtonRadioGroup Options(string[][] options)
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

        public OpButtonRadioGroup DefaultValue(string value) { Put("defaultValue", value); return this; }

        public OpButtonRadioGroup Disabled(bool value) { Put("disabled", value); return this; }
        public OpButtonRadioGroup OptionDirection(string value)
        {
            if (value != "horizontal" && value != "vertical") throw new System.ArgumentException("Invalid option direction", nameof(value));
            Put("optionDirection", value); return this;
        }

        /// <summary>
        /// Register a typed change handler that auto-acknowledges after the callback completes.
        /// </summary>
        public OpButtonRadioGroup OnChange(System.Action<OpButtonRadioGroupAction> callback)
        {
            m_ui.RegisterCallback(m_internalId, action =>
            {
                callback(new OpButtonRadioGroupAction(action));
                OpButtonSDK.AcknowledgeAction(action.Id);
            });
            return this;
        }

        /// <summary>
        /// Register a change handler that auto-acknowledges after the callback completes.
        /// </summary>
        public OpButtonRadioGroup OnChange(System.Action<OpButtonAction> callback)
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
        public OpButtonRadioGroup OnChangeAcknowledge(OpButtonWidgetCallback callback)
        {
            m_ui.RegisterCallback(m_internalId, callback);
            return this;
        }
    }
}
