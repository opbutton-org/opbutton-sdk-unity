namespace OpButton.SDK
{
    /// <summary>
    /// Listener for actions dispatched from the OpButton dashboard.
    /// </summary>
    public interface IOpButtonActionListener
    {
        void OnOpButtonActionReceived(OpButtonAction action);
    }
}
