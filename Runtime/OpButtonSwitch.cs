using System;

namespace OpButton.SDK
{
    /// <summary>
    /// Switch (toggle) widget.
    /// </summary>
    public sealed class OpButtonSwitch : OpButtonWidget<OpButtonSwitch>
    {
        internal OpButtonSwitch(OpButtonUI ui, string label) : base(ui, "switch")
        {
            Put("label", label);
        }

        public OpButtonSwitch DefaultValue(bool value) { Put("defaultValue", value); return this; }

        /// <summary>
        /// Register a typed change handler that auto-acknowledges after the callback completes.
        /// </summary>
        public OpButtonSwitch OnChange(Action<OpButtonSwitchAction> callback)
        {
            m_ui.RegisterCallback(m_internalId, action =>
            {
                callback(new OpButtonSwitchAction(action));
                OpButtonSDK.AcknowledgeAction(action.Id);
            });
            return this;
        }

        /// <summary>
        /// Register a change handler that auto-acknowledges after the callback completes.
        /// </summary>
        public OpButtonSwitch OnChange(Action<OpButtonAction> callback)
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
        public OpButtonSwitch OnChangeAcknowledge(OpButtonWidgetCallback callback)
        {
            m_ui.RegisterCallback(m_internalId, callback);
            return this;
        }
    }
}
