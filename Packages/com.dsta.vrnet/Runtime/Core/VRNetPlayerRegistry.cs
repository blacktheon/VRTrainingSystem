using System.Collections.Generic;
using System.Linq;

namespace DSTA.VRNet.Core
{
    public enum JoinKind { New, Rejoin }

    /// <summary>
    /// Server-side bookkeeping: which persistent PlayerGuid is behind which transient
    /// NGO clientId. Records survive disconnects so rejoining trainees keep their identity.
    /// Pure data class — no Unity dependencies.
    /// </summary>
    public class VRNetPlayerRegistry
    {
        public class PlayerRecord
        {
            public string PlayerGuid;
            public string PlayerName;
            public ulong ClientId;
            public bool Connected;
        }

        readonly Dictionary<string, PlayerRecord> _byGuid = new();
        readonly Dictionary<ulong, string> _guidByClient = new();

        public IEnumerable<PlayerRecord> ConnectedPlayers => _byGuid.Values.Where(r => r.Connected);
        public IEnumerable<PlayerRecord> AllPlayers => _byGuid.Values;

        public JoinKind RecordApproved(ulong clientId, ConnectionPayload payload)
        {
            if (_byGuid.TryGetValue(payload.playerGuid, out var existing))
            {
                _guidByClient.Remove(existing.ClientId);
                existing.ClientId = clientId;
                existing.Connected = true;
                existing.PlayerName = payload.playerName;
                _guidByClient[clientId] = payload.playerGuid;
                return JoinKind.Rejoin;
            }

            _byGuid[payload.playerGuid] = new PlayerRecord
            {
                PlayerGuid = payload.playerGuid,
                PlayerName = payload.playerName,
                ClientId = clientId,
                Connected = true
            };
            _guidByClient[clientId] = payload.playerGuid;
            return JoinKind.New;
        }

        public PlayerRecord RecordDisconnected(ulong clientId)
        {
            if (!_guidByClient.TryGetValue(clientId, out var guid)) return null;
            var record = _byGuid[guid];
            record.Connected = false;
            _guidByClient.Remove(clientId);
            return record;
        }

        public bool TryGetByClient(ulong clientId, out PlayerRecord record)
        {
            record = null;
            return _guidByClient.TryGetValue(clientId, out var guid) && _byGuid.TryGetValue(guid, out record);
        }

        public bool TryGetByGuid(string playerGuid, out PlayerRecord record)
            => _byGuid.TryGetValue(playerGuid, out record);
    }
}
