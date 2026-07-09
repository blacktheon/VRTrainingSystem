using System;
using System.Text;
using UnityEngine;

namespace DSTA.VRNet.Core
{
    /// <summary>Identity a client sends with its connection request (NGO ConnectionData).</summary>
    [Serializable]
    public struct ConnectionPayload
    {
        public string playerGuid;
        public string playerName;
        public string teamId;
        public string appVersion;

        public byte[] ToBytes() => Encoding.UTF8.GetBytes(JsonUtility.ToJson(this));

        public static bool TryParse(byte[] data, out ConnectionPayload payload)
        {
            payload = default;
            if (data == null || data.Length == 0 || data.Length > 8192) return false;
            try
            {
                payload = JsonUtility.FromJson<ConnectionPayload>(Encoding.UTF8.GetString(data));
            }
            catch (ArgumentException)
            {
                return false;
            }
            return Guid.TryParse(payload.playerGuid, out _);
        }
    }
}
