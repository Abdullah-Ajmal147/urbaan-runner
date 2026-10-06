using System.IO;
using System.Linq;
using GameFactory.Core.Bridge;
using GameFactory.Editor;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UrbanRunner.Game;

namespace UrbanRunner.Editor
{
    /// <summary>Urban Runner's values for the shared configurator, plus generated assets and the Run scene.</summary>
    public sealed class UrbanRunnerProjectSetup : IProjectSetup
    {
        public const string RunScenePath = "Assets/Games/UrbanRunner/Scenes/Run.unity";
        public const string GameAssetsPath = "Assets/Games/UrbanRunner/Data/GameAssets.asset";
        public const string PostFxPath = "Assets/Games/UrbanRunner/Data/PostFX.asset";
        const string MaterialDir = "Assets/SharedAssets/Materials";
        const string FontDir = "Assets/SharedAssets/Fonts";
        const string AudioDir = "Assets/Games/UrbanRunner/Audio";
        const string EnvDir = "Assets/Games/UrbanRunner/Art/Environment/Kenney";
        const string TmpDefaultFont = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

        const string ToonShader = "GameFactory/ToonLit";
        const string ToonOutlineShader = "GameFactory/ToonLitOutline";
        const string GlowShader = "GameFactory/AdditiveGlow";
        const string UnlitShader = "Universal Render Pipeline/Unlit";

        public GameProjectConfig Config { get; } = new GameProjectConfig
        {
            CompanyName = "GameFactory",
            ProductName = "Urban Runner",
            ApplicationId = "com.gamefactory.urbanrunner", // placeholder until owner task 4
            Scenes = new[] { RunScenePath },
        };

        [MenuItem("GameFactory/Apply Project Settings")]
        static void MenuApply() => AndroidProjectConfigurator.ApplyAll();

        public void GenerateAssets()
        {
            EnsureTmpEssentials();
            CourierRenders.EnsureMaterials();
            var assets = EnsureGameAssets();
            var lib = ChunkCatalog.EnsureAssets();
            CreateRunScene(assets, lib, EnsurePostFx());
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

        // ---------- materials ----------

        static Material Load(string name, string shader)
        {
            Directory.CreateDirectory(MaterialDir);
            var path = $"{MaterialDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find(shader));
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (mat.shader.name != shader) mat.shader = Shader.Find(shader);
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Material Toon(string name, Color32 color, float outline = 0f, float rim = 0.25f, Color? emission = null, Texture tex = null)
        {
            // Only outlined materials pay for the second (outline) pass.
            var m = Load(name, outline > 0f ? ToonOutlineShader : ToonShader);
            m.SetColor("_BaseColor", color);
            m.SetFloat("_OutlineWidth", outline);
            m.SetFloat("_RimStrength", rim);
            m.SetColor("_EmissionColor", emission ?? Color.black);
            if (tex != null) m.SetTexture("_BaseMap", tex);
            return m;
        }

        static Material Glow(string name, Color32 color, float intensity, bool streak = false, float softness = 2f)
        {
            var m = Load(name, GlowShader);
            m.SetColor("_Color", (Color)color * intensity);
            m.SetFloat("_Shape", streak ? 1 : 0);
            m.SetFloat("_Softness", softness);
            return m;
        }

        static Material Unlit(string name, Color color)
        {
            var m = Load(name, UnlitShader);
            m.SetColor("_BaseColor", color);
            return m;
        }

        static TMP_FontAsset FontAsset(string ttf, string assetName)
        {
            var path = $"{FontDir}/{assetName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (existing != null) return existing;
            var font = AssetDatabase.LoadAssetAtPath<Font>($"{FontDir}/{ttf}");
            var fa = TMP_FontAsset.CreateFontAsset(font, 90, 9, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            fa.name = assetName;
            AssetDatabase.CreateAsset(fa, path);
            fa.atlasTextures[0].name = assetName + " Atlas";
            AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
            fa.material.name = assetName + " Material";
            AssetDatabase.AddObjectToAsset(fa.material, fa);
            AssetDatabase.SaveAssets();
            return fa;
        }

        static T Asset<T>(string path) where T : Object
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a == null) throw new System.Exception("missing asset " + path);
            return a;
        }

        static AnimationClip Clip(string name) =>
            AssetDatabase.LoadAllAssetsAtPath($"{CharacterImport.Folder}Animations/{name}.fbx")
                .OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));

        public static GameAssets EnsureGameAssets()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(GameAssetsPath));
            var a = AssetDatabase.LoadAssetAtPath<GameAssets>(GameAssetsPath);
            if (a == null)
            {
                a = ScriptableObject.CreateInstance<GameAssets>();
                AssetDatabase.CreateAsset(a, GameAssetsPath);
            }
            const float O = 0.03f; // obstacle outline (metres)
            // Palette tokens (HANDOVER §3.1), toon + outline: these build the obstacles.
            a.night = Toon("Night", new Color32(0x0E, 0x12, 0x30, 0xFF), O, 0f);
            a.surface = Toon("Surface", new Color32(0x2A, 0x30, 0x6E, 0xFF), O);
            a.magenta = Toon("NeonMagenta", new Color32(0xFF, 0x3D, 0xA6, 0xFF), O);
            a.cyan = Toon("NeonCyan", new Color32(0x2E, 0xE6, 0xFF, 0xFF), O);
            a.amber = Toon("LanternAmber", new Color32(0xFF, 0xB5, 0x47, 0xFF), O);
            a.text = Toon("Text", new Color32(0xF4, 0xF6, 0xFF, 0xFF), O);
            a.success = Toon("Success", new Color32(0x3D, 0xFF, 0xA2, 0xFF), O);
            a.danger = Toon("Danger", new Color32(0xFF, 0x5A, 0x5A, 0xFF), O);
            a.vanBody = Toon("VanBody", new Color32(0xC8, 0xD2, 0xF4, 0xFF), O);
            a.glass = Toon("Glass", new Color32(0x10, 0x16, 0x3A, 0xFF), O, 0f);
            a.wood = Toon("Wood", new Color32(0xC0, 0x74, 0x34, 0xFF), O);
            // Street
            a.ground = Toon("Ground", new Color32(0x22, 0x22, 0x4E, 0xFF), 0f, 0f);
            a.asphalt = a.ground;
            a.pavement = Toon("Pavement", new Color32(0x3A, 0x34, 0x6E, 0xFF), 0f, 0f);
            a.kerb = Toon("Kerb", new Color32(0xB9, 0xB4, 0xE6, 0xFF), 0f, 0f);
            a.laneLine = Unlit("LaneLine", new Color32(0x9C, 0xA4, 0xF0, 0xFF));
            var colormap = Asset<Texture2D>($"{EnvDir}/Commercial/Textures/colormap.png");
            a.buildingToon = Toon("Building", Color.white, 0f, 0.15f, null, colormap);
            a.buildingToon.SetColor("_ShadowTint", new Color(0.32f, 0.3f, 0.62f));
            a.awningMagenta = Toon("AwningMagenta", new Color32(0xFF, 0x3D, 0xA6, 0xFF), 0f, 0.2f);
            a.awningCyan = Toon("AwningCyan", new Color32(0x2E, 0xE6, 0xFF, 0xFF), 0f, 0.2f);
            a.awningAmber = Toon("AwningAmber", new Color32(0xFF, 0xB5, 0x47, 0xFF), 0f, 0.2f);
            a.lanternAmber = Toon("LanternGlowAmber", new Color32(0xFF, 0xB5, 0x47, 0xFF), 0f, 0f, new Color(1f, 0.62f, 0.2f) * 1.1f);
            a.lanternMagenta = Toon("LanternGlowMagenta", new Color32(0xFF, 0x3D, 0xA6, 0xFF), 0f, 0f, new Color(1f, 0.2f, 0.6f));
            a.lanternCyan = Toon("LanternGlowCyan", new Color32(0x2E, 0xE6, 0xFF, 0xFF), 0f, 0f, new Color(0.15f, 0.85f, 1f) * 0.9f);
            a.lanternCap = Toon("LanternCap", new Color32(0x24, 0x18, 0x40, 0xFF), 0f, 0f);
            a.neonMagenta = Unlit("NeonTubeMagenta", new Color(1f, 0.24f, 0.65f) * 1.6f);
            a.neonCyan = Unlit("NeonTubeCyan", new Color(0.18f, 0.9f, 1f) * 1.6f);
            a.neonAmber = Unlit("NeonTubeAmber", new Color(1f, 0.71f, 0.28f) * 1.6f);
            a.signBack = Toon("SignBack", new Color32(0x14, 0x10, 0x34, 0xFF), 0f, 0f);
            a.cord = Unlit("LanternCord", new Color32(0x10, 0x0C, 0x28, 0xFF));
            a.buildings = Enumerable.Range(0, 14).Select(i => Asset<GameObject>($"{EnvDir}/Commercial/building-{(char)('a' + i)}.fbx")).ToArray();
            // Glow
            a.sparkGlow = Unlit("SparkGlow", new Color(0.55f, 1f, 1f));
            a.magnetGlow = Unlit("MagnetGlow", new Color32(0xFF, 0x3D, 0xA6, 0xFF));
            a.shieldGlow = Unlit("ShieldGlow", new Color32(0x3D, 0xFF, 0xA2, 0xFF));
            a.doubleGlow = Unlit("DoubleGlow", new Color32(0xFF, 0xB5, 0x47, 0xFF));
            a.haloAmber = Glow("HaloAmber", new Color32(0xFF, 0xB5, 0x47, 0xFF), 0.55f);
            a.haloMagenta = Glow("HaloMagenta", new Color32(0xFF, 0x3D, 0xA6, 0xFF), 0.5f);
            a.haloCyan = Glow("HaloCyan", new Color32(0x2E, 0xE6, 0xFF, 0xFF), 0.45f);
            a.streakAmber = Glow("StreakAmber", new Color32(0xFF, 0xB5, 0x47, 0xFF), 0.35f, true, 1.4f);
            a.streakMagenta = Glow("StreakMagenta", new Color32(0xFF, 0x3D, 0xA6, 0xFF), 0.35f, true, 1.4f);
            a.streakCyan = Glow("StreakCyan", new Color32(0x2E, 0xE6, 0xFF, 0xFF), 0.3f, true, 1.4f);
            a.sparkHalo = Glow("SparkHalo", new Color32(0x2E, 0xE6, 0xFF, 0xFF), 0.65f, false, 2.2f);
            a.shieldHalo = Glow("ShieldHalo", new Color32(0x3D, 0xFF, 0xA2, 0xFF), 0.45f, false, 1.2f);
            a.blobShadow = Load("BlobShadow", "GameFactory/BlobShadow");
            // Courier
            a.courierModel = Asset<GameObject>(CharacterImport.ModelPath);
            a.runClip = Clip("run");
            a.jumpClip = Clip("jump");
            a.idleClip = Clip("idle");
            a.courierMira = CourierRenders.MaterialFor("mira");
            a.courierDex = CourierRenders.MaterialFor("dex");
            a.courierJuno = CourierRenders.MaterialFor("juno");
            // Audio
            a.music = Asset<AudioClip>($"{AudioDir}/Music/lantern_loop.wav");
            AudioClip Sfx(string n) => Asset<AudioClip>($"{AudioDir}/Sfx/{n}.ogg");
            a.sfxSpark = Sfx("spark");
            a.sfxLane = Sfx("lane");
            a.sfxJump = Sfx("jump");
            a.sfxSlide = Sfx("slide");
            a.sfxLand = Sfx("land");
            a.sfxCrash = Sfx("crash");
            a.sfxPowerUp = Sfx("powerup");
            a.sfxShieldBreak = Sfx("shield_break");
            a.sfxRevive = Sfx("revive");
            a.sfxCountdown = Sfx("countdown");
            a.sfxUiTap = Sfx("ui_tap");
            // Fonts (OFL) and icons (MIT)
            a.font = FontAsset("InstrumentSans-SemiBold.ttf", "InstrumentSans SDF");
            a.displayFont = FontAsset("BricolageGrotesque-ExtraBold.ttf", "BricolageGrotesque SDF");
            a.iconFont = FontAsset("Phosphor-Fill.ttf", "Phosphor SDF");
            EditorUtility.SetDirty(a);
            AssetDatabase.SaveAssets();
            return a;
        }

        static VolumeProfile EnsurePostFx()
        {
            var p = AssetDatabase.LoadAssetAtPath<VolumeProfile>(PostFxPath);
            if (p != null) return p;
            p = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(p, PostFxPath);
            void Add<T>(System.Action<T> set) where T : VolumeComponent
            {
                var c = p.Add<T>(true);
                c.name = typeof(T).Name;
                set(c);
                AssetDatabase.AddObjectToAsset(c, p);
            }
            Add<Bloom>(b =>
            {
                b.threshold.Override(0.72f);
                b.intensity.Override(1.5f);
                b.scatter.Override(0.7f);
                b.downscale.Override(BloomDownscaleMode.Quarter); // mobile: quarter-res bloom chain
                b.maxIterations.Override(5);
            });
            Add<ColorAdjustments>(c => { c.saturation.Override(18f); c.contrast.Override(12f); });
            Add<Vignette>(v => { v.intensity.Override(0.28f); v.smoothness.Override(0.5f); });
            Add<Tonemapping>(t => t.mode.Override(TonemappingMode.Neutral));
            EditorUtility.SetDirty(p);
            AssetDatabase.SaveAssets();
            return p;
        }

        static void CreateRunScene(GameAssets assets, Sim.ChunkLibrary lib, VolumeProfile postFx)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(0x1E, 0x14, 0x4E, 0xFF);
            cam.fieldOfView = 58f;
            cam.nearClipPlane = 2.5f; // nothing sits between the chase camera and the runner; clips overhead lanterns
            cam.farClipPlane = 150f;
            var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            camGo.AddComponent<AudioListener>();

            var moon = new GameObject("Moonlight").AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.shadows = LightShadows.None;
            moon.color = new Color32(0xB8, 0xC2, 0xFF, 0xFF);
            moon.intensity = 0.9f;
            moon.transform.rotation = Quaternion.Euler(38, -35, 0);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color32(0x5A, 0x4E, 0xB0, 0xFF);
            RenderSettings.ambientEquatorColor = new Color32(0x3A, 0x2C, 0x7A, 0xFF);
            RenderSettings.ambientGroundColor = new Color32(0x1A, 0x14, 0x40, 0xFF);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color32(0x26, 0x1A, 0x5C, 0xFF);
            RenderSettings.fogStartDistance = 22f;
            RenderSettings.fogEndDistance = 110f;

            var volume = new GameObject("PostFX").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(PostFxPath);

            new GameObject(FlutterBridge.ObjectName).AddComponent<FlutterBridge>();

            var rootGo = new GameObject("GameRoot");
            var root = rootGo.AddComponent<GameRoot>();
            // NewScene unloads assets only held from C#, so reload them by path before wiring the references.
            var so = new SerializedObject(root);
            so.FindProperty("assets").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameAssets>(GameAssetsPath);
            so.FindProperty("chunks").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sim.ChunkLibrary>(ChunkCatalog.LibraryPath);
            so.FindProperty("mainCamera").objectReferenceValue = cam;
            so.FindProperty("postFx").objectReferenceValue = volume;
            so.ApplyModifiedPropertiesWithoutUndo();
            rootGo.AddComponent<StandaloneHost>();

            Directory.CreateDirectory(Path.GetDirectoryName(RunScenePath));
            EditorSceneManager.SaveScene(scene, RunScenePath);
        }
    }
}
