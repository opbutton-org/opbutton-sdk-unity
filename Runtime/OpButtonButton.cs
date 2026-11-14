using System;

namespace OpButton.SDK
{
    /// <summary>
    /// Button widget. Triggers a "click" action when pressed on the dashboard.
    /// </summary>
    public sealed class OpButtonButton : OpButtonWidget<OpButtonButton>
    {
        internal OpButtonButton(OpButtonUI ui, string label) : base(ui, "button")
        {
            Put("label", label);
        }

        public OpButtonButton Variant(string variant) { Put("variant", variant); return this; }
        public OpButtonButton Color(string color) { Put("color", color); return this; }
        public OpButtonButton Disabled(bool disabled) { Put("disabled", disabled); return this; }

        /// <summary>
        /// Register a click handler that auto-acknowledges the action after the callback completes.
        /// </summary>
        public OpButtonButton OnClick(Action callback)
        {
            m_ui.RegisterCallback(m_internalId, action =>
            {
                callback();
                OpButtonSDK.AcknowledgeAction(action.Id);
            });
            return this;
        }

        /// <summary>
        /// Register a click handler that receives the full OpButtonAction.
        /// The caller is responsible for calling OpButtonSDK.AcknowledgeAction when ready.
        /// </summary>
        public OpButtonButton OnClickAcknowledge(OpButtonWidgetCallback callback)
        {
            m_ui.RegisterCallback(m_internalId, callback);
            return this;
        }
    }
}
