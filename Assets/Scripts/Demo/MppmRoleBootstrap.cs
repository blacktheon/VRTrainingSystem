using System;
using System.Collections;
using System.Linq;
using DSTA.VRNet.Core;
using DSTA.VRNet.Discovery;
using Unity.Multiplayer.Playmode;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Integration-test bootstrap for Multiplayer Play Mode (MPPM): the same scene runs in
/// the main editor and every virtual player, so role selection happens at runtime via
/// <see cref="CurrentPlayer"/>. Main editor becomes the server; every virtual player
/// becomes a client. Virtual players tagged "WRONGTEAM" join with a mismatched teamId to
/// exercise connection-approval rejection; virtual players tagged "DROPPER" force a
/// self-disconnect after 12s to exercise ReconnectController's auto-rejoin.
/// </summary>
public class MppmRoleBootstrap : MonoBehaviour
{
    const string WrongTeamTag = "WRONGTEAM";
    const string DropperTag = "DROPPER";
    const string WrongTeamId = "TEAM-WRONG";
    const float DropAfterSeconds = 12f;

    IEnumerator Start()
    {
        // Let VRNetSession.Awake() run first so Instance is assigned.
        yield return null;

        var session = VRNetSession.Instance;
        if (session == null)
        {
            Debug.LogError("[MPPMTEST] VRNetSession.Instance is null - is the VRNetCore prefab in the scene?");
            yield break;
        }

        session.OnStatusChanged += s => Debug.Log($"[MPPMTEST] status: {s}");
        session.OnConnected += () => Debug.Log("[MPPMTEST] event OnConnected");
        session.OnDisconnected += () => Debug.Log("[MPPMTEST] event OnDisconnected");
        session.OnDiscoveryTimeout += () => Debug.Log("[MPPMTEST] event OnDiscoveryTimeout");
        session.OnPlayersChanged += () => Debug.Log(
            $"[MPPMTEST] roster: {session.Registry.ConnectedPlayers.Count()} connected / {session.Registry.AllPlayers.Count()} total " +
            $"[{string.Join(", ", session.Registry.AllPlayers.Select(p => $"{p.PlayerName}:{(p.Connected ? "up" : "down")}"))}]");

        bool isMainEditor = CurrentPlayer.IsMainEditor;
        string[] tags = CurrentPlayer.ReadOnlyTags();
        Debug.Log($"[MPPMTEST] bootstrap: isMainEditor={isMainEditor} tags=[{string.Join(",", tags)}]");

        if (isMainEditor)
        {
            Debug.Log("[MPPMTEST] role=SERVER");
            session.StartServer();
            yield break;
        }

        bool wrongTeam = tags.Contains(WrongTeamTag);
        bool dropper = tags.Contains(DropperTag);

        if (wrongTeam)
        {
            session.Config.teamId = WrongTeamId;
            Debug.Log($"[MPPMTEST] role=CLIENT WRONGTEAM: runtime teamId overridden to '{WrongTeamId}'");
        }
        else
        {
            Debug.Log("[MPPMTEST] role=CLIENT");
        }

        // MPPM accommodation: PlayerIdentity persists the player GUID in PlayerPrefs,
        // which all editor instances on this machine share (same registry key), so every
        // virtual player would present the same identity and the server would treat the
        // second join as the first client "rejoining". Real headsets each have their own
        // storage; only MPPM needs this per-instance override.
        var uniqueGuid = Guid.NewGuid().ToString();
        session.PlayerGuidOverride = uniqueGuid;
        session.PlayerName = $"{SystemInfo.deviceName}-{(dropper ? "DROPPER" : "VP")}-{uniqueGuid.Substring(0, 4)}";
        Debug.Log($"[MPPMTEST] identity: name={session.PlayerName} guid={uniqueGuid}");

        var rigGo = new GameObject("MppmFakeRig");
        rigGo.AddComponent<DemoFakeRig>();

        // Extra visibility into discovery for the integration test log.
        var listener = session.GetComponent<DiscoveryListener>();
        listener.OnServerFound += (address, message) =>
            Debug.Log($"[MPPMTEST] discovery hit from {address} (team {message.teamId}, port {message.port})");

        // Discovery deliberately hides other teams. Connect directly to exercise
        // approval rejection instead of merely timing out in discovery.
        if (wrongTeam) session.StartClient("127.0.0.1");
        else session.StartClientAuto();

        if (dropper)
        {
            Debug.Log($"[MPPMTEST] role=CLIENT DROPPER: scheduling self-drop in {DropAfterSeconds}s to exercise reconnect");
            StartCoroutine(DropAfterDelay(session));
        }
    }

    IEnumerator DropAfterDelay(VRNetSession session)
    {
        yield return new WaitForSeconds(DropAfterSeconds);
        Debug.Log("[MPPMTEST] DROPPER: forcing NetworkManager.Shutdown() to simulate connection loss");
        NetworkManager.Singleton.Shutdown();
    }
}
