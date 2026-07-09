using UnityEngine;

namespace DSTA.VRNet.Avatar
{
    /// <summary>
    /// The integration point that replaces game-specific rig singletons:
    /// the game registers its XR rig transforms here (via VRNetSession.SetLocalRig)
    /// and the library's avatar mirrors them. The library never references game types.
    /// </summary>
    public static class LocalRig
    {
        public static Transform Root { get; private set; }
        public static Transform Head { get; private set; }
        public static Transform LeftHand { get; private set; }
        public static Transform RightHand { get; private set; }

        public static bool IsSet => Root != null && Head != null && LeftHand != null && RightHand != null;

        public static void Set(Transform root, Transform head, Transform leftHand, Transform rightHand)
        {
            Root = root;
            Head = head;
            LeftHand = leftHand;
            RightHand = rightHand;
        }

        public static void Clear() => Set(null, null, null, null);
    }
}
