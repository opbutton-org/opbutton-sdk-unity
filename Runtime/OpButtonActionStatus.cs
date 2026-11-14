namespace OpButton.SDK
{
    public enum OpButtonActionStatus
    {
        Pending,
        Delivered,
        Acknowledged,
        Unknown
    }

    internal static class OpButtonActionStatusExtensions
    {
        public static string ToValue(this OpButtonActionStatus status)
        {
            switch (status)
            {
                case OpButtonActionStatus.Pending: return "pending";
                case OpButtonActionStatus.Delivered: return "delivered";
                case OpButtonActionStatus.Acknowledged: return "acknowledged";
                default: return "";
            }
        }

        public static OpButtonActionStatus FromValue(string value)
        {
            switch (value)
            {
                case "pending": return OpButtonActionStatus.Pending;
                case "delivered": return OpButtonActionStatus.Delivered;
                case "acknowledged": return OpButtonActionStatus.Acknowledged;
                default: return OpButtonActionStatus.Unknown;
            }
        }
    }
}
