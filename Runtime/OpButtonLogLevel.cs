namespace OpButton.SDK
{
    public enum OpButtonLogLevel
    {
        Debug,
        Info,
        Warning,
        Error,
        Critical,
        Exception
    }

    internal static class OpButtonLogLevelExtensions
    {
        public static string ToValue(this OpButtonLogLevel level)
        {
            switch (level)
            {
                case OpButtonLogLevel.Debug: return "debug";
                case OpButtonLogLevel.Info: return "info";
                case OpButtonLogLevel.Warning: return "warning";
                case OpButtonLogLevel.Error: return "error";
                case OpButtonLogLevel.Critical: return "critical";
                case OpButtonLogLevel.Exception: return "exception";
                default: return "info";
            }
        }
    }
}
