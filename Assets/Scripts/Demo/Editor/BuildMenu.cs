using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System.IO;

/// <summary>One-click Android builds: same project, different entry scene per device.</summary>
public static class BuildMenu
{
    const string ClientScene = "Assets/Scenes/ClientDemoScene.unity";
    const string ServerScene = "Assets/Scenes/ServerDemoScene.unity";

    [MenuItem("VRNet/Build Headset Client APK")]
    public static void BuildClient() => Build(ClientScene, "Builds/VRNet-HeadsetClient.apk", false);

    [MenuItem("VRNet/Build Tablet Server APK")]
    public static void BuildServer() => Build(ServerScene, "Builds/VRNet-TabletServer.apk", true);

    static void Build(string scene, string outputPath, bool tablet)
    {
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            throw new BuildFailedException("Switch the active build platform to Android before using the VRNet APK menus.");
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
        EditorApplication.LockReloadAssemblies();
        try
        {
            TabletBuildSettingsScope.Run(tablet, () =>
            {
                var report = BuildPipeline.BuildPlayer(new[] { scene }, outputPath, BuildTarget.Android, BuildOptions.None);
                Debug.Log($"[VRNet] Build {report.summary.result}: {outputPath} ({report.summary.totalSize / (1024 * 1024)} MB)");
                if (report.summary.result != BuildResult.Succeeded)
                    throw new BuildFailedException($"VRNet APK build {report.summary.result}: {outputPath}");
            });
        }
        finally { EditorApplication.UnlockReloadAssemblies(); }
    }
}
