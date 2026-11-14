using System;
using System.Collections.Generic;
using System.Globalization;

namespace OpButton.SDK
{
    /// <summary>
    /// A single log entry to be sent to the OpButton server.
    /// </summary>
    public sealed class OpButtonLogEntry
    {
        public string Level { get; }
        public string Message { get; }
        public string Category { get; }
        public string Thread { get; }
        public Dictionary<string, object> Metadata { get; }
        public Dictionary<string, object> Attributes { get; }
        public string ClientTimestamp { get; }

        private static string FormatTimestamp(long timestampMs)
        {
            var dt = DateTimeOffset.FromUnixTimeMilliseconds(timestampMs).UtcDateTime;
            return dt.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        }

        public OpButtonLogEntry(OpButtonLogLevel level, string message, Dictionary<string, object> metadata = null)
            : this(level.ToValue(), message, null, null, metadata, null, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
        {
        }

        public OpButtonLogEntry(OpButtonLogLevel level, string message, string category, string thread,
            Dictionary<string, object> metadata = null, Dictionary<string, object> attributes = null)
            : this(level.ToValue(), message, category, thread, metadata, attributes, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
        {
        }

        public OpButtonLogEntry(OpButtonLogLevel level, string message, string category, string thread,
            Dictionary<string, object> metadata, Dictionary<string, object> attributes, long clientTimestampMs)
            : this(level.ToValue(), message, category, thread, metadata, attributes, clientTimestampMs)
        {
        }

        public OpButtonLogEntry(string level, string message, string category, string thread,
            Dictionary<string, object> metadata, Dictionary<string, object> attributes, long clientTimestampMs)
        {
            Level = level;
            Message = message;
            Category = category;
            Thread = thread;
            Metadata = metadata;
            Attributes = attributes;
            ClientTimestamp = FormatTimestamp(clientTimestampMs);
        }

        public static OpButtonLogEntry Debug(string message) => new OpButtonLogEntry(OpButtonLogLevel.Debug, message);
        public static OpButtonLogEntry Info(string message) => new OpButtonLogEntry(OpButtonLogLevel.Info, message);
        public static OpButtonLogEntry Warning(string message) => new OpButtonLogEntry(OpButtonLogLevel.Warning, message);
        public static OpButtonLogEntry Error(string message) => new OpButtonLogEntry(OpButtonLogLevel.Error, message);
        public static OpButtonLogEntry Critical(string message) => new OpButtonLogEntry(OpButtonLogLevel.Critical, message);

        internal Dictionary<string, object> ToDict()
        {
            var dict = new Dictionary<string, object>
            {
                ["level"] = Level,
                ["message"] = Message,
                ["client_timestamp"] = ClientTimestamp
            };

            if (Category != null)
                dict["category"] = Category;

            if (Thread != null)
                dict["thread"] = Thread;

            if (Metadata != null && Metadata.Count > 0)
                dict["metadata"] = Metadata;

            if (Attributes != null && Attributes.Count > 0)
                dict["attributes"] = Attributes;

            return dict;
        }
    }
}
