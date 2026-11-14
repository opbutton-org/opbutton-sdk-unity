namespace OpButton.SDK
{
    /// <summary>
    /// Logs display widget.
    /// </summary>
    public sealed class OpButtonLogs : OpButtonWidget<OpButtonLogs>
    {
        internal OpButtonLogs(OpButtonUI ui, string label) : base(ui, "logs")
        {
            Put("label", label);
        }

        public OpButtonLogs MaxLines(int maxLines) { Put("maxLines", maxLines); return this; }
    }
}
