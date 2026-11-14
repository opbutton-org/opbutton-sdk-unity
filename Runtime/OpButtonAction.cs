namespace OpButton.SDK
{
    /// <summary>
    /// An action dispatched from the OpButton dashboard (button click, switch toggle, etc.).
    /// </summary>
    public sealed class OpButtonAction
    {
        public string Id { get; }
        public string SessionId { get; }
        public int WidgetId { get; }
        public OpButtonActionType ActionType { get; }
        public OpButtonActionStatus Status { get; }
        public SimpleJson Payload { get; }

        internal OpButtonAction(string id, string sessionId, int widgetId,
            OpButtonActionType actionType, OpButtonActionStatus status, SimpleJson payload)
        {
            Id = id;
            SessionId = sessionId;
            WidgetId = widgetId;
            ActionType = actionType;
            Status = status;
            Payload = payload;
        }

        internal static OpButtonAction FromDict(SimpleJson json)
        {
            return new OpButtonAction(
                json.GetString("id", ""),
                json.GetString("session_id", ""),
                json.GetInt("widget_id", 0),
                OpButtonActionTypeExtensions.FromValue(json.GetString("action_type", "")),
                OpButtonActionStatusExtensions.FromValue(json.GetString("status", "")),
                json.GetObject("payload")
            );
        }
    }
}
