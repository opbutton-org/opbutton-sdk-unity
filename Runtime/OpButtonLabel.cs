namespace OpButton.SDK
{
    /// <summary>
    /// Label widget. Displays text with an optional color indicator.
    /// </summary>
    public sealed class OpButtonLabel : OpButtonWidget<OpButtonLabel>
    {
        internal OpButtonLabel(OpButtonUI ui, string text) : base(ui, "label")
        {
            Put("text", text);
        }

        public OpButtonLabel Text(string text) { Put("text", text); return this; }
        public OpButtonLabel Color(string color) { Put("color", color); return this; }

        public OpButtonLabel TextProvider(OpButtonValueProvider provider)
        {
            SetProvider("text", provider);
            return this;
        }
    }
}
