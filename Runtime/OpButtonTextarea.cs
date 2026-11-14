using System;

namespace OpButton.SDK
{
    /// <summary>
    /// Multi-line text input widget.
    /// </summary>
    public sealed class OpButtonTextarea : OpButtonWidget<OpButtonTextarea>
    {
        internal OpButtonTextarea(OpButtonUI ui, string label) : base(ui, "textarea")
        {
            Put("label", label);
        }

        public OpButtonTextarea Rows(int rows) { Put("rows", rows); return this; }
        public OpButtonTextarea DefaultValue(string value) { Put("defaultValue", value); return this; }

        /// <summary>
        /// Register a typed submit handler that auto-acknowledges after the callback completes.
        /// </summary>
        public OpButtonTextarea OnSubmit(Action<OpButtonTextareaAction> callback)
        {
            m_ui.RegisterCallback(m_internalId, action =>
            {
                callback(new OpButtonTextareaAction(action));
                OpButtonSDK.AcknowledgeAction(action.Id);
            });
            return this;
        }

        /// <summary>
        /// Register a submit handler that auto-acknowledges after the callback completes.
        /// </summary>
        public OpButtonTextarea OnSubmit(Action<OpButtonAction> callback)
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
        public OpButtonTextarea OnSubmitAcknowledge(OpButtonWidgetCallback callback)
        {
            m_ui.RegisterCallback(m_internalId, callback);
            return this;
        }
    }
}
