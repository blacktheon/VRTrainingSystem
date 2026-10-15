using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.XR.Management;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using Object = UnityEngine.Object;

/// <summary>Build-time overrides with restoration even when a build callback throws.</summary>
public static class TabletBuildSettingsScope
{
    public static bool IsTabletBuild { get; private set; }
    static bool running;

    public static void Run(bool tablet, Action build)
    {
        if (build == null) throw new ArgumentNullException(nameof(build));
        if (running || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Demo builds require an idle editor outside Play Mode.");

        var snapshot = new Snapshot();
        running = true;
        try
        {
            IsTabletBuild = tablet;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android,
                tablet ? "com.immrsiv.vrnet.server" : "com.immrsiv.vrnet.client");
            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
            if (tablet) snapshot.ApplyTablet();
            build();
        }
        finally
        {
            try { snapshot.Restore(); }
            finally
            {
                IsTabletBuild = false;
                running = false;
            }
        }
    }

    sealed class Snapshot
    {
        readonly string applicationId = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
        readonly AndroidSdkVersions minSdk = PlayerSettings.Android.minSdkVersion;
        readonly AndroidArchitecture architectures = PlayerSettings.Android.targetArchitectures;
        readonly AndroidApplicationEntry applicationEntry = PlayerSettings.Android.applicationEntry;
        readonly bool defaultGraphics = PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.Android);
        readonly GraphicsDeviceType[] graphics = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
        readonly bool graphicsJobs = PlayerSettings.graphicsJobs;
        readonly bool appBundle = EditorUserBuildSettings.buildAppBundle;
        readonly bool exportProject = EditorUserBuildSettings.exportAsGoogleAndroidProject;
        readonly MobileTextureSubtarget textureFormat = EditorUserBuildSettings.androidBuildSubtarget;
        readonly int versionCode = PlayerSettings.Android.bundleVersionCode;
        readonly string il2CppArgs = PlayerSettings.GetAdditionalIl2CppArgs();
        readonly string xrBootSettings = EditorUserBuildSettings.GetPlatformSettings("Android", "xr-boot-settings");
        readonly Object[] preloadedAssets = PlayerSettings.GetPreloadedAssets();
        readonly ObjectState playerSettings = new ObjectState(Resources.FindObjectsOfTypeAll<PlayerSettings>().Single());
        readonly XRGeneralSettings xr = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Android);
        readonly OpenXRSettings openXr = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
        readonly OVRProjectConfig meta = OVRProjectConfig.CachedProjectConfig;
        readonly List<ObjectState> objects = new List<ObjectState>();
        readonly Dictionary<string, byte[]> files = new Dictionary<string, byte[]>();
        readonly List<XRLoader> loaders;
        HashSet<BuildTargetGroup> metaPlatforms;
        bool metaSupportedAndroid;
        const string OvrBuildConfigPath = "Assets/Resources/OVRBuildConfig.asset";
        readonly bool hadOvrBuildConfig = File.Exists(OvrBuildConfigPath);

        public Snapshot()
        {
            CaptureFile("ProjectSettings/ProjectSettings.asset");
            Capture(xr);
            Capture(xr != null ? xr.Manager : null);
            loaders = xr != null && xr.Manager != null ? xr.Manager.activeLoaders.ToList() : null;
            Capture(openXr);
            if (openXr != null)
                foreach (var feature in openXr.GetFeatures()) Capture(feature);
            Capture(meta);
            Capture(AssetDatabase.LoadAssetAtPath<Object>(OvrBuildConfigPath));

            // Unity/URP build preparation changes these native settings and render assets.
            // Keep the capture bounded to the settings actually affected by this build.
            foreach (var path in new[] { "ProjectSettings/GraphicsSettings.asset", "ProjectSettings/TagManager.asset", "ProjectSettings/UnityConnectSettings.asset" })
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path)) Capture(asset);
            var renderSettingsPaths = new HashSet<string>();
            foreach (var type in new[] { "RenderPipelineAsset", "RenderPipelineGlobalSettings", "VolumeProfile" })
                foreach (var guid in AssetDatabase.FindAssets("t:" + type, new[] { "Assets" }))
                    renderSettingsPaths.Add(AssetDatabase.GUIDToAssetPath(guid));
            foreach (var path in renderSettingsPaths)
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path)) Capture(asset);

            // The SDK also edits runtime settings before a build; its post-build restoration
            // is not called when an earlier callback fails. Capture existing assets, including
            // settings already preloaded, without creating any new SDK settings assets.
            foreach (var asset in preloadedAssets)
                if (asset != null && asset is ScriptableObject) Capture(asset);
            var runtimeSettingsGuids = AssetDatabase.FindAssets("t:OVRRuntimeSettings", new[] { "Assets" })
                .Concat(AssetDatabase.FindAssets("t:RuntimeSettings", new[] { "Assets" })).Distinct();
            foreach (var guid in runtimeSettingsGuids)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(assetPath))
                {
                    var typeName = asset != null ? asset.GetType().FullName : string.Empty;
                    if (typeName == "OVRRuntimeSettings" ||
                        typeName == "Meta.XR.ImmersiveDebugger.RuntimeSettings" ||
                        typeName == "Meta.XR.ImmersiveDebugger.DevAgent.RuntimeSettings") Capture(asset);
                }
            }
        }

        void Capture(Object asset)
        {
            if (asset == null || objects.Any(x => x.Asset == asset)) return;
            objects.Add(new ObjectState(asset));
            CaptureFile(AssetDatabase.GetAssetPath(asset));
        }

        void CaptureFile(string path)
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path) && !files.ContainsKey(path))
                files.Add(path, File.ReadAllBytes(path));
        }

        public void ApplyTablet()
        {
            // Clear any XR boot flags left by a prior build before XR Management runs.
            EditorUserBuildSettings.SetPlatformSettings("Android", "xr-boot-settings", string.Empty);
            // This installed Meta validator runs even without an XR loader. Temporarily
            // opt Android out of its Quest-only validation without changing user preferences.
            var setup = typeof(OVRProjectConfig).Assembly.GetType("OVRProjectSetup");
            var platforms = setup?.GetField("SupportedPlatforms", BindingFlags.NonPublic | BindingFlags.Static);
            metaPlatforms = platforms?.GetValue(null) as HashSet<BuildTargetGroup>;
            if (metaPlatforms == null)
                throw new BuildFailedException("The installed Meta SDK validator changed; tablet build isolation must be updated.");
            metaSupportedAndroid = metaPlatforms.Contains(BuildTargetGroup.Android);
            metaPlatforms.Remove(BuildTargetGroup.Android);

            if (xr != null)
            {
                xr.InitManagerOnStart = false;
                EditorUtility.SetDirty(xr);
                if (xr.Manager != null)
                {
                    xr.Manager.automaticLoading = false;
                    xr.Manager.automaticRunning = false;
                    if (!xr.Manager.TrySetLoaders(new List<XRLoader>()))
                        throw new BuildFailedException("Unable to disable Android XR loaders for the tablet build.");
                    EditorUtility.SetDirty(xr.Manager);
                }
            }

            if (openXr != null)
            {
                foreach (var feature in openXr.GetFeatures())
                {
                    if (feature == null) continue;
                    // The public setter can be vetoed by the OpenXR inspector's required-
                    // feature UI. These temporary overrides must disable every Android feature.
                    var serialized = new SerializedObject(feature);
                    var enabled = serialized.FindProperty("m_enabled");
                    if (enabled == null) throw new BuildFailedException("OpenXR feature serialization changed; cannot isolate tablet settings.");
                    enabled.boolValue = false;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            if (meta != null)
            {
                meta.targetDeviceTypes.Clear();
                meta.systemSplashScreen = null;
                meta.horizonOsSdkDisabled = true;
                meta.enableIL2CPPLTO = false;
                EditorUtility.SetDirty(meta);
            }

            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.GameActivity;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.graphicsJobs = false;
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ETC2;
        }

        public void Restore()
        {
            // Attempt every restoration even if a single asset became unavailable.
            var errors = new List<Exception>();
            void Attempt(Action action)
            {
                try { action(); }
                catch (Exception error) { errors.Add(error); }
            }

            if (metaPlatforms != null)
                Attempt(() => { if (metaSupportedAndroid) metaPlatforms.Add(BuildTargetGroup.Android); else metaPlatforms.Remove(BuildTargetGroup.Android); });
            if (loaders != null && xr != null && xr.Manager != null)
                Attempt(() => { if (!xr.Manager.TrySetLoaders(loaders)) throw new InvalidOperationException("Unable to restore Android XR loaders."); });
            foreach (var state in objects) Attempt(state.Restore);
            Attempt(() => PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, applicationId));
            Attempt(() => PlayerSettings.Android.minSdkVersion = minSdk);
            Attempt(() => PlayerSettings.Android.targetArchitectures = architectures);
            Attempt(() => PlayerSettings.Android.applicationEntry = applicationEntry);
            Attempt(() => PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, graphics));
            Attempt(() => PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, defaultGraphics));
            Attempt(() => PlayerSettings.graphicsJobs = graphicsJobs);
            Attempt(() => PlayerSettings.Android.bundleVersionCode = versionCode);
            Attempt(() => PlayerSettings.SetAdditionalIl2CppArgs(il2CppArgs));
            Attempt(() => PlayerSettings.SetPreloadedAssets(preloadedAssets));
            Attempt(() => EditorUserBuildSettings.buildAppBundle = appBundle);
            Attempt(() => EditorUserBuildSettings.exportAsGoogleAndroidProject = exportProject);
            Attempt(() => EditorUserBuildSettings.androidBuildSubtarget = textureFormat);
            Attempt(() => EditorUserBuildSettings.SetPlatformSettings("Android", "xr-boot-settings", xrBootSettings));
            // Setting the application ID also flips an undocumented serialized override
            // flag. Restore the complete native object last, after all public setters.
            Attempt(playerSettings.Restore);
            if (!hadOvrBuildConfig && File.Exists(OvrBuildConfigPath))
                Attempt(() =>
                {
                    OVRConfig.Instance = null;
                    if (!AssetDatabase.DeleteAsset(OvrBuildConfigPath))
                        throw new IOException("Unable to remove the SDK build-only OVRBuildConfig asset.");
                });

            // BuildPipeline and SDK callbacks may have saved temporary asset values.
            // Restore the original bytes as well as the live objects, including dirty state.
            foreach (var file in files)
                Attempt(() => { if (!File.Exists(file.Key) || !File.ReadAllBytes(file.Key).SequenceEqual(file.Value)) File.WriteAllBytes(file.Key, file.Value); });
            if (errors.Count > 0) throw new AggregateException("Some pre-build project settings could not be restored.", errors);
        }
    }

    sealed class ObjectState
    {
        public Object Asset { get; private set; }
        readonly string json;
        readonly bool dirty;
        readonly string path;
        readonly string assetName;
        readonly Type assetType;
        readonly string guid;
        readonly long localId;
        readonly bool hasPersistentId;
        readonly int instanceId;

        public ObjectState(Object asset)
        {
            Asset = asset;
            path = AssetDatabase.GetAssetPath(asset);
            assetName = asset.name;
            assetType = asset.GetType();
            instanceId = asset.GetInstanceID();
            hasPersistentId = AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out guid, out localId);
            json = EditorJsonUtility.ToJson(asset);
            dirty = EditorUtility.IsDirty(asset);
        }

        public void Restore()
        {
            try
            {
                if (Asset == null)
                {
                    Asset = ResolveReloadedAsset();
                    Debug.Log($"[VRNet] Restoring reloaded build settings asset: {Description}");
                }
                EditorJsonUtility.FromJsonOverwrite(json, Asset);
                if (dirty) EditorUtility.SetDirty(Asset);
                else EditorUtility.ClearDirty(Asset);
            }
            catch (Exception error)
            {
                throw new InvalidOperationException($"Unable to restore build settings asset: {Description}", error);
            }
        }

        string Description => $"'{assetName}', type={assetType.FullName}, path='{path}', guid='{guid}', localId={localId}, originalInstanceId={instanceId}";

        Object ResolveReloadedAsset()
        {
            // Player builds can unload and reload persistent settings. A Unity Object
            // reference then compares equal to null although the asset still exists.
            // Names are not identities: multiple subassets can have the same name/type.
            var currentPath = hasPersistentId ? AssetDatabase.GUIDToAssetPath(guid) : path;
            if (string.IsNullOrEmpty(currentPath)) currentPath = path;
            if (string.IsNullOrEmpty(currentPath))
                throw new InvalidOperationException("The destroyed object has no persistent asset path.");
            var matches = AssetDatabase.LoadAllAssetsAtPath(currentPath)
                .Where(candidate => candidate != null && candidate.GetType() == assetType)
                .Where(candidate => !hasPersistentId ||
                    (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(candidate, out string candidateGuid, out long candidateId) &&
                     candidateGuid == guid && candidateId == localId)).ToArray();
            if (matches.Length != 1)
                throw new InvalidOperationException($"Expected one persistent asset at '{currentPath}', found {matches.Length} matching identities.");
            return matches[0];
        }
    }
}
