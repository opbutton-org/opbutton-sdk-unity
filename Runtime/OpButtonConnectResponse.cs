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

        public OpButtonConnectResponse(string requestId, string status, string sessionToken)
        {
            RequestId = requestId;
            Status = status;
            SessionToken = sessionToken;
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
                token
            );
        }
    }
}
