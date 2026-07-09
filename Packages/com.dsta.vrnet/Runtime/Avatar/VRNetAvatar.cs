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
        public static readonly List<VRNetAvatar> All = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => All.Clear();

        [SerializeField] Transform _head;
        [SerializeField] Transform _leftHand;
        [SerializeField] Transform _rightHand;
        [SerializeField] Renderer[] _hiddenForOwner;

        public override void OnNetworkSpawn()
        {
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

        public static VRNetAvatar FindByOwner(ulong clientId)
            => All.Find(a => a.OwnerClientId == clientId);
    }
}
