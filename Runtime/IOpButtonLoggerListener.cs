namespace OpButton.SDK
{
    /// <summary>
    /// Listener for OpButton SDK internal diagnostic logs.
    /// </summary>
    public interface IOpButtonLoggerListener
    {
        void OnOpButtonLoggerMessage(OpButtonLogLevel level, string tag, string message, System.Exception exception);
    }
}
