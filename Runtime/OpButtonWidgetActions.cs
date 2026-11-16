namespace OpButton.SDK
{
    /// <summary>
    /// Typed action for <see cref="OpButtonSwitch"/> change events.
    /// Provides a strongly-typed <see cref="Value"/> instead of raw payload access.
    /// </summary>
    public sealed class OpButtonSwitchAction
    {
        public OpButtonAction Action { get; }
        public bool Value { get; }

        internal OpButtonSwitchAction(OpButtonAction action)
        {
            Action = action;
            Value = action.Payload?.GetBool("value") ?? false;
        }
    }

    /// <summary>
    /// Typed action for <see cref="OpButtonInput"/> submit events.
    /// </summary>
    public sealed class OpButtonInputAction
    {
        public OpButtonAction Action { get; }
        public string Value { get; }

        internal OpButtonInputAction(OpButtonAction action)
        {
            Action = action;
            Value = action.Payload?.GetString("value", "") ?? "";
        }
    }

    /// <summary>
    /// Typed action for <see cref="OpButtonSelect"/> change events.
    /// </summary>
    public sealed class OpButtonSelectAction
    {
        public OpButtonAction Action { get; }
        public string Value { get; }

        internal OpButtonSelectAction(OpButtonAction action)
        {
            Action = action;
            Value = action.Payload?.GetString("value", "") ?? "";
        }
    }

    /// <summary>
    /// Typed action for <see cref="OpButtonTextarea"/> submit events.
    /// </summary>
    public sealed class OpButtonTextareaAction
    {
        public OpButtonAction Action { get; }
        public string Value { get; }

        internal OpButtonTextareaAction(OpButtonAction action)
        {
            Action = action;
            Value = action.Payload?.GetString("value", "") ?? "";
        }
    }

    public sealed class OpButtonRadioGroupAction
    {
        public OpButtonAction Action { get; }
        public string Value { get; }

        internal OpButtonRadioGroupAction(OpButtonAction action)
        {
            Action = action;
            Value = action.Payload?.GetString("value", "") ?? "";
        }
    }

    public sealed class OpButtonSliderAction
    {
        public OpButtonAction Action { get; }
        public double Value { get; }

        internal OpButtonSliderAction(OpButtonAction action)
        {
            Action = action;
            Value = action.Payload?.GetDouble("value") ?? 0;
        }
    }
}
