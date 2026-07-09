using DSTA.VRNet.Core;
using TMPro;
using UnityEngine;

/// <summary>Headset connect UI: auto-join, manual IP fallback, live status line.</summary>
public class DemoClientUI : MonoBehaviour
{
    [SerializeField] VRNetSession _session;
    [SerializeField] TMP_Text _statusText;
    [SerializeField] TMP_InputField _manualIpField;
    [SerializeField] GameObject _connectPanel;

    void Start()
    {
        _session.OnStatusChanged += status => _statusText.text = status;
        _session.OnConnected += () => _connectPanel.SetActive(false);
        _session.OnDisconnected += () => _connectPanel.SetActive(true);
    }

    public void OnJoinAutoClicked() => _session.StartClientAuto();
    public void OnJoinManualClicked() => _session.StartClient(_manualIpField.text);
}
