using System;
using System.Collections.Generic;

namespace OpButton.SDK
{
    /// <summary>Routes reserved commands exclusively, then publishes application actions to observers.</summary>
    internal sealed class OpButtonActionRouter
    {
        internal delegate void Command(OpButtonAction action, string token);
        private readonly Dictionary<OpButtonActionType, Command> m_commands;
        private readonly Action<OpButtonAction> m_widgets;
        private readonly Func<List<IOpButtonActionListener>> m_listeners;

        internal OpButtonActionRouter(Dictionary<OpButtonActionType, Command> commands,
            Action<OpButtonAction> widgets, Func<List<IOpButtonActionListener>> listeners)
        {
            m_commands = new Dictionary<OpButtonActionType, Command>(commands);
            m_widgets = widgets;
            m_listeners = listeners;
        }

        internal void Dispatch(OpButtonAction action, string token)
        {
            if (m_commands.TryGetValue(action.ActionType, out var command))
            {
                try { command(action, token); }
                catch (Exception error) { OpButtonLog.Error("Internal command failed", error); }
                return;
            }
            try { m_widgets(action); }
            catch (Exception error) { OpButtonLog.Error("Widget callback threw exception", error); }
            foreach (var listener in m_listeners())
            {
                try { listener.OnOpButtonActionReceived(action); }
                catch (Exception error) { OpButtonLog.Error("Action listener threw exception", error); }
            }
        }
    }
}
