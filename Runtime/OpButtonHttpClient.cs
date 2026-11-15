using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;

namespace OpButton.SDK
{
    /// <summary>
    /// Low-level HTTP client for the OpButton server API.
    /// Thread-safe.
    /// </summary>
    internal sealed class OpButtonHttpClient : OpButtonPollDelivery.ITransport
    {
        private readonly string m_baseUrl;
        private readonly string m_apiToken;
        private readonly HttpClient m_http;

        internal OpButtonHttpClient(string baseUrl, string apiToken, HttpMessageHandler handler = null)
        {
            m_baseUrl = baseUrl;
            m_apiToken = apiToken;
            m_http = new HttpClient(handler ?? new HttpClientHandler())
            {
                Timeout = TimeSpan.FromSeconds(15)
            };
        }

        internal void Shutdown()
        {
            m_http.Dispose();
        }

        // ── Client endpoints ──

        internal OpButtonConnectResponse Connect(string deviceId, string deviceName,
            Dictionary<string, object> sessionAttributes)
        {
            var body = new Dictionary<string, object>
            {
                ["device_id"] = deviceId,
                ["device_name"] = deviceName
            };

            if (sessionAttributes != null && sessionAttributes.Count > 0)
                body["session_attributes"] = sessionAttributes;

            var request = ApiTokenRequest("/api/client/connect", HttpMethod.Post, body);
            return OpButtonConnectResponse.FromDict(Execute(request));
        }

        internal OpButtonConnectResponse PollStatus(string requestId)
        {
            var request = ApiTokenRequest("/api/client/poll-status/" + requestId, HttpMethod.Get);
            return OpButtonConnectResponse.FromDict(Execute(request));
        }

        internal bool CloseSession(string sessionToken)
        {
            var request = SessionTokenRequest("/api/client/session/close", sessionToken, HttpMethod.Post);
            return Execute(request).GetBool("ok", false);
        }

        public SimpleJson SubmitPanel(string sessionToken, Dictionary<string, object> layout)
        {
            var body = new Dictionary<string, object> { ["layout"] = layout };
            var request = SessionTokenRequest("/api/client/session/panel", sessionToken, HttpMethod.Post, body);
            return Execute(request);
        }

        public SimpleJson PollActions(string sessionToken,
            Dictionary<string, object> changedAttributes,
            List<OpButtonLogEntry> logs,
            List<OpButtonMetricEntry> metrics)
        {
            var body = new Dictionary<string, object>();

            if (changedAttributes != null && changedAttributes.Count > 0)
                body["session_attributes"] = changedAttributes;

            if (logs != null && logs.Count > 0)
            {
                var logsArray = new List<object>();
                foreach (var entry in logs)
                    logsArray.Add(entry.ToDict());
                body["logs"] = logsArray;
            }

            if (metrics != null && metrics.Count > 0)
            {
                var metricsArray = new List<object>();
                foreach (var entry in metrics)
                    metricsArray.Add(entry.ToDict());
                body["metrics"] = metricsArray;
            }

            var request = SessionTokenRequest("/api/client/session/actions/poll", sessionToken,
                HttpMethod.Post, body.Count > 0 ? body : null);
            return Execute(request);
        }

        internal SimpleJson GetLivePublisherState(string sessionToken)
        {
            var request = SessionTokenRequest("/api/client/session/live/publisher", sessionToken, HttpMethod.Get);
            return Execute(request);
        }

        internal void AcknowledgeAction(string sessionToken, string actionId, Dictionary<string, object> ackPayload)
        {
            var body = new Dictionary<string, object>
            {
                ["action_id"] = actionId,
                ["ack_payload"] = ackPayload ?? new Dictionary<string, object>()
            };

            var request = SessionTokenRequest("/api/client/session/actions/ack", sessionToken, HttpMethod.Post, body);
            Execute(request);
        }

        internal void SendLogs(string sessionToken, List<OpButtonLogEntry> logs)
        {
            if (logs == null || logs.Count == 0) return;

            var logsArray = new List<object>();
            foreach (var entry in logs)
                logsArray.Add(entry.ToDict());

            var body = new Dictionary<string, object> { ["logs"] = logsArray };
            var request = SessionTokenRequest("/api/client/session/logs", sessionToken, HttpMethod.Post, body);
            Execute(request);
        }

        internal void SendMetrics(string sessionToken, List<OpButtonMetricEntry> metrics)
        {
            if (metrics == null || metrics.Count == 0) return;

            var metricsArray = new List<object>();
            foreach (var entry in metrics)
                metricsArray.Add(entry.ToDict());

            var body = new Dictionary<string, object> { ["metrics"] = metricsArray };
            var request = SessionTokenRequest("/api/client/session/metrics", sessionToken, HttpMethod.Post, body);
            Execute(request);
        }

        // ── Helpers ──

        private HttpRequestMessage ApiTokenRequest(string path, HttpMethod method,
            Dictionary<string, object> body = null)
        {
            var request = new HttpRequestMessage(method, m_baseUrl + path);
            request.Headers.Add("X-Api-Token", m_apiToken);

            if (body != null)
                request.Content = new StringContent(SimpleJson.Serialize(body), Encoding.UTF8, "application/json");
            else if (method == HttpMethod.Post)
                request.Content = new StringContent("", Encoding.UTF8, "application/json");

            return request;
        }

        private HttpRequestMessage SessionTokenRequest(string path, string sessionToken,
            HttpMethod method, Dictionary<string, object> body = null)
        {
            var request = new HttpRequestMessage(method, m_baseUrl + path);
            request.Headers.Add("X-Session-Token", sessionToken);

            if (body != null)
                request.Content = new StringContent(SimpleJson.Serialize(body), Encoding.UTF8, "application/json");
            else if (method == HttpMethod.Post)
                request.Content = new StringContent("", Encoding.UTF8, "application/json");

            return request;
        }

        private SimpleJson Execute(HttpRequestMessage request)
        {
            try
            {
                var response = m_http.SendAsync(request, CancellationToken.None).GetAwaiter().GetResult();
                string responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                if (!response.IsSuccessStatusCode)
                {
                    string detail = responseBody;
                    try
                    {
                        var errorJson = SimpleJson.Parse(responseBody);
                        string parsedDetail = errorJson.GetString("detail");
                        if (parsedDetail != null) detail = parsedDetail;
                    }
                    catch { }

                    throw new OpButtonException((int)response.StatusCode,
                        "HTTP " + (int)response.StatusCode + ": " + detail);
                }

                if (string.IsNullOrEmpty(responseBody))
                    return new SimpleJson();

                return SimpleJson.Parse(responseBody);
            }
            catch (OpButtonException)
            {
                throw;
            }
            catch (Exception e)
            {
                throw new OpButtonException("Network error: " + e.Message, e);
            }
        }
    }
}
