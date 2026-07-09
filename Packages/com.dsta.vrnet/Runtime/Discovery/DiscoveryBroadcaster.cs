using System.Net;
using System.Net.Sockets;
using UnityEngine;

namespace DSTA.VRNet.Discovery
{
    /// <summary>Server-side: periodically broadcasts a DiscoveryMessage on the LAN.</summary>
    public class DiscoveryBroadcaster : MonoBehaviour
    {
        UdpClient _udp;
        byte[] _bytes;
        int _port;

        public void StartBroadcasting(DiscoveryMessage message, int discoveryPort, float intervalSeconds)
        {
            StopBroadcasting();
            _udp = new UdpClient { EnableBroadcast = true };
            _bytes = message.ToBytes();
            _port = discoveryPort;
            InvokeRepeating(nameof(Broadcast), 0f, intervalSeconds);
        }

        public void StopBroadcasting()
        {
            CancelInvoke(nameof(Broadcast));
            _udp?.Close();
            _udp = null;
        }

        void Broadcast()
        {
            try
            {
                _udp.Send(_bytes, _bytes.Length, new IPEndPoint(IPAddress.Broadcast, _port));
            }
            catch (SocketException e)
            {
                Debug.LogWarning($"[VRNet] Discovery broadcast failed: {e.Message}");
            }
        }

        void OnDestroy() => StopBroadcasting();
    }
}
