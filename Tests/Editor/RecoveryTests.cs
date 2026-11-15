using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace OpButton.SDK
{
    public sealed class RecoveryTests
    {
        [UnitySetUp]
        public IEnumerator EnterRuntime() { yield return new EnterPlayMode(); }

        [UnityTearDown]
        public IEnumerator LeaveRuntime()
        {
            OpButtonSDK.Shutdown();
            if (OpButtonRunner.Instance != null) UnityEngine.Object.DestroyImmediate(OpButtonRunner.Instance.gameObject);
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator RecoveryDeliversCommandsRetriesAttributesAndKeepsSampling()
        {
            var peer = new Peer();
            var collector = new Collector();
            var config = new OpButtonConfig.Builder("https://opbutton.invalid", "test")
                .SetAutoConnect(false).SetDeviceId("test-device").SetDeviceName("Test").SetActionPollIntervalMs(100)
                .SetSessionListener(new Listener(peer))
                .SetSessionAttributes(new OpButtonSessionAttributeBuilder().PutProvider("level", () => Volatile.Read(ref peer.Level)))
                .SetMetricConfig(new OpButtonMetricConfigBuilder().SetSampleIntervalMs(100).AddCollector(collector)).Build();
            OpButtonSDK.Initialize(config, new OpButtonHttpClient(config.BaseUrl, config.ApiToken, peer));
            bool command = false;
            int mainThread = Thread.CurrentThread.ManagedThreadId;
            var button = OpButtonSDK.GetUI().AddTab("Controls").Vertical().AddButton("Run").OnClick(() =>
            {
                Assert.AreEqual(mainThread, Thread.CurrentThread.ManagedThreadId);
                command = true;
            });
            peer.WidgetId = button.InternalId;
            OpButtonSDK.Connect();
            float deadline = Time.realtimeSinceStartup + 8;
            while ((!command || Volatile.Read(ref peer.AttributesAccepted) == 0) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(command, "Recovery poll command was discarded");
            Assert.AreEqual(1, Volatile.Read(ref peer.AttributesAccepted), "Failed attribute delta was not retried");
            int before = Volatile.Read(ref collector.Samples);
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.Greater(Volatile.Read(ref collector.Samples), before, "Sampling stopped during recovery");
        }

        private sealed class Collector : IOpButtonMetricCollector
        {
            internal int Samples;
            public void Collect(List<OpButtonMetricEntry> output) { Interlocked.Increment(ref Samples); }
        }

        private sealed class Listener : IOpButtonSessionListener
        {
            private readonly Peer m_peer;
            internal Listener(Peer peer) { m_peer = peer; }
            public void OnOpButtonSessionStarted(string token) { Volatile.Write(ref m_peer.Level, 2); }
            public void OnOpButtonSessionConnecting() { }
            public void OnOpButtonSessionWaitingApproval(string requestId) { }
            public void OnOpButtonSessionClosed() { }
            public void OnOpButtonSessionRejected() { }
            public void OnOpButtonSessionAuthFailed() { }
            public void OnOpButtonSessionError(OpButtonException error) { }
        }

        private sealed class Peer : HttpMessageHandler
        {
            internal int WidgetId;
            internal int Level = 1;
            internal int AttributesAccepted;
            private int m_polls;

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellation)
            {
                var body = new Dictionary<string, object>();
                var status = HttpStatusCode.OK;
                if (request.RequestUri.AbsolutePath == "/api/client/connect")
                {
                    body["request_id"] = "request";
                    body["status"] = "approved";
                    body["session_token"] = "test-session";
                }
                else if (request.RequestUri.AbsolutePath == "/api/client/session/actions/poll")
                {
                    int poll = Interlocked.Increment(ref m_polls);
                    body["actions"] = new List<object>();
                    if (poll == 1)
                    {
                        Thread.Sleep(100);
                        status = HttpStatusCode.ServiceUnavailable;
                    }
                    else if (poll == 2)
                    {
                        body["actions"] = new List<object> { new Dictionary<string, object>
                        {
                            ["id"] = "command", ["session_id"] = "session", ["widget_id"] = WidgetId,
                            ["action_type"] = "click", ["status"] = "delivered", ["payload"] = new Dictionary<string, object>()
                        }};
                    }
                    else
                    {
                        string content = request.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                        var attributes = string.IsNullOrEmpty(content) ? null : SimpleJson.Parse(content).GetObject("session_attributes");
                        if (attributes != null && attributes.GetInt("level") == 2) Interlocked.Exchange(ref AttributesAccepted, 1);
                    }
                }
                return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(SimpleJson.Serialize(body)) });
            }
        }
    }
}
