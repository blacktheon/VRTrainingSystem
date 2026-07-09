using DSTA.VRNet.Core;
using UnityEngine;

/// <summary>
/// Fake XR rig for testing without a headset: creates rig transforms and orbits the
/// head so avatar sync is visible. Enable this GameObject only for desktop tests;
/// keep it disabled when DemoRigBinder provides the real rig.
/// </summary>
public class DemoFakeRig : MonoBehaviour
{
    Transform _root, _head, _left, _right;

    void Start()
    {
        _root = new GameObject("FakeRoot").transform;
        _root.SetParent(transform, false);
        _head = new GameObject("FakeHead").transform;
        _head.SetParent(_root, false);
        _left = new GameObject("FakeLeftHand").transform;
        _left.SetParent(_root, false);
        _right = new GameObject("FakeRightHand").transform;
        _right.SetParent(_root, false);
        VRNetSession.SetLocalRig(_root, _head, _left, _right);
    }

    void Update()
    {
        float t = Time.time;
        _head.localPosition = new Vector3(Mathf.Sin(t) * 0.5f, 1.6f, Mathf.Cos(t) * 0.5f);
        _head.localRotation = Quaternion.Euler(0f, t * 40f, 0f);
        _left.localPosition = _head.localPosition + new Vector3(-0.3f, -0.4f, 0.2f);
        _right.localPosition = _head.localPosition + new Vector3(0.3f, -0.4f, 0.2f);
    }
}
