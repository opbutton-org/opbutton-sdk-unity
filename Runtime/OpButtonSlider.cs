
namespace OpButton.SDK
{
    /// <summary>
    /// Numeric slider. Sends a change action when an edit is committed.
    /// </summary>
    public sealed class OpButtonSlider : OpButtonWidget<OpButtonSlider>
    {
        internal OpButtonSlider(OpButtonUI ui, string label) : base(ui, "slider")
        {
            Put("label", label);
        }

        public OpButtonSlider Range(double min, double max, double step)
        {
            if (double.IsNaN(min) || double.IsInfinity(min) || double.IsNaN(max) || double.IsInfinity(max) || double.IsNaN(step) || double.IsInfinity(step) || double.IsInfinity(max - min) || min >= max || step <= 0 || step > max - min)
                throw new System.ArgumentException("Invalid slider range");
            Put("min", min); Put("max", max); Put("step", step); return this;
        }
        public OpButtonSlider DefaultValue(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) throw new System.ArgumentOutOfRangeException(nameof(value));
            Put("defaultValue", value); return this;
        }
        public OpButtonSlider Unit(string value) { Put("unit", value); return this; }
        public OpButtonSlider Disabled(bool value) { Put("disabled", value); return this; }

        /// <summary>
        /// Register a typed change handler that auto-acknowledges after the callback completes.
        /// </summary>
        public OpButtonSlider OnChange(System.Action<OpButtonSliderAction> callback)
        {
            m_ui.RegisterCallback(m_internalId, action =>
            {
                callback(new OpButtonSliderAction(action));
                OpButtonSDK.AcknowledgeAction(action.Id);
            });
            return this;
        }

        /// <summary>
        /// Register a change handler that auto-acknowledges after the callback completes.
        /// </summary>
        public OpButtonSlider OnChange(System.Action<OpButtonAction> callback)
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
        public OpButtonSlider OnChangeAcknowledge(OpButtonWidgetCallback callback)
        {
            m_ui.RegisterCallback(m_internalId, callback);
            return this;
        }
    }
}
