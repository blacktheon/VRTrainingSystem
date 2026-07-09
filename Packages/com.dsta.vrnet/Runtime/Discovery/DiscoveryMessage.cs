using System;
using System.Text;
using UnityEngine;

namespace DSTA.VRNet.Discovery
{
    /// <summary>
    /// Payload broadcast by the server tablet over UDP so headsets can find it.
    /// JSON-encoded for forward compatibility; 'magic' guards against foreign packets.
    /// </summary>
    [Serializable]
    public struct DiscoveryMessage
    {
        public const string MagicValue = "VRNET1";

        public string magic;
        public string teamId;
        public string serverName;
        public int port;
        public string appVersion;

        public static DiscoveryMessage Create(string teamId, string serverName, int port, string appVersion)
        {
            return new DiscoveryMessage
            {
                magic = MagicValue,
                teamId = teamId,
                serverName = serverName,
                port = port,
                appVersion = appVersion
            };
        }

        public byte[] ToBytes() => Encoding.UTF8.GetBytes(JsonUtility.ToJson(this));

        public static bool TryParse(byte[] data, out DiscoveryMessage message)
        {
            message = default;
            if (data == null || data.Length == 0) return false;
            try
            {
                message = JsonUtility.FromJson<DiscoveryMessage>(Encoding.UTF8.GetString(data));
            }
            catch (Exception)
            {
                return false;
            }
            return message.magic == MagicValue;
        }
    }
}
