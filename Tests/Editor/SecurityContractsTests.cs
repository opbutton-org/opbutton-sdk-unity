using System;
using NUnit.Framework;

namespace OpButton.SDK
{
    public sealed class SecurityContractsTests
    {
        [Test]
        public void ProductionBaseUrlRequiresHttpsAndHasNoAmbiguousSuffix()
        {
            Assert.Throws<ArgumentException>(() =>
                new OpButtonConfig.Builder("http://example.com", "token").Build());
            Assert.Throws<ArgumentException>(() =>
                new OpButtonConfig.Builder("https://user@example.com", "token").Build());
            Assert.Throws<ArgumentException>(() =>
                new OpButtonConfig.Builder("https://example.com?next=evil", "token").Build());
            Assert.Throws<ArgumentException>(() =>
                new OpButtonConfig.Builder("https://example.com#fragment", "token").Build());

            var local = new OpButtonConfig.Builder("http://127.0.0.1:8000", "token")
                .SetAllowInsecureLocalhost(true)
                .Build();
            Assert.AreEqual("http://127.0.0.1:8000", local.GetBaseUrl());

            var ipv6 = new OpButtonConfig.Builder("http://[::1]:8000", "token")
                .SetAllowInsecureLocalhost(true)
                .Build();
            Assert.AreEqual("http://[::1]:8000", ipv6.GetBaseUrl());
        }

        [Test]
        public void DeviceSecretUsesProtocolBounds()
        {
            Assert.Throws<ArgumentException>(() =>
                new OpButtonConfig.Builder("https://example.com", "token")
                    .SetDeviceSecret("short")
                    .Build());
            Assert.Throws<ArgumentException>(() =>
                new OpButtonConfig.Builder("https://example.com", "token")
                    .SetDeviceSecret(new string('x', 257))
                    .Build());
            Assert.Throws<ArgumentException>(() =>
                new OpButtonConfig.Builder("https://example.com", "token")
                    .SetDeviceSecret(new string('x', 32) + "\n")
                    .Build());
            Assert.Throws<ArgumentException>(() =>
                new OpButtonConfig.Builder(
                    "https://example.com", "token\r\nInjected: true").Build());
        }
    }
}
