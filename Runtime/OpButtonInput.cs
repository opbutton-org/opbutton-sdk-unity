using System;

namespace OpButton.SDK
{
    /// <summary>
    /// Text input widget.
    /// </summary>
    public sealed class OpButtonInput : OpButtonWidget<OpButtonInput>
    {
        internal OpButtonInput(OpButtonUI ui, string label) : base(ui, "input")
        {
            Put("label", label);
        }

        public OpButtonInput InputType(string type) { Put("inputType", type); return this; }
        public OpButtonInput DefaultValue(string value) { Put("defaultValue", value); return this; }
        public OpButtonInput Placeholder(string placeholder) { Put("placeholder", placeholder); return this; }

        /// <summary>
        /// Register a typed submit handler that auto-acknowledges after the callback completes.
        /// </summary>
        public OpButtonInput OnSubmit(Action<OpButtonInputAction> callback)
        {
            m_ui.RegisterCallback(m_internalId, action =>
            {
                callback(new OpButtonInputAction(action));
                OpButtonSDK.AcknowledgeAction(action.Id);
            });
            return this;
        }

        /// <summary>
        /// Register a submit handler that auto-acknowledges after the callback completes.
        /// </summary>
        public OpButtonInput OnSubmit(Action<OpButtonAction> callback)
        {
            m_ui.RegisterCallback(m_internalId, action =>
            {
                callback(action);
                OpButtonSDK.AcknowledgeAction(action.Id);
            });
            return this;
        }

        /// <summary>
        /// Register a submit handler where the caller must acknowledge manually.
        /// </summary>
        public OpButtonInput OnSubmitAcknowledge(OpButtonWidgetCallback callback)
        {
            m_ui.RegisterCallback(m_internalId, callback);
            return this;
        }
    }
}
