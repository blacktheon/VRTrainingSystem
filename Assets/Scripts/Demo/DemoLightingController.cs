using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

/// <summary>Local presentation of the server's state; never writes network state.</summary>
public class DemoLightingController : MonoBehaviour
{
    [SerializeField] Light _sun;
    [SerializeField, Range(0f, 1f)] float _nightIntensity = 0.06f;
    DemoSessionState _state;
    float _dayAmbientIntensity, _dayExposure;
    Color _dayAmbient, _dayFog;
    readonly List<SunState> _suns = new();
    struct SunState
    {
        public Light light;
        public float intensity;
        public Color color;
        public Quaternion rotation;
    }
    AmbientMode _dayAmbientMode;
    Material _originalSky, _runtimeSky;

    void Awake()
    {
        if (_sun == null) _sun = RenderSettings.sun;
        if (_sun != null) CaptureSun(_sun);
        // Imported environments can contain their own sun in addition to the scene's
        // directional light. Leaving either at daylight intensity hides the night mode.
        foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (light != _sun && light.type == LightType.Directional) CaptureSun(light);
        _dayAmbientMode = RenderSettings.ambientMode;
        _dayAmbient = RenderSettings.ambientLight;
        _dayAmbientIntensity = RenderSettings.ambientIntensity;
        _dayFog = RenderSettings.fogColor;
        _originalSky = RenderSettings.skybox;
        if (_originalSky != null)
        {
            _runtimeSky = new Material(_originalSky);
            if (_runtimeSky.HasProperty("_Exposure")) _dayExposure = _runtimeSky.GetFloat("_Exposure");
            RenderSettings.skybox = _runtimeSky;
        }
    }

    void CaptureSun(Light light) => _suns.Add(new SunState
    {
        light = light, intensity = light.intensity, color = light.color,
        rotation = light.transform.rotation
    });

    void Update()
    {
        var current = DemoSessionState.Instance;
        if (ReferenceEquals(current, _state)) return;
        if (_state != null) _state.NightChanged -= Apply;
        _state = current;
        if (_state != null) _state.NightChanged += Apply;
        Apply(_state != null && _state.IsNight);
    }

    void Apply(bool night)
    {
        foreach (var sun in _suns)
        {
            if (sun.light == null) continue;
            sun.light.intensity = night ? _nightIntensity : sun.intensity;
            sun.light.color = night ? new Color(0.55f, 0.66f, 1f) : sun.color;
            sun.light.transform.rotation = night ? Quaternion.Euler(15f, -120f, 0f) : sun.rotation;
        }
        RenderSettings.ambientMode = night ? AmbientMode.Flat : _dayAmbientMode;
        RenderSettings.ambientLight = night ? new Color(0.035f, 0.045f, 0.08f) : _dayAmbient;
        RenderSettings.ambientIntensity = night ? 0.2f : _dayAmbientIntensity;
        RenderSettings.fogColor = night ? new Color(0.015f, 0.025f, 0.055f) : _dayFog;
        if (_runtimeSky != null && _runtimeSky.HasProperty("_Exposure"))
            _runtimeSky.SetFloat("_Exposure", night ? _dayExposure * 0.025f : _dayExposure);
    }

    void OnDestroy()
    {
        if (_state != null) _state.NightChanged -= Apply;
        Apply(false);
        if (_runtimeSky != null)
        {
            if (RenderSettings.skybox == _runtimeSky) RenderSettings.skybox = _originalSky;
            Destroy(_runtimeSky);
        }
    }
}
