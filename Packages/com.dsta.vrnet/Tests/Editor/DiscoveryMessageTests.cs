using System.Text;
using NUnit.Framework;
using DSTA.VRNet.Discovery;

namespace DSTA.VRNet.Tests
{
    public class DiscoveryMessageTests
    {
        [Test]
        public void RoundTrip_PreservesAllFields()
        {
            var original = DiscoveryMessage.Create("TEAM-A", "Tablet 1", 7777, "0.1.0");
            bool ok = DiscoveryMessage.TryParse(original.ToBytes(), out var parsed);

            Assert.IsTrue(ok);
            Assert.AreEqual("TEAM-A", parsed.teamId);
            Assert.AreEqual("Tablet 1", parsed.serverName);
            Assert.AreEqual(7777, parsed.port);
            Assert.AreEqual("0.1.0", parsed.appVersion);
        }

        [Test]
        public void TryParse_Garbage_ReturnsFalse()
        {
            bool ok = DiscoveryMessage.TryParse(Encoding.UTF8.GetBytes("not json {{{"), out _);
            Assert.IsFalse(ok);
        }

        [Test]
        public void TryParse_WrongMagic_ReturnsFalse()
        {
            var msg = DiscoveryMessage.Create("TEAM-A", "x", 7777, "0.1.0");
            msg.magic = "NOPE";
            bool ok = DiscoveryMessage.TryParse(msg.ToBytes(), out _);
            Assert.IsFalse(ok);
        }

        [Test]
        public void TryParse_NullOrEmpty_ReturnsFalse()
        {
            Assert.IsFalse(DiscoveryMessage.TryParse(null, out _));
            Assert.IsFalse(DiscoveryMessage.TryParse(new byte[0], out _));
        }
    }
}
