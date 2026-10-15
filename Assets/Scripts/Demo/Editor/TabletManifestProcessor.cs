using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
#if UNITY_ANDROID
using UnityEditor.Android;
#endif

/// <summary>Removes headset-only declarations from generated tablet output, never the source manifest.</summary>
public sealed class TabletManifestProcessor
#if UNITY_ANDROID
    : IPostGenerateGradleAndroidProject
#endif
{
    static readonly XNamespace Android = "http://schemas.android.com/apk/res/android";
    static readonly string[] HeadsetLibraries =
    {
        "libossdk.oculus.so", "libopenxr.so", "libopenxr_loader.so", "libOVRPlugin.so"
    };

    // Meta's OVRGradleGeneration runs at 99999, including when all XR loaders are disabled.
    public int callbackOrder => int.MaxValue;

    public void OnPostGenerateGradleAndroidProject(string path)
    {
        if (!TabletBuildSettingsScope.IsTabletBuild) return;

        var manifests = new[]
        {
            Path.Combine(path, "src", "main", "AndroidManifest.xml"),
            Path.Combine(path, "..", "launcher", "src", "main", "AndroidManifest.xml")
        };
        foreach (var manifest in manifests)
        {
            if (File.Exists(manifest))
                File.WriteAllText(manifest, TransformForTablet(File.ReadAllText(manifest)));
        }
        var gradle = Path.Combine(path, "build.gradle");
        if (File.Exists(gradle)) File.WriteAllText(gradle, TransformGradleForTablet(File.ReadAllText(gradle)));
    }

    /// <summary>Pure transformation shared by the Gradle callback and manifest regression tests.</summary>
    public static string TransformForTablet(string xml)
    {
        var document = XDocument.Parse(xml);
        foreach (var element in document.Descendants().ToArray())
        {
            var name = (string)element.Attribute(Android + "name") ?? string.Empty;
            var tag = element.Name.LocalName;
            if (tag == "uses-horizonos-sdk" || tag == "uses-horizonos-supplement" ||
                (tag == "uses-feature" && (IsHeadsetName(name) || name.StartsWith("android.hardware.vulkan.", StringComparison.Ordinal))) ||
                ((tag == "uses-permission" || tag == "meta-data" || tag == "category") && IsHeadsetName(name)) ||
                (tag == "uses-native-library" && HeadsetLibraries.Contains(name)))
            {
                element.Remove();
                continue;
            }

            if (tag == "activity" || tag == "activity-alias")
            {
                element.Attribute(Android + "enableVrMode")?.Remove();
                if (name.StartsWith("com.unity3d.player.", StringComparison.Ordinal))
                    element.Attribute(Android + "excludeFromRecents")?.Remove();
            }
        }
        return document.ToString();
    }

    /// <summary>Excludes the headset profiler AAR from the generated tablet project only.</summary>
    public static string TransformGradleForTablet(string gradle)
    {
        // AGP 9 merges uses-native-library as an unkeyed node: a tools:node=remove
        // declaration can itself survive with required=true. Exclude the source AAR
        // that requires libossdk.oculus.so instead. Installed package files stay intact.
        return Regex.Replace(gradle,
            @"^[\t ]*implementation\s*\(\s*name\s*:\s*['""]RuntimeOptimizer_Plugin['""]\s*,\s*ext\s*:\s*['""]aar['""]\s*\)[\t ]*;?[\t ]*\r?\n?",
            string.Empty, RegexOptions.Multiline);
    }

    static bool IsHeadsetName(string name)
    {
        return name.StartsWith("com.oculus.", StringComparison.Ordinal) ||
               name.StartsWith("oculus.", StringComparison.Ordinal) ||
               name.StartsWith("com.meta.xr.", StringComparison.Ordinal) ||
               name.StartsWith("org.khronos.openxr.", StringComparison.Ordinal) ||
               name.StartsWith("android.hardware.vr.", StringComparison.Ordinal) ||
               name.StartsWith("android.software.vr.", StringComparison.Ordinal) ||
               name.StartsWith("android.hardware.xr.", StringComparison.Ordinal) ||
               name.StartsWith("android.software.xr.", StringComparison.Ordinal);
    }
}
