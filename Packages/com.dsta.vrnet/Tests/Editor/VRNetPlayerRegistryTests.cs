using NUnit.Framework;
using DSTA.VRNet.Core;

namespace DSTA.VRNet.Tests
{
    public class VRNetPlayerRegistryTests
    {
        const string GuidA = "5f0c7c9e-9d3a-4a5e-8b21-2f6f0a34d111";

        static ConnectionPayload Payload(string guid = GuidA, string name = "Trainee 1")
            => new ConnectionPayload { playerGuid = guid, playerName = name, teamId = "TEAM-A", appVersion = "0.1.0" };

        [Test]
        public void FirstJoin_IsNew_AndConnected()
        {
            var registry = new VRNetPlayerRegistry();
            var kind = registry.RecordApproved(1, Payload());

            Assert.AreEqual(JoinKind.New, kind);
            Assert.IsTrue(registry.TryGetByClient(1, out var record));
            Assert.IsTrue(record.Connected);
            Assert.AreEqual("Trainee 1", record.PlayerName);
        }

        [Test]
        public void Disconnect_MarksNotConnected_AndUnmapsClientId()
        {
            var registry = new VRNetPlayerRegistry();
            registry.RecordApproved(1, Payload());
            var record = registry.RecordDisconnected(1);

            Assert.IsNotNull(record);
            Assert.IsFalse(record.Connected);
            Assert.IsFalse(registry.TryGetByClient(1, out _));
            Assert.IsTrue(registry.TryGetByGuid(GuidA, out _));
        }

        [Test]
        public void Rejoin_SameGuidNewClientId_IsRejoin_AndRemaps()
        {
            var registry = new VRNetPlayerRegistry();
            registry.RecordApproved(1, Payload());
            registry.RecordDisconnected(1);
            var kind = registry.RecordApproved(2, Payload());

            Assert.AreEqual(JoinKind.Rejoin, kind);
            Assert.IsFalse(registry.TryGetByClient(1, out _));
            Assert.IsTrue(registry.TryGetByClient(2, out var record));
            Assert.IsTrue(record.Connected);
        }

        [Test]
        public void Disconnect_UnknownClient_ReturnsNull()
        {
            Assert.IsNull(new VRNetPlayerRegistry().RecordDisconnected(42));
        }

        [Test]
        public void Disconnect_StaleClientIdAfterRejoin_DoesNotAffectRejoinedRecord()
        {
            var registry = new VRNetPlayerRegistry();
            registry.RecordApproved(1, Payload());
            // Client reconnects with a new clientId before the server notices the drop
            registry.RecordApproved(2, Payload());
            // The late disconnect for the OLD clientId now fires
            var stale = registry.RecordDisconnected(1);

            Assert.IsNull(stale);
            Assert.IsTrue(registry.TryGetByClient(2, out var record));
            Assert.IsTrue(record.Connected);
        }

        [Test]
        public void ConnectedPlayers_ExcludesDropped()
        {
            var registry = new VRNetPlayerRegistry();
            registry.RecordApproved(1, Payload());
            registry.RecordApproved(2, Payload(guid: "6a1d8d0f-1e4b-4b6f-9c32-3a7b1b45e222", name: "Trainee 2"));
            registry.RecordDisconnected(1);

            var connected = new System.Collections.Generic.List<VRNetPlayerRegistry.PlayerRecord>(registry.ConnectedPlayers);
            Assert.AreEqual(1, connected.Count);
            Assert.AreEqual("Trainee 2", connected[0].PlayerName);
        }
    }
}
