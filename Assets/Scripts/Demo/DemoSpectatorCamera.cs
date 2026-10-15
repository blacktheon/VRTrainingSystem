using System.Collections.Generic;
using DSTA.VRNet.Avatar;
using DSTA.VRNet.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Touch/mouse orbit camera for the tablet's overview and trainee follow views.</summary>
[RequireComponent(typeof(Camera))]
public class DemoSpectatorCamera : MonoBehaviour
{
    [SerializeField] VRNetSession _session;
    [SerializeField] RectTransform _instructorPanel;
    [SerializeField] Vector3 _fallbackTarget = new(0f, 1f, 0f);
    [SerializeField, Min(0.5f)] float _minimumDistance = 2f;
    [SerializeField, Min(1f)] float _maximumDistance = 100f;
    [SerializeField, Min(1f)] float _followDistance = 5f;
    [SerializeField, Range(0.01f, 1f)] float _orbitSensitivity = 0.15f;
    [SerializeField, Min(0.1f)] float _smoothing = 8f;

    public string ViewLabel { get; private set; } = "Overview";

    readonly List<VRNetPlayerRegistry.PlayerRecord> _candidates = new();
    readonly List<RaycastResult> _uiHits = new();
    readonly HashSet<int> _blockedTouches = new();
    readonly Vector3[] _panelCorners = new Vector3[4];
    Camera _camera;
    Rect _originalCameraRect;
    PointerEventData _pointerData;
    EventSystem _pointerEventSystem;
    string _followGuid;
    Vector3 _initialTarget;
    Vector3 _smoothedTarget;
    float _initialDistance;
    float _distance;
    float _zoom = 1f;
    float _yaw;
    float _pitch;
    bool _mouseOrbitAllowed;
    int _previousTouchCount;
    int _previousFirstTouch;
    int _previousSecondTouch;
    Vector2 _previousTouchPosition;
    float _previousPinchDistance;

    void Awake()
    {
        _camera = GetComponent<Camera>();
        _originalCameraRect = _camera.rect;
        _maximumDistance = Mathf.Max(_minimumDistance, _maximumDistance);
        _yaw = transform.eulerAngles.y;
        _pitch = Mathf.Clamp(Mathf.DeltaAngle(0f, transform.eulerAngles.x), 10f, 80f);
        var direction = Quaternion.Euler(_pitch, _yaw, 0f) * Vector3.forward;
        // Intersect the scene camera's viewing direction with trainee height, retaining
        // its useful starting composition without dividing by a horizontal direction.
        float planeDistance = Mathf.Abs(direction.y) > 0.001f
            ? (_fallbackTarget.y - transform.position.y) / direction.y : -1f;
        _initialDistance = Mathf.Clamp(planeDistance > 0f ? planeDistance : _followDistance,
            _minimumDistance, _maximumDistance);
        _initialTarget = planeDistance > 0f
            ? transform.position + direction * _initialDistance : _fallbackTarget;
        _smoothedTarget = _initialTarget;
        _distance = _initialDistance;
    }

    void OnDisable()
    {
        if (_camera != null)
        {
            _camera.rect = _originalCameraRect;
            _camera.ResetAspect();
        }
        _blockedTouches.Clear();
        _previousTouchCount = 0;
        _mouseOrbitAllowed = false;
    }

    public void ShowOverview()
    {
        _followGuid = null;
        _zoom = 1f;
        ViewLabel = "Overview";
    }

    public void FollowNextPlayer()
    {
        ResolveSession();
        _candidates.Clear();
        if (_session != null)
            foreach (var player in _session.Registry.ConnectedPlayers)
                if (FindAvatar(player) != null) _candidates.Add(player);

        if (_candidates.Count == 0)
        {
            ShowOverview();
            return;
        }

        _candidates.Sort((a, b) => a.ClientId.CompareTo(b.ClientId));
        int current = _candidates.FindIndex(player => player.PlayerGuid == _followGuid);
        var next = _candidates[(current + 1) % _candidates.Count];
        _followGuid = next.PlayerGuid;
        _zoom = 1f;
        ViewLabel = $"Following: {next.PlayerName}";
    }

    void LateUpdate()
    {
        ResolveSession();
        RefreshViewport();
        ReadInput();
        Vector3 target = _initialTarget;
        float desiredDistance = _initialDistance;
        bool following = false;
        if (_followGuid != null)
        {
            if (_session != null && _session.Registry.TryGetByGuid(_followGuid, out var player))
            {
                var avatar = FindAvatar(player);
                if (avatar != null)
                {
                    target = avatar.Head != null ? avatar.Head.position - Vector3.up * 0.3f
                        : avatar.transform.position + Vector3.up;
                    desiredDistance = _followDistance;
                    ViewLabel = $"Following: {player.PlayerName}";
                    following = true;
                }
            }
            if (!following) ShowOverview();
        }

        if (!following && TryGetOverviewBounds(out var bounds))
        {
            target = bounds.center;
            // A bounding sphere fits every orbit angle; use the narrower field of view
            // so portrait and landscape tablets both keep the whole group in frame.
            float halfVertical = _camera.fieldOfView * 0.5f * Mathf.Deg2Rad;
            float halfHorizontal = Mathf.Atan(Mathf.Tan(halfVertical) * Mathf.Max(0.1f, _camera.aspect));
            float halfFov = Mathf.Max(0.01f, Mathf.Min(halfVertical, halfHorizontal));
            desiredDistance = Mathf.Max(_initialDistance, (bounds.extents.magnitude + 0.5f) / Mathf.Sin(halfFov));
        }

        desiredDistance = Mathf.Clamp(desiredDistance * _zoom, _minimumDistance, _maximumDistance);
        float blend = 1f - Mathf.Exp(-_smoothing * Time.unscaledDeltaTime);
        _smoothedTarget = Vector3.Lerp(_smoothedTarget, target, blend);
        _distance = Mathf.Lerp(_distance, desiredDistance, blend);
        var rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        transform.SetPositionAndRotation(_smoothedTarget - rotation * Vector3.forward * _distance, rotation);
    }

    void RefreshViewport()
    {
        var viewport = _originalCameraRect;
        if (_instructorPanel != null && _instructorPanel.gameObject.activeInHierarchy && Screen.width > 0)
        {
            var canvas = _instructorPanel.GetComponentInParent<Canvas>();
            if (canvas != null && canvas.isActiveAndEnabled)
            {
                canvas = canvas.rootCanvas;
                var uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                _instructorPanel.GetWorldCorners(_panelCorners);
                float right = float.NegativeInfinity;
                foreach (var corner in _panelCorners)
                    right = Mathf.Max(right, RectTransformUtility.WorldToScreenPoint(uiCamera, corner).x);
                // Reserve the whole left strip, including any inset, so the center
                // and aspect used to frame trainees belong to the unobscured view.
                viewport.xMin = Mathf.Clamp(right / Screen.width, viewport.xMin,
                    Mathf.Max(viewport.xMin, viewport.xMax - 1f / Screen.width));
            }
        }
        if (_camera.rect == viewport) return;
        _camera.rect = viewport;
        _camera.ResetAspect();
    }

    void ResolveSession()
    {
        if (_session == null) _session = VRNetSession.Instance;
    }

    static VRNetAvatar FindAvatar(VRNetPlayerRegistry.PlayerRecord player)
    {
        if (!player.Connected) return null;
        var avatar = VRNetAvatar.FindByOwner(player.ClientId);
        return avatar != null && avatar.IsSpawned ? avatar : null;
    }

    bool TryGetOverviewBounds(out Bounds bounds)
    {
        bounds = default;
        bool found = false;
        if (_session == null) return false;
        foreach (var player in _session.Registry.ConnectedPlayers)
        {
            var avatar = FindAvatar(player);
            if (avatar == null) continue;
            if (!found) bounds = new Bounds(avatar.transform.position, Vector3.zero);
            found = true;
            bounds.Encapsulate(avatar.transform.position);
            if (avatar.Head != null) bounds.Encapsulate(avatar.Head.position);
            if (avatar.LeftHand != null) bounds.Encapsulate(avatar.LeftHand.position);
            if (avatar.RightHand != null) bounds.Encapsulate(avatar.RightHand.position);
        }
        return found;
    }

    void ReadInput()
    {
        if (ReadTouches())
        {
            _mouseOrbitAllowed = false;
            return;
        }
        var mouse = Mouse.current;
        if (mouse == null) return;
        bool blocked = IsPointerBlocked(mouse.position.ReadValue());
        if (mouse.leftButton.wasPressedThisFrame) _mouseOrbitAllowed = !blocked;
        if (blocked || !mouse.leftButton.isPressed) _mouseOrbitAllowed = false;
        if (_mouseOrbitAllowed) Orbit(mouse.delta.ReadValue());
        if (!blocked) Zoom(Mathf.Exp(-mouse.scroll.ReadValue().y * 0.0015f));
    }

    bool ReadTouches()
    {
        var screen = Touchscreen.current;
        int count = 0;
        bool anyTouch = false;
        int firstId = 0, secondId = 0;
        Vector2 first = default, second = default;
        if (screen != null)
            foreach (var touch in screen.touches)
            {
                int id = touch.touchId.ReadValue();
                if (!touch.press.isPressed)
                {
                    _blockedTouches.Remove(id);
                    continue;
                }
                anyTouch = true;
                var position = touch.position.ReadValue();
                if (touch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Began)
                    _blockedTouches.Remove(id);
                if (IsPointerBlocked(position)) _blockedTouches.Add(id);
                if (_blockedTouches.Contains(id)) continue;
                if (count == 0) { first = position; firstId = id; }
                else if (count == 1) { second = position; secondId = id; }
                count++;
            }

        if (count == 1 && _previousTouchCount == 1 && firstId == _previousFirstTouch)
            Orbit(first - _previousTouchPosition);
        if (count == 2)
        {
            float separation = Vector2.Distance(first, second);
            if (_previousTouchCount == 2 && firstId == _previousFirstTouch && secondId == _previousSecondTouch && separation > 1f)
                Zoom(_previousPinchDistance / separation);
            _previousPinchDistance = separation;
        }
        _previousTouchCount = count;
        _previousFirstTouch = firstId;
        _previousSecondTouch = secondId;
        _previousTouchPosition = first;
        if (!anyTouch) _blockedTouches.Clear();
        return anyTouch;
    }

    bool IsPointerBlocked(Vector2 position)
    {
        if (!_camera.pixelRect.Contains(position)) return true;
        var eventSystem = EventSystem.current;
        if (eventSystem == null) return false;
        if (_pointerData == null || _pointerEventSystem != eventSystem)
        {
            _pointerEventSystem = eventSystem;
            _pointerData = new PointerEventData(eventSystem);
        }
        _pointerData.Reset();
        _pointerData.position = position;
        _uiHits.Clear();
        eventSystem.RaycastAll(_pointerData, _uiHits);
        foreach (var hit in _uiHits)
            if (hit.module is GraphicRaycaster) return true;
        return false;
    }

    void Orbit(Vector2 delta)
    {
        _yaw = Mathf.Repeat(_yaw + delta.x * _orbitSensitivity, 360f);
        _pitch = Mathf.Clamp(_pitch - delta.y * _orbitSensitivity, 10f, 80f);
    }

    void Zoom(float factor)
    {
        if (factor > 0f) _zoom = Mathf.Clamp(_zoom * factor, 0.2f, 5f);
    }
}
