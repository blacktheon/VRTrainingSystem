using System;
using NUnit.Framework;
using DSTA.VRNet.Core;

namespace DSTA.VRNet.Tests
{
    public class PlayerIdentityTests
    {
        [SetUp]
        public void ClearStoredGuid() => PlayerIdentity.Reset();

        [Test]
        public void GetOrCreate_ReturnsValidGuid()
        {
            Assert.IsTrue(Guid.TryParse(PlayerIdentity.GetOrCreate(), out _));
        }

        [Test]
        public void GetOrCreate_IsStableAcrossCalls()
        {
            Assert.AreEqual(PlayerIdentity.GetOrCreate(), PlayerIdentity.GetOrCreate());
        }

        [Test]
        public void Reset_ProducesNewGuid()
        {
            var first = PlayerIdentity.GetOrCreate();
            PlayerIdentity.Reset();
            Assert.AreNotEqual(first, PlayerIdentity.GetOrCreate());
        }
    }
}
