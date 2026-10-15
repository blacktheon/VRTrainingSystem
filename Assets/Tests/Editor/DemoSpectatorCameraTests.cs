using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public class DemoSpectatorCameraTests
{
    GameObject _cameraObject;
    GameObject _canvasObject;
    Camera _camera;
    Component _spectator;
    RectTransform _panel;
    Type _spectatorType;

    [SetUp]
    public void SetUp()
    {
        _spectatorType = Type.GetType("DemoSpectatorCamera, Assembly-CSharp");
        Assert.That(_spectatorType, Is.Not.Null);
        _cameraObject = new GameObject("Spectator viewport regression", typeof(Camera));
        _cameraObject.hideFlags = HideFlags.HideAndDontSave;
        _camera = _cameraObject.GetComponent<Camera>();
        _camera.transform.SetPositionAndRotation(new Vector3(0f, 7f, -10f), Quaternion.Euler(32f, 0f, 0f));
        _spectator = _cameraObject.AddComponent(_spectatorType);

        _canvasObject = new GameObject("Instructor viewport regression", typeof(RectTransform), typeof(Canvas));
        _canvasObject.hideFlags = HideFlags.HideAndDontSave;
        _canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        _panel = new GameObject("Instructor panel", typeof(RectTransform)).GetComponent<RectTransform>();
        _panel.SetParent(_canvasObject.transform, false);
        _panel.anchorMin = Vector2.zero;
        _panel.anchorMax = Vector2.up;
        _panel.pivot = Vector2.up;
        _panel.sizeDelta = new Vector2(Screen.width * 0.3f, 0f);
        // Permit the unfixed implementation to reach the behavioral assertion: it
        // leaves the camera over the panel because it has no reservation field yet.
        _spectatorType.GetField("_instructorPanel", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.SetValue(_spectator, _panel);
        Invoke("Awake");
    }

    [TearDown]
    public void TearDown()
    {
        if (_cameraObject != null) Object.DestroyImmediate(_cameraObject);
        if (_canvasObject != null) Object.DestroyImmediate(_canvasObject);
    }

    [Test]
    public void Viewport_ExcludesSidebarAndTracksItsScreenSize()
    {
        Canvas.ForceUpdateCanvases();
        Invoke("LateUpdate");
        AssertPanelExcluded();

        // A canvas scale or orientation change moves this edge in screen pixels.
        _panel.sizeDelta = new Vector2(Screen.width * 0.4f, 0f);
        _panel.localScale = new Vector3(1.25f, 1f, 1f);
        Canvas.ForceUpdateCanvases();
        Invoke("LateUpdate");
        AssertPanelExcluded();
        Assert.That(_camera.rect.xMin, Is.EqualTo(0.5f).Within(0.002f));

        _panel.gameObject.SetActive(false);
        Invoke("LateUpdate");
        Assert.That(_camera.rect, Is.EqualTo(new Rect(0f, 0f, 1f, 1f)),
            "Hiding the panel should return the unused area to the camera.");
    }

    [Test]
    public void Disable_RestoresTheOriginalCameraViewport()
    {
        var original = new Rect(0.05f, 0.1f, 0.9f, 0.8f);
        _camera.rect = original;
        Invoke("Awake");
        Canvas.ForceUpdateCanvases();
        Invoke("LateUpdate");
        AssertPanelExcluded();
        Invoke("OnDisable");
        Assert.That(_camera.rect, Is.EqualTo(original),
            "Disabling spectator controls must not leave the camera cropped.");
    }

    void AssertPanelExcluded()
    {
        var corners = new Vector3[4];
        _panel.GetWorldCorners(corners);
        float right = RectTransformUtility.WorldToScreenPoint(null, corners[2]).x;
        Assert.That(_camera.pixelRect.xMin, Is.GreaterThanOrEqualTo(right - 1f),
            "Overview must frame trainees in the unobscured area beside the instructor panel.");
        Assert.That(_camera.pixelRect.width, Is.GreaterThan(1f));
        Assert.That(_camera.aspect, Is.EqualTo(_camera.pixelRect.width / _camera.pixelRect.height).Within(0.002f),
            "Overview framing must use the narrowed viewport's aspect ratio.");
    }

    void Invoke(string method)
        => _spectatorType.GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_spectator, null);
}
