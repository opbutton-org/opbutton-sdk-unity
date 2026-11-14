namespace OpButton.SDK
{
    /// <summary>
    /// Listener for session lifecycle events.
    /// All methods are called on the main thread.
    /// </summary>
    public interface IOpButtonSessionListener
    {
        void OnOpButtonSessionConnecting() { }
        void OnOpButtonSessionWaitingApproval(string requestId) { }
        void OnOpButtonSessionStarted(string sessionToken) { }
        void OnOpButtonSessionClosed() { }
        void OnOpButtonSessionRejected() { }
        void OnOpButtonSessionAuthFailed() { }
        void OnOpButtonSessionError(OpButtonException exception) { }
    }
}
