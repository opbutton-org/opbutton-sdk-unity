namespace OpButton.SDK
{
    /// <summary>
    /// Response from connect / poll-status endpoints.
    /// </summary>
    internal sealed class OpButtonConnectResponse
    {
        public string RequestId { get; }
        public string Status { get; }
        public string SessionToken { get; }
        public string DeviceSecret { get; }

        public OpButtonConnectResponse(string requestId, string status, string sessionToken,
            string deviceSecret)
        {
            RequestId = requestId;
            Status = status;
            SessionToken = sessionToken;
            DeviceSecret = deviceSecret;
        }

        public bool IsPending => Status == "pending";
        public bool IsApproved => Status == "approved";
        public bool IsRejected => Status == "rejected";

        public static OpButtonConnectResponse FromDict(SimpleJson json)
        {
            string token = json.IsNull("session_token") ? null : json.GetString("session_token", null);
            return new OpButtonConnectResponse(
                json.GetString("request_id", ""),
                json.GetString("status", ""),
                token,
                json.IsNull("device_secret") ? null : json.GetString("device_secret", null)
            );
        }
    }
}
