using Unity.Netcode.Components;

namespace DSTA.VRNet.Avatar
{
    /// <summary>Owner-authoritative NetworkTransform: each headset drives its own avatar.</summary>
    public class OwnerNetworkTransform : NetworkTransform
    {
        protected override bool OnIsServerAuthoritative() => false;
    }
}
