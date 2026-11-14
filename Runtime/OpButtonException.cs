using System;

namespace OpButton.SDK
{
    /// <summary>
    /// OpButton SDK exception.
    /// </summary>
    public class OpButtonException : Exception
    {
        public int HttpCode { get; }

        public OpButtonException(string message) : base(message)
        {
            HttpCode = 0;
        }

        public OpButtonException(string message, Exception innerException) : base(message, innerException)
        {
            HttpCode = 0;
        }

        public OpButtonException(int httpCode, string message) : base(message)
        {
            HttpCode = httpCode;
        }

        public bool IsNetworkError => HttpCode == 0 && InnerException != null;
        public bool IsSessionGone => HttpCode == 410;
        public bool IsUnauthorized => HttpCode == 401;
    }
}
