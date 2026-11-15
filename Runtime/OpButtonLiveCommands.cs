using System;
using System.Collections.Generic;

namespace OpButton.SDK
{
    /// <summary>Composes media commands with a captured session; no command calls the global facade.</summary>
    internal sealed class OpButtonLiveCommands
    {
        private readonly OpButtonLiveManager m_live;
        private readonly Action<Action> m_background;
        private readonly Func<string, bool> m_isCurrent;
        private readonly Action<string, string, Dictionary<string, object>> m_acknowledge;

        internal OpButtonLiveCommands(OpButtonLiveManager live, Action<Action> background,
            Func<string, bool> isCurrent, Action<string, string, Dictionary<string, object>> acknowledge)
        {
            m_live = live;
            m_background = background;
            m_isCurrent = isCurrent;
            m_acknowledge = acknowledge;
        }

        internal Dictionary<OpButtonActionType, OpButtonActionRouter.Command> Commands()
        {
            return new Dictionary<OpButtonActionType, OpButtonActionRouter.Command>
            {
                [OpButtonActionType.LiveStart] = (action, token) => Stream(action, token, m_live.Start),
                [OpButtonActionType.LiveUpdate] = (action, token) => Stream(action, token, m_live.Update),
                [OpButtonActionType.LiveStop] = (action, token) => m_acknowledge(token, action.Id, m_live.Stop()),
                [OpButtonActionType.LiveTap] = (action, token) => m_acknowledge(token, action.Id, m_live.Tap(action.Payload?.ToDictionary())),
                [OpButtonActionType.LiveLongPress] = (action, token) => m_acknowledge(token, action.Id, m_live.LongPress(action.Payload?.ToDictionary()))
            };
        }

        private void Stream(OpButtonAction action, string token,
            Func<string, Dictionary<string, object>, Dictionary<string, object>> operation)
        {
            m_background(() =>
            {
                if (!m_isCurrent(token)) return;
                var result = operation(token, action.Payload?.ToDictionary());
                if (m_isCurrent(token)) m_acknowledge(token, action.Id, result);
            });
        }
    }
}
