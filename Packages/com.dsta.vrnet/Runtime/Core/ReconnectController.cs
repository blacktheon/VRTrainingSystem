using System.Collections;
using Unity.Netcode;
using UnityEngine;

namespace DSTA.VRNet.Core
{
    /// <summary>
    /// Client-side resilience: after a successful connection, any disconnect or failed
    /// re-discovery triggers another StartClientAuto with exponential backoff. Enabled
    /// on first successful connect; disabled by VRNetSession.StopSession().
    /// </summary>
    public class ReconnectController : MonoBehaviour
    {
        public bool Enabled;

        VRNetSession _session;
        float _backoffSeconds = 1f;
        Coroutine _pending;

        void Start()
        {
            _session = GetComponent<VRNetSession>();
            _session.OnConnected += () =>
            {
                Enabled = true;
                _backoffSeconds = 1f;
            };
            _session.OnDisconnected += ScheduleRetry;
            _session.OnDiscoveryTimeout += ScheduleRetry;
        }

        void ScheduleRetry()
        {
            if (!Enabled || _pending != null) return;
            _pending = StartCoroutine(Retry());
        }

        IEnumerator Retry()
        {
            Debug.Log($"[VRNet] Reconnecting in {_backoffSeconds:0.#}s...");
            yield return new WaitForSeconds(_backoffSeconds);
            _backoffSeconds = Mathf.Min(_backoffSeconds * 2f, _session.Config.reconnectMaxBackoffSeconds);
            _pending = null;
            if (Enabled) _session.StartClientAuto();
        }
    }
}
