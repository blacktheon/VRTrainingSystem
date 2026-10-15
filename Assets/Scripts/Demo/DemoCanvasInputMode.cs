using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR;

/// <summary>Selects tracked canvas input in VR and mouse/keyboard input outside VR.</summary>
public class DemoCanvasInputMode : MonoBehaviour
{
    [SerializeField] BaseInputModule _trackedModule;
    [SerializeField] BaseInputModule _desktopModule;

    void OnEnable() => Update();

    void Update()
    {
        // This component is only on the Quest client scene, never the tablet.
        bool tracked = Application.platform == RuntimePlatform.Android || XRSettings.isDeviceActive;
        if (_desktopModule != null && _desktopModule.enabled == tracked) _desktopModule.enabled = !tracked;
        if (_trackedModule != null && _trackedModule.enabled != tracked) _trackedModule.enabled = tracked;
    }
}
