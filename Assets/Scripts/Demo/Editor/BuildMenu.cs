using UnityEditor;
using UnityEngine;

/// <summary>One-click Android builds: same project, different entry scene per device.</summary>
public static class BuildMenu
{
    const string ClientScene = "Assets/Scenes/ClientDemoScene.unity";
    const string ServerScene = "Assets/Scenes/ServerDemoScene.unity";

    [MenuItem("VRNet/Build Headset Client APK")]
    public static void BuildClient() => Build(ClientScene, "Builds/VRNet-HeadsetClient.apk");

    [MenuItem("VRNet/Build Tablet Server APK")]
    public static void BuildServer() => Build(ServerScene, "Builds/VRNet-TabletServer.apk");

    static void Build(string scene, string outputPath)
    {
        var report = BuildPipeline.BuildPlayer(new[] { scene }, outputPath, BuildTarget.Android, BuildOptions.None);
        Debug.Log($"[VRNet] Build {report.summary.result}: {outputPath} ({report.summary.totalSize / (1024 * 1024)} MB)");
    }
}
