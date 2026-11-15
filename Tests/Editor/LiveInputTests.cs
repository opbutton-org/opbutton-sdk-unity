using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace OpButton.SDK
{
    public sealed class PointerProbe : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        public int Downs, Ups, Clicks;
        public bool ConsumeClickOnRelease;
        public void OnPointerDown(PointerEventData data) { Downs++; }
        public void OnPointerUp(PointerEventData data)
        {
            Ups++;
            if (ConsumeClickOnRelease) data.eligibleForClick = false;
        }
        public void OnPointerClick(PointerEventData data) { Clicks++; }
    }

    public sealed class ProbeRaycaster : BaseRaycaster
    {
        public GameObject Target;
        public override Camera eventCamera => null;
        public override void Raycast(PointerEventData data, List<RaycastResult> results)
        {
            results.Add(new RaycastResult { gameObject = Target, module = this, distance = 0 });
        }
    }

    public sealed class LiveInputTests
    {
        private readonly List<GameObject> m_objects = new List<GameObject>();

        [UnitySetUp]
        public IEnumerator EnterRuntime() { yield return new EnterPlayMode(); }

        [UnityTearDown]
        public IEnumerator LeaveRuntime() { yield return new ExitPlayMode(); }

        private GameObject Object(string name)
        {
            var value = new GameObject(name);
            m_objects.Add(value);
            return value;
        }

        [TearDown]
        public void TearDown()
        {
            OpButtonSDK.Shutdown();
            foreach (var item in m_objects) UnityEngine.Object.DestroyImmediate(item);
            m_objects.Clear();
            foreach (var overlay in UnityEngine.Object.FindObjectsOfType<OpButtonLiveOverlayView>())
                UnityEngine.Object.DestroyImmediate(overlay.gameObject);
            if (OpButtonRunner.Instance != null) UnityEngine.Object.DestroyImmediate(OpButtonRunner.Instance.gameObject);
        }

        private OpButtonPointerGesture Press(out PointerProbe probe)
        {
            var events = Object("Events").AddComponent<EventSystem>();
            var parent = Object("Button");
            probe = parent.AddComponent<PointerProbe>();
            var child = Object("Text");
            child.transform.SetParent(parent.transform);
            var raycaster = Object("Raycaster").AddComponent<ProbeRaycaster>();
            raycaster.Target = child;
            return OpButtonPointerGesture.Begin(events, Vector2.zero);
        }

        [UnityTest]
        public IEnumerator APressStaysDownUntilReleaseAndFindsItsParentHandler()
        {
            var gesture = Press(out var probe);
            Assert.IsNotNull(gesture);
            Assert.AreEqual(1, probe.Downs);
            Assert.AreEqual(0, probe.Ups);
            Assert.AreEqual(0, probe.Clicks);
            yield return null;
            Assert.AreEqual(0, probe.Ups);
            gesture.Release();
            Assert.AreEqual(1, probe.Ups);
            Assert.AreEqual(1, probe.Clicks);
            gesture.Release();
            Assert.AreEqual(1, probe.Clicks);
        }

        [Test]
        public void CancellationReleasesWithoutClicking()
        {
            var gesture = Press(out var probe);
            gesture.Release(cancelled: true);
            Assert.AreEqual(1, probe.Ups);
            Assert.AreEqual(0, probe.Clicks);
            Assert.IsFalse(gesture.IsActive);
        }

        [Test]
        public void HostCanConsumeTheClickWhenReleasingAPress()
        {
            var gesture = Press(out var probe);
            probe.ConsumeClickOnRelease = true;
            gesture.Release();
            Assert.AreEqual(1, probe.Ups);
            Assert.AreEqual(0, probe.Clicks);
        }

        [Test]
        public void LiveStopOnTheMainThreadCompletesImmediately()
        {
            OpButtonRunner.EnsureExists();
            var publisher = new ProbePublisher();
            var manager = new OpButtonLiveManager(new OpButtonHttpClient("https://opbutton.invalid", "test"), publisher);
            var started = DateTime.UtcNow;
            manager.Stop();
            Assert.IsTrue(publisher.Stopped);
            Assert.Less((DateTime.UtcNow - started).TotalSeconds, 0.5);
        }

        [Test]
        public void ExpiredMainThreadRequestsDoNotRunAfterTheHostResumes()
        {
            OpButtonRunner.EnsureExists();
            int calls = 0;
            var worker = Task.Run(() => Assert.Throws<OpButtonException>(() =>
                OpButtonRunner.InvokeOnMainThread(() => ++calls, timeoutMs: 25)));
            Assert.IsTrue(worker.Wait(2000));
            // The main thread was deliberately blocked above. Pump its queue now.
            OpButtonRunner.Instance.SendMessage("Update");
            Assert.AreEqual(0, calls);
        }

        [Test]
        public void EndingASessionReleasesWaitingMainThreadRequests()
        {
            OpButtonRunner.EnsureExists();
            using (var cancellation = new CancellationTokenSource())
            {
                var worker = Task.Run(() => Assert.Throws<OperationCanceledException>(() =>
                    OpButtonRunner.InvokeOnMainThread(() => 42, cancellation: cancellation.Token)));
                cancellation.Cancel();
                Assert.IsTrue(worker.Wait(2000));
                OpButtonRunner.Instance.SendMessage("Update");
            }
        }

        [UnityTest]
        public IEnumerator LongPressDelaysReleaseAndStoppingLiveCancelsWithoutClick()
        {
            // Register a raycast target whose parent owns the pointer callbacks.
            var initial = Press(out var probe);
            initial.Release(cancelled: true);
            probe.Downs = probe.Ups = probe.Clicks = 0;
            OpButtonRunner.EnsureExists();
            var http = new OpButtonHttpClient("https://opbutton.invalid", "test", new PublisherHandler());
            var manager = new OpButtonLiveManager(http, new ProbePublisher());
            Assert.AreEqual(true, manager.Start("session", null)["ok"]);
            Assert.AreEqual(true, manager.LongPress(new Dictionary<string, object> { ["duration_ms"] = 250 })["ok"]);
            Assert.AreEqual(1, probe.Downs);
            Assert.AreEqual(0, probe.Ups);
            yield return new WaitForSecondsRealtime(0.1f);
            Assert.AreEqual(0, probe.Ups);
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.AreEqual(1, probe.Ups);
            Assert.AreEqual(1, probe.Clicks);

            manager.LongPress(new Dictionary<string, object> { ["duration_ms"] = 250 });
            manager.Stop();
            Assert.AreEqual(2, probe.Ups);
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.AreEqual(1, probe.Clicks);
            http.Shutdown();
        }

        private sealed class PublisherHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellation)
            {
                var body = new Dictionary<string, object>
                {
                    ["configured"] = true, ["requested"] = true, ["server_url"] = "wss://opbutton.invalid",
                    ["participant_token"] = "test", ["video_track_name"] = "screen"
                };
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(SimpleJson.Serialize(body))
                });
            }
        }

        private sealed class ProbePublisher : IOpButtonLivePublisher
        {
            internal bool Stopped;
            public void Start(string url, string token, string preset, int size, int fps, Action<Exception> done) { done(null); }
            public void EnableScreenShare(Action<Exception> done) { done(null); }
            public void UpdateQuality(string preset, int size, int fps, Action<bool, Exception> done) { done(true, null); }
            public void Stop() { Stopped = true; }
        }
    }
}
