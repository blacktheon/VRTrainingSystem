using System.Text;
using DSTA.VRNet.Core;
using TMPro;
using UnityEngine;

/// <summary>Tablet UI: set team ID, start the server, watch the roster.</summary>
public class DemoServerUI : MonoBehaviour
{
    [SerializeField] VRNetSession _session;
    [SerializeField] TMP_Text _statusText;
    [SerializeField] TMP_Text _playerListText;
    [SerializeField] TMP_InputField _teamIdField;
    [SerializeField] GameObject _startPanel;

    void Start()
    {
        _teamIdField.text = _session.Config.teamId;
        _session.OnStatusChanged += status =>
        {
            _statusText.text = status;
            RefreshPlayerList();
        };
        _session.OnPlayersChanged += RefreshPlayerList;
    }

    public void OnStartServerClicked()
    {
        if (!string.IsNullOrWhiteSpace(_teamIdField.text))
            _session.Config.teamId = _teamIdField.text.Trim();
        _session.StartServer();
        _startPanel.SetActive(false);
    }

    void RefreshPlayerList()
    {
        var builder = new StringBuilder("Players:\n");
        foreach (var player in _session.Registry.AllPlayers)
            builder.AppendLine($"  {player.PlayerName}  {(player.Connected ? "connected" : "DROPPED")}");
        _playerListText.text = builder.ToString();
    }
}
