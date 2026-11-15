#if OPBUTTON_LIVEKIT
using System;
using System.Collections;
using LiveKit;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace OpButton.SDK
{
    public sealed class QualityUpdateTests
    {
        [UnityTest]
        public IEnumerator QualityUpdateWaitsForPublicationAndPropagatesLateFailure()
        {
            return CheckPublication(new InvalidOperationException("Late publish failure"));
        }

        [UnityTest]
        public IEnumerator QualityUpdateReportsActiveOnlyAfterPublicationCompletes()
        {
            return CheckPublication(null);
        }

        private static IEnumerator CheckPublication(Exception publicationError)
        {
            bool completed = false;
            IEnumerator Publish(Room room, OpButtonLiveKitPublisher.LiveQuality quality, Action<Exception> done)
            {
                yield return null;
                yield return null;
                Assert.IsFalse(completed, "Quality update completed before publication");
                done(publicationError);
            }
            var publisher = new OpButtonLiveKitPublisher(Publish);
            yield return publisher.DoUpdateQuality(null, new OpButtonLiveKitPublisher.LiveQuality("high", 1080, 4), (active, error) =>
            {
                Assert.AreEqual(publicationError == null, active);
                Assert.AreSame(publicationError, error);
                completed = true;
            });
            Assert.IsTrue(completed);
        }
    }
}
#endif
