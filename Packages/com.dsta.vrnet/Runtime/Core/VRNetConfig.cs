using UnityEngine;

namespace DSTA.VRNet.Core
{
    /// <summary>All tunables for one deployment. Create one asset per project via the asset menu.</summary>
    [CreateAssetMenu(menuName = "VRNet/Config", fileName = "VRNetConfig")]
    public class VRNetConfig : ScriptableObject
    {
        [Header("Identity")]
        public string teamId = "TEAM-A";
        public string serverName = "VRNet Server";
        public string appVersion = "0.1.0";

        [Header("Network")]
        public int serverPort = 7777;
        public int discoveryPort = 47777;
        public float discoveryIntervalSeconds = 1f;
        public float discoveryTimeoutSeconds = 6f;

        [Header("Resilience")]
        [Tooltip("How long UTP tolerates silence before declaring disconnect. Generous for shaky Wi-Fi.")]
        public int disconnectTimeoutMs = 10000;
        public int connectTimeoutMs = 2000;
        public int maxConnectAttempts = 5;
        [Tooltip("Server keeps a dropped player's frozen avatar this long awaiting rejoin.")]
        public float avatarDespawnGraceSeconds = 30f;
        public float reconnectMaxBackoffSeconds = 10f;
    }
}
