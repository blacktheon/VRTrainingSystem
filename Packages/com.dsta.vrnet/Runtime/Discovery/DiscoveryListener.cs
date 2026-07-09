using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEngine;

namespace DSTA.VRNet.Discovery
{
    /// <summary>
    /// Client-side: listens for server broadcasts, filters by team, and raises
    /// OnServerFound on the main thread. ReuseAddress allows several listeners on
    /// one machine (Multiplayer Play Mode virtual players).
    /// </summary>
    public class DiscoveryListener : MonoBehaviour
    {
        public event Action<IPAddress, DiscoveryMessage> OnServerFound;

        UdpClient _udp;
        string _teamFilter;
        readonly ConcurrentQueue<(IPAddress address, DiscoveryMessage message)> _found = new();

        public void StartListening(int discoveryPort, string teamFilter)
        {
            StopListening();
            _teamFilter = teamFilter;
            _udp = new UdpClient();
            _udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _udp.Client.Bind(new IPEndPoint(IPAddress.Any, discoveryPort));
            _ = ReceiveLoop(_udp);
        }

        public void StopListening()
        {
            _udp?.Close();
            _udp = null;
        }

        async Task ReceiveLoop(UdpClient udp)
        {
            while (true)
            {
                UdpReceiveResult result;
                try { result = await udp.ReceiveAsync(); }
                catch (ObjectDisposedException) { return; }
                catch (SocketException) { return; }

                if (DiscoveryMessage.TryParse(result.Buffer, out var message) && message.teamId == _teamFilter)
                    _found.Enqueue((result.RemoteEndPoint.Address, message));
            }
        }

        void Update()
        {
            while (_found.TryDequeue(out var hit))
                OnServerFound?.Invoke(hit.address, hit.message);
        }

        void OnDestroy() => StopListening();
    }
}
