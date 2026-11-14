namespace OpButton.SDK
{
    public enum OpButtonActionType
    {
        Click,
        Change,
        Toggle,
        LiveStart,
        LiveUpdate,
        LiveStop,
        LiveTap,
        LiveLongPress,
        Unknown
    }

    internal static class OpButtonActionTypeExtensions
    {
        public static string ToValue(this OpButtonActionType type)
        {
            switch (type)
            {
                case OpButtonActionType.Click: return "click";
                case OpButtonActionType.Change: return "change";
                case OpButtonActionType.Toggle: return "toggle";
                case OpButtonActionType.LiveStart: return "live_start";
                case OpButtonActionType.LiveUpdate: return "live_update";
                case OpButtonActionType.LiveStop: return "live_stop";
                case OpButtonActionType.LiveTap: return "live_tap";
                case OpButtonActionType.LiveLongPress: return "live_long_press";
                default: return "";
            }
        }

        public static OpButtonActionType FromValue(string value)
        {
            switch (value)
            {
                case "click": return OpButtonActionType.Click;
                case "change": return OpButtonActionType.Change;
                case "toggle": return OpButtonActionType.Toggle;
                case "live_start": return OpButtonActionType.LiveStart;
                case "live_update": return OpButtonActionType.LiveUpdate;
                case "live_stop": return OpButtonActionType.LiveStop;
                case "live_tap": return OpButtonActionType.LiveTap;
                case "live_long_press": return OpButtonActionType.LiveLongPress;
                default: return OpButtonActionType.Unknown;
            }
        }
    }
}
