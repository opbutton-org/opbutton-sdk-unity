#if OPBUTTON_LIVEKIT
using UnityEngine;

namespace OpButton.SDK
{
    internal static class OpButtonLiveKitRegistration
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void Register()
        {
            OpButtonLivePublisherFactory.Register(() => new OpButtonLiveKitPublisher());
        }
    }
}
#endif
