using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace DSTA.VRNet.Avatar
{
    /// <summary>
    /// Networked player avatar. The owner mirrors its LocalRig in world space each
    /// frame; OwnerNetworkTransforms replicate to everyone else. The owner's own
    /// renderers are hidden (you don't want to see your own head from inside).
    /// </summary>
    public class VRNetAvatar : NetworkBehaviour
    {
        static readonly List<VRNetAvatar> All = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => All.Clear();

        [SerializeField] Transform _head;
        [SerializeField] Transform _leftHand;
        [SerializeField] Transform _rightHand;
        [SerializeField] Renderer[] _hiddenForOwner;

        // OwnerClientId reverts to the server when the owning client disconnects
        // (DontDestroyWithOwner keeps the avatar alive), so stale-avatar lookup must
        // key on the clientId the avatar was spawned for, not the current owner.
        ulong _spawnOwnerClientId;

        public override void OnNetworkSpawn()
        {
            _spawnOwnerClientId = OwnerClientId;
            All.Add(this);
            if (IsOwner && _hiddenForOwner != null)
                foreach (var renderer in _hiddenForOwner)
                    if (renderer != null)
                        renderer.enabled = false;
        }

        public override void OnNetworkDespawn() => All.Remove(this);

        void LateUpdate()
        {
            if (!IsOwner || !LocalRig.IsSet) return;
            transform.SetPositionAndRotation(LocalRig.Root.position, LocalRig.Root.rotation);
            _head.SetPositionAndRotation(LocalRig.Head.position, LocalRig.Head.rotation);
            _leftHand.SetPositionAndRotation(LocalRig.LeftHand.position, LocalRig.LeftHand.rotation);
            _rightHand.SetPositionAndRotation(LocalRig.RightHand.position, LocalRig.RightHand.rotation);
        }

        /// <summary>
        /// Finds the avatar spawned for <paramref name="clientId"/>, even after a
        /// disconnect has reverted its live ownership to the server.
        /// </summary>
        public static VRNetAvatar FindByOwner(ulong clientId)
            => All.Find(a => a._spawnOwnerClientId == clientId);
    }
}
