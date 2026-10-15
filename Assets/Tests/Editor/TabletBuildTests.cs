using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Xml.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine.Rendering;

public class TabletBuildTests
{
    static readonly XNamespace Android = "http://schemas.android.com/apk/res/android";

    // Models the project's override after the Meta and OpenXR Gradle callbacks have run.
    const string QuestManifest = @"<manifest xmlns:android='http://schemas.android.com/apk/res/android'
        xmlns:tools='http://schemas.android.com/tools' xmlns:horizonos='http://schemas.horizonos/sdk' package='com.example.server'>
      <uses-feature android:name='android.hardware.vr.headtracking' android:version='1' android:required='true'/>
      <uses-feature android:name='android.software.vr.mode' android:required='true'/>
      <uses-feature android:name='oculus.software.handtracking' android:required='false'/>
      <uses-feature android:name='com.oculus.feature.PASSTHROUGH' android:required='true'/>
      <uses-feature android:name='android.hardware.vulkan.level' android:version='1' android:required='true'/>
      <uses-feature android:glEsVersion='0x00030000' android:required='true'/>
      <uses-permission android:name='com.oculus.permission.HAND_TRACKING'/>
      <uses-permission android:name='android.permission.INTERNET'/>
      <uses-permission android:name='android.permission.ACCESS_NETWORK_STATE'/>
      <uses-permission android:name='android.permission.CHANGE_WIFI_MULTICAST_STATE'/>
      <horizonos:uses-horizonos-sdk horizonos:minSdkVersion='60' horizonos:targetSdkVersion='205'/>
      <horizonos:uses-horizonos-supplement name='horizonos-supplement-hzplatformclientcore' minSupplementVersion='1' targetSupplementVersion='1'/>
      <application android:allowBackup='false' android:label='@string/app_name'>
        <uses-native-library android:name='libopenxr.so' android:required='true'/>
        <meta-data android:name='com.oculus.supportedDevices' android:value='quest2|quest3' tools:replace='android:value'/>
        <meta-data android:name='com.oculus.ossplash.background' android:value='black'/>
        <meta-data android:name='unityplayer.SkipPermissionsDialog' android:value='false'/>
        <activity android:name='com.unity3d.player.UnityPlayerGameActivity' android:exported='true'
            android:theme='@style/Theme.AppCompat.DayNight.NoActionBar' android:excludeFromRecents='true'
            android:enableVrMode='com.example.VrService' android:launchMode='singleTask'>
          <intent-filter>
            <action android:name='android.intent.action.MAIN'/>
            <category android:name='android.intent.category.LAUNCHER'/>
            <category android:name='com.oculus.intent.category.VR'/>
          </intent-filter>
          <meta-data android:name='com.oculus.vr.focusaware' android:value='true'/>
          <meta-data android:name='unityplayer.UnityActivity' android:value='true'/>
        </activity>
        <activity android:name='com.example.HelpActivity' android:exported='false'/>
        <provider android:name='androidx.core.content.FileProvider' android:authorities='com.example.files' android:exported='false'/>
      </application>
    </manifest>";

    [Test]
    public void TabletManifest_RemovesHeadsetRequirementsWithoutRemovingNetworkPermissions()
    {
        var manifest = XDocument.Parse(Transform(QuestManifest));
        var features = manifest.Descendants("uses-feature").ToArray();
        Assert.That(features.Any(x => ((string)x.Attribute(Android + "name") ?? "").Contains("oculus")), Is.False);
        Assert.That(features.Any(x => ((string)x.Attribute(Android + "name") ?? "").Contains("vr.")), Is.False);
        Assert.That(features.Any(x => ((string)x.Attribute(Android + "name") ?? "").Contains("vulkan")), Is.False);
        Assert.That(features.Single().Attribute(Android + "glEsVersion")?.Value, Is.EqualTo("0x00030000"));
        Assert.That(manifest.Descendants().Any(x => x.Name.LocalName == "uses-horizonos-sdk"), Is.False);
        Assert.That(manifest.Descendants().Any(x => x.Name.LocalName == "uses-horizonos-supplement"), Is.False);
        Assert.That(manifest.Descendants("uses-native-library"), Is.Empty);
        Assert.That(manifest.Descendants("uses-permission").Select(x => (string)x.Attribute(Android + "name")),
            Is.EquivalentTo(new[] { "android.permission.INTERNET", "android.permission.ACCESS_NETWORK_STATE", "android.permission.CHANGE_WIFI_MULTICAST_STATE" }));
    }

    [Test]
    public void TabletManifest_KeepsOrdinaryLauncherAndUnityActivityWhileRemovingVrLaunchMode()
    {
        var manifest = XDocument.Parse(Transform(QuestManifest));
        var activity = manifest.Descendants("activity").First();
        Assert.That(activity.Attribute(Android + "name")?.Value, Is.EqualTo("com.unity3d.player.UnityPlayerGameActivity"));
        Assert.That(activity.Attribute(Android + "exported")?.Value, Is.EqualTo("true"));
        Assert.That(activity.Attribute(Android + "enableVrMode"), Is.Null);
        Assert.That(activity.Attribute(Android + "excludeFromRecents")?.Value, Is.Not.EqualTo("true"));
        Assert.That(activity.Descendants("category").Select(x => (string)x.Attribute(Android + "name")),
            Is.EquivalentTo(new[] { "android.intent.category.LAUNCHER" }));
        Assert.That(activity.Descendants("action").Single().Attribute(Android + "name")?.Value, Is.EqualTo("android.intent.action.MAIN"));
        Assert.That(manifest.Descendants("meta-data").Select(x => (string)x.Attribute(Android + "name")),
            Is.EquivalentTo(new[] { "unityplayer.SkipPermissionsDialog", "unityplayer.UnityActivity" }));
        Assert.That(manifest.Descendants("provider").Single().Attribute(Android + "authorities")?.Value, Is.EqualTo("com.example.files"));
        Assert.That(manifest.Descendants("activity").Last().Attribute(Android + "exported")?.Value, Is.EqualTo("false"));
    }

    [Test]
    public void TabletManifest_IsIdempotentAndHandlesUnprefixedHorizonTagFromUnity()
    {
        var input = QuestManifest.Replace("horizonos:uses-horizonos-sdk", "uses-horizonos-sdk");
        var once = Transform(input);
        var twice = Transform(once);
        Assert.That(XNode.DeepEquals(XDocument.Parse(once), XDocument.Parse(twice)), Is.True);
        Assert.That(XDocument.Parse(once).Descendants().Any(x => x.Name.LocalName == "uses-horizonos-sdk"), Is.False);
    }

    [Test]
    public void TabletManifest_DoesNotInventLauncherInLibraryManifestOrRemoveUnrelatedComponents()
    {
        const string library = @"<manifest xmlns:a='http://schemas.android.com/apk/res/android'>
          <uses-feature a:name='android.hardware.camera' a:required='false'/>
          <application><service a:name='com.example.SyncService' a:exported='false'/>
          <uses-native-library a:name='libexample.so' a:required='false'/></application></manifest>";
        Assert.That(XNode.DeepEquals(XDocument.Parse(library), XDocument.Parse(Transform(library))), Is.True);
    }

    [Test]
    public void TabletGradle_ExcludesOnlyTheHeadsetRuntimeOptimizerDependency()
    {
        const string before = "dependencies {\n    implementation fileTree(dir: 'libs', include: ['*.jar'])\n    implementation(name: 'RuntimeOptimizer_Plugin', ext:'aar')\n    implementation(name: 'unityopus', ext:'aar')\n    implementation(name: 'InteractionSdk', ext:'aar')\n    implementation(name: 'OVRPlugin', ext:'aar')\n}\n";
        const string after = "dependencies {\n    implementation fileTree(dir: 'libs', include: ['*.jar'])\n    implementation(name: 'unityopus', ext:'aar')\n    implementation(name: 'InteractionSdk', ext:'aar')\n    implementation(name: 'OVRPlugin', ext:'aar')\n}\n";
        var transformed = InvokeManifestMethod("TransformGradleForTablet", before);
        Assert.That(transformed, Is.EqualTo(after));
        Assert.That(InvokeManifestMethod("TransformGradleForTablet", transformed), Is.EqualTo(after));
    }

    [TestCase(false)]
    [TestCase(true)]
    // A cold ASTC/ETC2 switch imports the project's textures and SDK sample videos.
    [Timeout(900000)]
    public void TabletSettings_RestoresPlayerSettingsAfterSuccessOrFailure(bool failBuild)
    {
        var type = Type.GetType("TabletBuildSettingsScope, Assembly-CSharp-Editor");
        Assert.That(type, Is.Not.Null, "Tablet build isolation has not been implemented.");
        var method = type.GetMethod("Run", BindingFlags.Public | BindingFlags.Static);
        Assert.That(method, Is.Not.Null);
        var playerSettingsObject = UnityEngine.Resources.FindObjectsOfTypeAll<PlayerSettings>().Single();
        var playerSettings = new SerializedObject(playerSettingsObject);
        var overrideApplicationId = playerSettings.FindProperty("overrideDefaultApplicationIdentifier");
        var originalOverride = overrideApplicationId.boolValue;
        overrideApplicationId.boolValue = false;
        playerSettings.ApplyModifiedPropertiesWithoutUndo();
        var playerSettingsJson = EditorJsonUtility.ToJson(playerSettingsObject);
        var applicationId = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
        var minSdk = PlayerSettings.Android.minSdkVersion;
        var graphicsApis = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
        var defaultApis = PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.Android);
        var preloadedAssets = PlayerSettings.GetPreloadedAssets();
        var il2CppArgs = PlayerSettings.GetAdditionalIl2CppArgs();
        var versionCode = PlayerSettings.Android.bundleVersionCode;
        var textureFormat = EditorUserBuildSettings.androidBuildSubtarget;
        var xrBootSettings = EditorUserBuildSettings.GetPlatformSettings("Android", "xr-boot-settings");
        var entered = false;
        Action simulatedBuild = () =>
        {
            entered = true;
            Assert.That(PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android), Is.Not.EqualTo(applicationId));
            Assert.That(PlayerSettings.GetGraphicsAPIs(BuildTarget.Android), Has.Member(GraphicsDeviceType.OpenGLES3));
            Assert.That(PlayerSettings.GetGraphicsAPIs(BuildTarget.Android), Has.No.Member(GraphicsDeviceType.Vulkan));
            Assert.That(EditorUserBuildSettings.androidBuildSubtarget, Is.EqualTo(MobileTextureSubtarget.ETC2));
            // These real SDK callback side effects must be restored even if a later callback fails.
            PlayerSettings.Android.bundleVersionCode++;
            PlayerSettings.SetAdditionalIl2CppArgs("--test-build-side-effect");
            PlayerSettings.SetPreloadedAssets(Array.Empty<UnityEngine.Object>());
            EditorUserBuildSettings.SetPlatformSettings("Android", "xr-boot-settings", "test-pre-init:changed");
            if (failBuild) throw new InvalidOperationException("simulated build failure");
        };
        try
        {
            if (failBuild)
            {
                var error = Assert.Throws<TargetInvocationException>(() => method.Invoke(null, new object[] { true, simulatedBuild }));
                Assert.That(error.InnerException, Is.TypeOf<InvalidOperationException>());
            }
            else method.Invoke(null, new object[] { true, simulatedBuild });
        }
        finally
        {
            var restoredPlayerJson = EditorJsonUtility.ToJson(playerSettingsObject);
            playerSettings.Update();
            overrideApplicationId.boolValue = originalOverride;
            playerSettings.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(entered, Is.True);
            Assert.That(PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android), Is.EqualTo(applicationId));
            Assert.That(PlayerSettings.Android.minSdkVersion, Is.EqualTo(minSdk));
            Assert.That(PlayerSettings.GetGraphicsAPIs(BuildTarget.Android), Is.EqualTo(graphicsApis));
            Assert.That(PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.Android), Is.EqualTo(defaultApis));
            Assert.That(PlayerSettings.GetPreloadedAssets(), Is.EqualTo(preloadedAssets));
            Assert.That(PlayerSettings.GetAdditionalIl2CppArgs(), Is.EqualTo(il2CppArgs));
            Assert.That(PlayerSettings.Android.bundleVersionCode, Is.EqualTo(versionCode));
            Assert.That(EditorUserBuildSettings.androidBuildSubtarget, Is.EqualTo(textureFormat));
            var restoredBootSettings = EditorUserBuildSettings.GetPlatformSettings("Android", "xr-boot-settings");
            // Keep a failing isolation test from contaminating the open editor.
            EditorUserBuildSettings.SetPlatformSettings("Android", "xr-boot-settings", xrBootSettings);
            Assert.That(restoredBootSettings, Is.EqualTo(xrBootSettings));
            Assert.That(restoredPlayerJson == playerSettingsJson, Is.True,
                "The complete PlayerSettings state, including automatic application ID mode, must be restored.");
        }
    }

    [Test]
    public void TabletSettings_RestoresRenderAndNativeSettingsAfterCallbackFailure()
    {
        var method = Type.GetType("TabletBuildSettingsScope, Assembly-CSharp-Editor").GetMethod("Run");
        var paths = new List<string> { "ProjectSettings/GraphicsSettings.asset", "ProjectSettings/TagManager.asset", "ProjectSettings/UnityConnectSettings.asset" };
        foreach (var type in new[] { "RenderPipelineAsset", "RenderPipelineGlobalSettings", "VolumeProfile" })
            paths.AddRange(AssetDatabase.FindAssets("t:" + type, new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath));
        var assets = paths.Distinct().SelectMany(AssetDatabase.LoadAllAssetsAtPath).Where(x => x != null).Distinct().ToArray();
        var originals = assets.Select(EditorJsonUtility.ToJson).ToArray();
        var originalDirty = assets.Select(EditorUtility.IsDirty).ToArray();
        var observed = new string[assets.Length];
        Action failBuild = () =>
        {
            foreach (var asset in assets)
            {
                var serialized = new SerializedObject(asset);
                var name = serialized.FindProperty("m_Name");
                var enabled = serialized.FindProperty("m_Enabled");
                var tags = serialized.FindProperty("tags");
                var shaderLog = serialized.FindProperty("m_LogWhenShaderIsCompiled");
                if (name != null) name.stringValue += " build temporary";
                else if (enabled != null) enabled.boolValue = !enabled.boolValue;
                else if (tags != null) { tags.InsertArrayElementAtIndex(tags.arraySize); tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = "BuildScopeTemporaryTag"; }
                else if (shaderLog != null) shaderLog.boolValue = !shaderLog.boolValue;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            throw new InvalidOperationException("simulated build callback failure");
        };
        try
        {
            var error = Assert.Throws<TargetInvocationException>(() => method.Invoke(null, new object[] { true, failBuild }));
            Assert.That(error.InnerException, Is.TypeOf<InvalidOperationException>());
            observed = assets.Select(EditorJsonUtility.ToJson).ToArray();
        }
        finally
        {
            // A failed regression must not leave the user's live assets mutated.
            for (var i = 0; i < assets.Length; i++)
            {
                EditorJsonUtility.FromJsonOverwrite(originals[i], assets[i]);
                if (originalDirty[i]) EditorUtility.SetDirty(assets[i]); else EditorUtility.ClearDirty(assets[i]);
            }
        }
        for (var i = 0; i < assets.Length; i++)
            Assert.That(observed[i] == originals[i], Is.True, $"Build callback mutation was not restored for {AssetDatabase.GetAssetPath(assets[i])} ({assets[i].GetType().Name}).");
    }

    [Test]
    public void BuildSettings_RestoresReloadedSubassetsByLocalIdEvenWhenNamesMatch()
    {
        var sourcePath = AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("t:VolumeProfile", new[] { "Assets" }).First());
        var profileType = AssetDatabase.LoadMainAssetAtPath(sourcePath).GetType();
        var path = "Assets/Tests/Editor/TabletReloadRegression-" + Guid.NewGuid().ToString("N") + ".asset";
        var main = UnityEngine.ScriptableObject.CreateInstance(profileType);
        var first = UnityEngine.ScriptableObject.CreateInstance<DSTA.VRNet.Core.VRNetConfig>();
        var second = UnityEngine.ScriptableObject.CreateInstance<DSTA.VRNet.Core.VRNetConfig>();
        first.name = second.name = "Duplicate name";
        first.teamId = "original-first";
        second.teamId = "original-second";
        AssetDatabase.CreateAsset(main, path);
        AssetDatabase.AddObjectToAsset(first, path);
        AssetDatabase.AddObjectToAsset(second, path);
        AssetDatabase.SaveAssetIfDirty(main);
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(first, out string _, out long firstId);
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(second, out string _, out long secondId);
        var method = Type.GetType("TabletBuildSettingsScope, Assembly-CSharp-Editor").GetMethod("Run");
        try
        {
            Action simulatedBuild = () =>
            {
                first.teamId = second.teamId = "temporary-build-value";
                EditorUtility.SetDirty(first);
                EditorUtility.SetDirty(second);
                AssetDatabase.SaveAssetIfDirty(main);
                if (first != null) UnityEngine.Resources.UnloadAsset(first);
                if (second != null) UnityEngine.Resources.UnloadAsset(second);
                if (main != null) UnityEngine.Resources.UnloadAsset(main);
                AssetDatabase.LoadAllAssetsAtPath(path);
            };
            Assert.DoesNotThrow(() => method.Invoke(null, new object[] { false, simulatedBuild }));
            var restored = AssetDatabase.LoadAllAssetsAtPath(path).OfType<DSTA.VRNet.Core.VRNetConfig>().ToArray();
            Assert.That(restored.Length, Is.EqualTo(2));
            foreach (var config in restored)
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(config, out string _, out long id);
                Assert.That(id == firstId || id == secondId, Is.True);
                Assert.That(config.teamId, Is.EqualTo(id == firstId ? "original-first" : "original-second"));
            }
        }
        finally { AssetDatabase.DeleteAsset(path); }
    }

    static string Transform(string xml)
    {
        return InvokeManifestMethod("TransformForTablet", xml);
    }

    static string InvokeManifestMethod(string methodName, string xml)
    {
        var type = Type.GetType("TabletManifestProcessor, Assembly-CSharp-Editor");
        Assert.That(type, Is.Not.Null, "Tablet manifest transformation has not been implemented.");
        var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
        Assert.That(method, Is.Not.Null, $"Expected public static string {methodName}(string xml).");
        return (string)method.Invoke(null, new object[] { xml });
    }
}
