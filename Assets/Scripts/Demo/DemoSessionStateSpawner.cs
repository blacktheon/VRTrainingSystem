using Unity.Netcode;
using UnityEngine;

/// <summary>One dynamic state object per server session, shared by the two entry scenes.</summary>
[RequireComponent(typeof(NetworkManager))]
public class DemoSessionStateSpawner : MonoBehaviour
{
    [SerializeField] NetworkObject _statePrefab;
    NetworkManager _network;
    NetworkObject _spawnedState;

    void OnEnable()
    {
        _network = GetComponent<NetworkManager>();
        _network.OnServerStarted += SpawnState;
    }

    void OnDisable()
    {
        if (_network != null) _network.OnServerStarted -= SpawnState;
    }

    void SpawnState()
    {
        if (_spawnedState != null && _spawnedState.IsSpawned) return;
        if (_statePrefab == null)
        {
            Debug.LogError("[VRNet Demo] Assign the shared session state prefab.", this);
            return;
        }
        _spawnedState = Instantiate(_statePrefab);
        _spawnedState.Spawn();
    }
}
