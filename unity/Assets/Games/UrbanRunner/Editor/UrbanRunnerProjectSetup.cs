using System.IO;
using GameFactory.Core.Bridge;
using GameFactory.Editor;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UrbanRunner.Game;

namespace UrbanRunner.Editor
{
    /// <summary>Urban Runner's values for the shared configurator, plus generated assets and scenes.</summary>
    public sealed class UrbanRunnerProjectSetup : IProjectSetup
    {
        public const string RunScenePath = "Assets/Games/UrbanRunner/Scenes/Run.unity";
        public const string GameAssetsPath = "Assets/Games/UrbanRunner/Data/GameAssets.asset";
        const string MaterialDir = "Assets/SharedAssets/Materials";
        const string TmpDefaultFont = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

        public GameProjectConfig Config { get; } = new GameProjectConfig
        {
            CompanyName = "GameFactory",
            ProductName = "Urban Runner",
            ApplicationId = "com.gamefactory.urbanrunner", // placeholder until owner task 4
            Scenes = new[] { RunScenePath },
        };

        [MenuItem("GameFactory/Apply Project Settings")]
        static void MenuApply() => AndroidProjectConfigurator.ApplyAll();

        [MenuItem("GameFactory/Urban Runner/Rebuild Run Scene")]
        static void MenuRebuildScene()
        {
            CreateRunScene(EnsureGameAssets(), ChunkCatalog.EnsureAssets());
        }

        public void GenerateAssets()
        {
            EnsureTmpEssentials();
            var assets = EnsureGameAssets();
            var lib = ChunkCatalog.EnsureAssets();
            if (!File.Exists(RunScenePath)) CreateRunScene(assets, lib);
            EnsureRunSceneReferences();
        }

        /// <summary>Re-wires GameRoot's asset references if any are missing (self-healing, idempotent).</summary>
        static void EnsureRunSceneReferences()
        {
            var scene = EditorSceneManager.OpenScene(RunScenePath, OpenSceneMode.Single);
            var root = Object.FindAnyObjectByType<GameRoot>();
            if (root == null) throw new System.Exception("Run scene has no GameRoot");
            var so = new SerializedObject(root);
            var changed = false;
            void Fix(string prop, Object value)
            {
                var p = so.FindProperty(prop);
                if (p.objectReferenceValue == null) { p.objectReferenceValue = value; changed = true; }
            }
            Fix("assets", AssetDatabase.LoadAssetAtPath<GameAssets>(GameAssetsPath));
            Fix("chunks", AssetDatabase.LoadAssetAtPath<Sim.ChunkLibrary>(ChunkCatalog.LibraryPath));
            Fix("mainCamera", Camera.main);
            if (!changed) return;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene);
            Debug.Log("UR_GAME Run scene references repaired");
        }

        /// <summary>
        /// TMP Essential Resources are committed under Assets/TextMesh Pro. Batchmode can't import a .unitypackage
        /// synchronously, so restore them with tools/lib/extract_unitypackage.py if they are ever deleted.
        /// </summary>
        static void EnsureTmpEssentials()
        {
            if (File.Exists(TmpDefaultFont)) return;
            throw new System.Exception("TMP Essential Resources missing. Run: python3 tools/lib/extract_unitypackage.py " +
                "\"unity/Library/PackageCache/com.unity.ugui@*/Package Resources/TMP Essential Resources.unitypackage\" unity");
        }

        static Material Mat(string name, string shader, Color32 color)
        {
            Directory.CreateDirectory(MaterialDir);
            var path = $"{MaterialDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find(shader));
                AssetDatabase.CreateAsset(mat, path);
                mat.SetColor("_BaseColor", color);
                mat.enableInstancing = true;
                EditorUtility.SetDirty(mat);
            }
            return mat;
        }

        const string Lit = "Universal Render Pipeline/Simple Lit";
        const string Unlit = "Universal Render Pipeline/Unlit";

        public static GameAssets EnsureGameAssets()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(GameAssetsPath));
            var a = AssetDatabase.LoadAssetAtPath<GameAssets>(GameAssetsPath);
            if (a == null)
            {
                a = ScriptableObject.CreateInstance<GameAssets>();
                AssetDatabase.CreateAsset(a, GameAssetsPath);
            }
            // Palette tokens (HANDOVER §3.1)
            a.night = Mat("Night", Lit, new Color32(0x0E, 0x12, 0x30, 0xFF));
            a.surface = Mat("Surface", Lit, new Color32(0x1A, 0x20, 0x50, 0xFF));
            a.magenta = Mat("NeonMagenta", Lit, new Color32(0xFF, 0x3D, 0xA6, 0xFF));
            a.cyan = Mat("NeonCyan", Lit, new Color32(0x2E, 0xE6, 0xFF, 0xFF));
            a.amber = Mat("LanternAmber", Lit, new Color32(0xFF, 0xB5, 0x47, 0xFF));
            a.text = Mat("Text", Lit, new Color32(0xF4, 0xF6, 0xFF, 0xFF));
            a.success = Mat("Success", Lit, new Color32(0x3D, 0xFF, 0xA2, 0xFF));
            a.danger = Mat("Danger", Lit, new Color32(0xFF, 0x5A, 0x5A, 0xFF));
            // World
            a.ground = Mat("Ground", Lit, new Color32(0x22, 0x27, 0x4A, 0xFF));
            a.laneLine = Mat("LaneLine", Unlit, new Color32(0x5A, 0x63, 0xA8, 0xFF));
            a.vanBody = Mat("VanBody", Lit, new Color32(0xB8, 0xC4, 0xE8, 0xFF));
            a.glass = Mat("Glass", Lit, new Color32(0x10, 0x16, 0x3A, 0xFF));
            a.wood = Mat("Wood", Lit, new Color32(0xB0, 0x6A, 0x2E, 0xFF));
            // Glow
            a.sparkGlow = Mat("SparkGlow", Unlit, new Color32(0x2E, 0xE6, 0xFF, 0xFF));
            a.magnetGlow = Mat("MagnetGlow", Unlit, new Color32(0xFF, 0x3D, 0xA6, 0xFF));
            a.shieldGlow = Mat("ShieldGlow", Unlit, new Color32(0x3D, 0xFF, 0xA2, 0xFF));
            a.doubleGlow = Mat("DoubleGlow", Unlit, new Color32(0xFF, 0xB5, 0x47, 0xFF));
            if (a.font == null) a.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TmpDefaultFont);
            EditorUtility.SetDirty(a);
            AssetDatabase.SaveAssets();
            return a;
        }

        static void CreateRunScene(GameAssets assets, Sim.ChunkLibrary lib)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(0x0E, 0x12, 0x30, 0xFF);
            cam.fieldOfView = 62f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 180f;

            var lightGo = new GameObject("Moonlight");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.None;
            light.color = new Color32(0xC8, 0xD0, 0xFF, 0xFF);
            light.intensity = 1.1f;
            lightGo.transform.rotation = Quaternion.Euler(45, -25, 0);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color32(0x3A, 0x40, 0x78, 0xFF);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color32(0x0E, 0x12, 0x30, 0xFF);
            RenderSettings.fogStartDistance = 70f;
            RenderSettings.fogEndDistance = 170f;

            new GameObject(FlutterBridge.ObjectName).AddComponent<FlutterBridge>();

            var rootGo = new GameObject("GameRoot");
            var root = rootGo.AddComponent<GameRoot>();
            var so = new SerializedObject(root);
            so.FindProperty("assets").objectReferenceValue = assets;
            so.FindProperty("chunks").objectReferenceValue = lib;
            so.FindProperty("mainCamera").objectReferenceValue = cam;
            so.ApplyModifiedPropertiesWithoutUndo();
            rootGo.AddComponent<StandaloneHost>();

            Directory.CreateDirectory(Path.GetDirectoryName(RunScenePath));
            EditorSceneManager.SaveScene(scene, RunScenePath);
        }
    }
}
