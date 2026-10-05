using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace GameFactory.Editor
{
    /// <summary>Per-game values the shared configurator needs.</summary>
    public sealed class GameProjectConfig
    {
        public string CompanyName;
        public string ProductName;
        public string ApplicationId;
        public string Version = "0.1.0";
        public int VersionCode = 1;
        public int TargetSdk = 36;
        public string[] Scenes;
        public Color32 ClearColor = new Color32(0x0E, 0x12, 0x30, 0xFF);
    }

    /// <summary>A game implements this so the shared build scripts can (re)apply its settings before every build.</summary>
    public interface IProjectSetup
    {
        GameProjectConfig Config { get; }
        /// <summary>Create or refresh generated assets (scenes, materials). Must be idempotent.</summary>
        void GenerateAssets();
    }

    /// <summary>
    /// Applies the factory's Android / URP settings (HANDOVER §5.2–5.3). Idempotent: safe to run before every build.
    /// </summary>
    public static class AndroidProjectConfigurator
    {
        public const string UrpAssetPath = "Assets/SharedAssets/Rendering/MobileURP.asset";
        public const string UrpRendererPath = "Assets/SharedAssets/Rendering/MobileURP_Renderer.asset";

        public static IProjectSetup FindGameSetup()
        {
            var types = TypeCache.GetTypesDerivedFrom<IProjectSetup>().Where(t => !t.IsAbstract).ToArray();
            if (types.Length != 1)
                throw new InvalidOperationException($"Expected exactly one IProjectSetup, found {types.Length}");
            return (IProjectSetup)Activator.CreateInstance(types[0]);
        }

        public static void ApplyAll()
        {
            var setup = FindGameSetup();
            Apply(setup.Config);
            setup.GenerateAssets();
            EditorBuildSettings.scenes = setup.Config.Scenes.Select(s => new EditorBuildSettingsScene(s, true)).ToArray();
            AssetDatabase.SaveAssets();
        }

        public static void Apply(GameProjectConfig c)
        {
            var android = NamedBuildTarget.Android;

            PlayerSettings.companyName = c.CompanyName;
            PlayerSettings.productName = c.ProductName;
            PlayerSettings.bundleVersion = c.Version;
            PlayerSettings.Android.bundleVersionCode = c.VersionCode;
            PlayerSettings.SetApplicationIdentifier(android, c.ApplicationId);

            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetIl2CppCodeGeneration(android, Il2CppCodeGeneration.OptimizeSpeed);
            PlayerSettings.SetManagedStrippingLevel(android, ManagedStrippingLevel.Low);
            PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.Activity;
            PlayerSettings.Android.minSdkVersion = LowestSupportedMinSdk();
            PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)c.TargetSdk;
            SetArchitectures(emulator: false);

            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.colorSpace = ColorSpace.Linear;

            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.Android.startInFullscreen = true;

            SetActiveInputHandlingBoth();
            EnsureUrp();
        }

        /// <summary>ARMv7 + ARM64 always; x86-64 (Chrome OS) only for emulator builds (HANDOVER §8.2).</summary>
        public static void SetArchitectures(bool emulator)
        {
            var arch = AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64;
            if (emulator) arch |= AndroidArchitecture.X86_64;
            PlayerSettings.Android.targetArchitectures = arch;
        }

        static AndroidSdkVersions LowestSupportedMinSdk() =>
            Enum.GetValues(typeof(AndroidSdkVersions)).Cast<AndroidSdkVersions>()
                .Where(v => (int)v >= 23).OrderBy(v => (int)v).First();

        /// <summary>"Both" (old Input Manager + Input System). There is no public API, so edit the asset.</summary>
        static void SetActiveInputHandlingBoth()
        {
            var asset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset").FirstOrDefault();
            if (asset == null) return;
            var so = new SerializedObject(asset);
            var prop = so.FindProperty("activeInputHandler");
            if (prop != null && prop.intValue != 2)
            {
                prop.intValue = 2;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        /// <summary>Mobile URP: no realtime shadows, no HDR, no MSAA (blob shadows are drawn by the game).</summary>
        static void EnsureUrp()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(UrpAssetPath));
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(UrpAssetPath);
            if (asset == null)
            {
                var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, UrpRendererPath);
                asset = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(asset, UrpAssetPath);
            }
            asset.supportsHDR = false;
            asset.msaaSampleCount = 1;
            asset.renderScale = 1f;
            asset.shadowDistance = 0f;
            asset.supportsCameraDepthTexture = false;
            asset.supportsCameraOpaqueTexture = false;
            EditorUtility.SetDirty(asset);

            GraphicsSettings.defaultRenderPipeline = asset;
            for (var i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = asset;
                QualitySettings.shadows = UnityEngine.ShadowQuality.Disable;
                QualitySettings.vSyncCount = 0;
            }
        }
    }
}
