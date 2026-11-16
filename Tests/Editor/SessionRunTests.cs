using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace OpButton.SDK
{
    public sealed class SessionRunTests
    {
        private sealed class Peer : HttpMessageHandler
        {
            internal readonly List<Uri> Requests = new List<Uri>();
            internal readonly List<string> Runs = new List<string>();
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Requests.Add(request.RequestUri);
                if (request.Content != null)
                {
                    string body = await request.Content.ReadAsStringAsync().ConfigureAwait(false);
                    Runs.Add(SimpleJson.Parse(body).GetString("run_id", ""));
                }
                if (Requests.Count == 1) throw new HttpRequestException("Connection dropped after request");
                Assert.AreEqual("test", string.Join("", request.Headers.GetValues("X-Api-Token")));
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"request_id\":\"request\",\"status\":\"pending\"}")
                };
            }
        }

        [Test]
        public void RetriesAndApprovalPollKeepIdentityButNextSdkGetsAnother()
        {
            var peer = new Peer();
            var first = new OpButtonHttpClient("https://run.invalid", "test", peer);
            var next = new OpButtonHttpClient("https://run.invalid", "test", peer);
            try
            {
                Assert.Throws<OpButtonException>(() => first.Connect("phone", "Phone", null));
                first.Connect("phone", "Phone", null);
                first.PollStatus("request");
                next.Connect("phone", "Phone", null);
                Assert.AreEqual(peer.Runs[0], peer.Runs[1]);
                Assert.AreEqual("?run_id=" + peer.Runs[0], peer.Requests[2].Query);
                Assert.AreNotEqual(peer.Runs[0], peer.Runs[2]);
                foreach (string run in peer.Runs) Assert.IsTrue(Guid.TryParse(run, out _));
            }
            finally { first.Shutdown(); next.Shutdown(); }
        }
    }
}
