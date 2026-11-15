using System;

namespace OpButton.SDK
{
    /// <summary>Media adapter boundary; the core has no dependency on a media SDK.</summary>
    internal interface IOpButtonLivePublisher
    {
        void Start(string serverUrl, string participantToken, string presetName,
            int maxDimension, int framesPerSecond, Action<Exception> onComplete);
        void EnableScreenShare(Action<Exception> onComplete);
        void UpdateQuality(string presetName, int maxDimension, int framesPerSecond,
            Action<bool, Exception> onComplete);
        void Stop();
    }

    internal static class OpButtonLivePublisherFactory
    {
        private static Func<IOpButtonLivePublisher> s_factory = () => new UnavailablePublisher();

        internal static void Register(Func<IOpButtonLivePublisher> factory) { s_factory = factory; }
        internal static IOpButtonLivePublisher Create() { return s_factory(); }

        private sealed class UnavailablePublisher : IOpButtonLivePublisher
        {
            private static Exception Missing() => new OpButtonException(
                "LiveKit is not installed. Follow the optional LiveKit setup in docs/live-streaming.md.");
            public void Start(string url, string token, string preset, int size, int fps, Action<Exception> done) { done(Missing()); }
            public void EnableScreenShare(Action<Exception> done) { done(Missing()); }
            public void UpdateQuality(string preset, int size, int fps, Action<bool, Exception> done) { done(false, Missing()); }
            public void Stop() { }
        }
    }
}
