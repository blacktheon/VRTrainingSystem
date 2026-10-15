using System.Text;
using DSTA.VRNet.Core;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Instructor controls for the flat tablet server.</summary>
public class DemoServerUI : MonoBehaviour
{
    [SerializeField] VRNetSession _session;
    [SerializeField] TMP_Text _statusText;
    [SerializeField] TMP_Text _playerListText;
    [SerializeField] TMP_InputField _teamIdField;
    [SerializeField] GameObject _startPanel;
    [SerializeField] Button _dayNightButton;
    [SerializeField] TMP_Text _dayNightLabel;
    [SerializeField] DemoSpectatorCamera _spectatorCamera;
    [SerializeField] TMP_Text _cameraModeText;

    VRNetSession _subscribedSession;

    bool ServerRunning => NetworkManager.Singleton != null &&
        NetworkManager.Singleton.IsServer && !NetworkManager.Singleton.ShutdownInProgress;

    void OnEnable()
    {
        Subscribe();
        RefreshPlayerList();
    }

    void Start()
    {
        Subscribe();
        if (_teamIdField != null && _session != null && _session.Config != null)
            _teamIdField.text = _session.Config.teamId;
        RefreshPlayerList();
        RefreshControls();
    }

    void OnDisable()
    {
        if (_subscribedSession == null) return;
        _subscribedSession.OnStatusChanged -= HandleStatusChanged;
        _subscribedSession.OnPlayersChanged -= RefreshPlayerList;
        _subscribedSession = null;
    }

    void Subscribe()
    {
        if (_session == null) _session = VRNetSession.Instance;
        if (_session == null || _subscribedSession != null) return;
        _subscribedSession = _session;
        _subscribedSession.OnStatusChanged += HandleStatusChanged;
        _subscribedSession.OnPlayersChanged += RefreshPlayerList;
    }

    void LateUpdate() => RefreshControls();

    void HandleStatusChanged(string status)
    {
        if (_statusText != null) _statusText.text = status;
        RefreshPlayerList();
        RefreshControls();
    }

    public void OnStartServerClicked()
    {
        if (_session == null || _session.Config == null || ServerRunning) return;
        if (_teamIdField != null && !string.IsNullOrWhiteSpace(_teamIdField.text))
            _session.Config.teamId = _teamIdField.text.Trim();
        _session.StartServer();
        // Failed starts retain the retry controls. NGO reports success through IsServer.
        RefreshControls();
    }

    public void OnStopServerClicked()
    {
        if (_session != null) _session.StopSession();
        if (_spectatorCamera != null) _spectatorCamera.ShowOverview();
        RefreshPlayerList();
        RefreshControls();
    }

    public void OnToggleDayNightClicked()
    {
        var state = DemoSessionState.Instance;
        if (state != null) state.ToggleDayNight();
        RefreshControls();
    }

    public void OnOverviewClicked()
    {
        if (_spectatorCamera != null) _spectatorCamera.ShowOverview();
        RefreshControls();
    }

    public void OnFollowNextClicked()
    {
        if (_spectatorCamera != null) _spectatorCamera.FollowNextPlayer();
        RefreshControls();
    }

    void RefreshControls()
    {
        bool running = ServerRunning;
        if (_startPanel != null && _startPanel.activeSelf == running)
            _startPanel.SetActive(!running);

        var state = DemoSessionState.Instance;
        if (_dayNightButton != null) _dayNightButton.interactable = running && state != null;
        if (_dayNightLabel != null)
            _dayNightLabel.text = state != null && state.IsNight ? "Switch to Day" : "Switch to Night";
        if (_cameraModeText != null)
            _cameraModeText.text = _spectatorCamera != null ? _spectatorCamera.ViewLabel : "Overview";
    }

    void RefreshPlayerList()
    {
        if (_playerListText == null) return;
        int connected = 0;
        var builder = new StringBuilder();
        if (_session != null)
            foreach (var player in _session.Registry.AllPlayers)
            {
                if (player.Connected) connected++;
                builder.AppendLine($"{player.PlayerName}  {(player.Connected ? "connected" : "DISCONNECTED")}");
            }
        if (builder.Length == 0) builder.Append("Waiting for trainees...");
        builder.Insert(0, $"Connected: {connected}\n\n");
        _playerListText.text = builder.ToString();
    }
}
