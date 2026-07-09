using DSTA.VRNet.Core;
using UnityEngine;

/// <summary>Reference integration: hands the project's XR rig to the VRNet library.</summary>
public class DemoRigBinder : MonoBehaviour
{
    [SerializeField] Transform _root;      // XR Origin
    [SerializeField] Transform _head;      // Main Camera
    [SerializeField] Transform _leftHand;  // Left Controller
    [SerializeField] Transform _rightHand; // Right Controller

    void Start() => VRNetSession.SetLocalRig(_root, _head, _leftHand, _rightHand);
}
