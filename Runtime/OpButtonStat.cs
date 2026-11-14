namespace OpButton.SDK
{
    /// <summary>
    /// Stat widget. Displays a numeric value with a label and unit.
    /// </summary>
    public sealed class OpButtonStat : OpButtonWidget<OpButtonStat>
    {
        internal OpButtonStat(OpButtonUI ui, string label) : base(ui, "stat")
        {
            Put("label", label);
        }

        public OpButtonStat Value(string value) { Put("value", value); return this; }
        public OpButtonStat Unit(string unit) { Put("unit", unit); return this; }

        public OpButtonStat ValueProvider(OpButtonValueProvider provider)
        {
            SetProvider("value", provider);
            return this;
        }
    }
}
