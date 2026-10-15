using DSTA.VRNet.Core;
using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>Headset connect UI: auto-join, manual IP fallback, live status line.</summary>
public class DemoClientUI : MonoBehaviour
{
    [SerializeField] VRNetSession _session;
    [SerializeField] TMP_Text _statusText;
    [SerializeField] TMP_InputField _manualIpField;
    [SerializeField] GameObject _connectPanel;
    [SerializeField] bool _autoJoinOnStart = true;
    [Tooltip("Leave empty for LAN discovery. Set an IP for networks that block broadcast.")]
    [SerializeField] string _serverAddress;
    [SerializeField] string _playerName;
    Coroutine _initialRetry;
    bool _hasConnected;
    bool _manualConnection;

    IEnumerator Start()
    {
        if (_session == null) yield break;
        _session.OnStatusChanged += SetStatus;
        _session.OnConnected += Connected;
        _session.OnDisconnected += Disconnected;
        _session.OnDiscoveryTimeout += RetryInitialConnection;
        _session.OnStopped += CancelInitialRetry;
        if (!string.IsNullOrWhiteSpace(_playerName)) _session.PlayerName = _playerName.Trim();
        if (_manualIpField != null) _manualIpField.text = _serverAddress;
        yield return null; // All rig binders register before the first connection can spawn an avatar.
        if (_autoJoinOnStart)
        {
            if (string.IsNullOrWhiteSpace(_serverAddress)) OnJoinAutoClicked();
            else { _manualConnection = true; _session.StartClient(_serverAddress.Trim()); }
        }
    }

    void SetStatus(string status) { if (_statusText != null) _statusText.text = status; }
    void Connected()
    {
        _hasConnected = true;
        CancelInitialRetry();
        if (_connectPanel != null) _connectPanel.SetActive(false);
    }
    void Disconnected()
    {
        if (_connectPanel != null) _connectPanel.SetActive(true);
        RetryInitialConnection();
    }

    void RetryInitialConnection()
    {
        // The library handles reconnects after the first successful join. The sample
        // also supports starting a headset before the instructor starts the server.
        if (!_hasConnected && _autoJoinOnStart && _initialRetry == null && isActiveAndEnabled)
            _initialRetry = StartCoroutine(RetryAfterDelay());
    }

    IEnumerator RetryAfterDelay()
    {
        yield return new WaitForSecondsRealtime(2f);
        _initialRetry = null;
        if (_session == null) yield break;
        if (_manualConnection) _session.StartClient(_serverAddress);
        else _session.StartClientAuto();
    }

    void CancelInitialRetry()
    {
        if (_initialRetry != null) StopCoroutine(_initialRetry);
        _initialRetry = null;
    }

    public void OnJoinAutoClicked()
    {
        CancelInitialRetry();
        _manualConnection = false;
        if (_session != null) _session.StartClientAuto();
    }
    public void OnJoinManualClicked()
    {
        var address = _manualIpField != null ? _manualIpField.text.Trim() : _serverAddress?.Trim();
        if (string.IsNullOrWhiteSpace(address)) { SetStatus("Enter the tablet's IP address first."); return; }
        CancelInitialRetry();
        _manualConnection = true;
        _serverAddress = address;
        if (_session != null) _session.StartClient(address);
    }

    void OnDestroy()
    {
        if (_session == null) return;
        _session.OnStatusChanged -= SetStatus;
        _session.OnConnected -= Connected;
        _session.OnDisconnected -= Disconnected;
        _session.OnDiscoveryTimeout -= RetryInitialConnection;
        _session.OnStopped -= CancelInitialRetry;
    }
}
