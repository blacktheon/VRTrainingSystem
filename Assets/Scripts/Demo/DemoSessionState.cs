using System;
using Unity.Netcode;
using UnityEngine;

/// <summary>Persistent instructor state, automatically synchronized to late joiners.</summary>
[RequireComponent(typeof(NetworkObject))]
public class DemoSessionState : NetworkBehaviour
{
    public static DemoSessionState Instance { get; private set; }
    readonly NetworkVariable<bool> _isNight = new(false,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public bool IsNight => _isNight.Value;
    public event Action<bool> NightChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Instance = null;

    public override void OnNetworkSpawn()
    {
        Instance = this;
        _isNight.OnValueChanged += OnNightChanged;
        NightChanged?.Invoke(IsNight);
    }

    public bool ToggleDayNight()
    {
        if (!IsSpawned || !IsServer) return false;
        _isNight.Value = !_isNight.Value;
        return true;
    }

    void OnNightChanged(bool previous, bool current) => NightChanged?.Invoke(current);

    public override void OnNetworkDespawn()
    {
        _isNight.OnValueChanged -= OnNightChanged;
        if (Instance == this) Instance = null;
    }
}
