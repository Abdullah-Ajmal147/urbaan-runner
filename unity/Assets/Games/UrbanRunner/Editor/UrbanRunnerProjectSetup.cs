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

        /// <summary>Street materials share one rim strength so merged segments bake into one material (D29).</summary>
        const float EnvRim = 0.12f;

        /// <summary>Self-lit (lanterns, neon, lit windows): emission = colour × k, so they bake together (D29).</summary>
        static Material Glowing(string name, Color32 color, float k) => Toon(name, color, 0f, 0f, (Color)color * k);

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

        /// <summary>A copy of the font's material with a soft underlay (an asset, so builds keep the shader variant).</summary>
        static Material ShadowMaterial(TMP_FontAsset font, string name)
        {
            var path = $"{FontDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(font.material);
                AssetDatabase.CreateAsset(m, path);
            }
            m.CopyPropertiesFromMaterial(font.material);
            m.EnableKeyword("UNDERLAY_ON");
            m.SetColor("_UnderlayColor", new Color(0.12f, 0.05f, 0.22f, 0.8f));
            m.SetFloat("_UnderlayOffsetX", 0f);
            m.SetFloat("_UnderlayOffsetY", -0.5f);
            m.SetFloat("_UnderlayDilate", 0.35f);
            m.SetFloat("_UnderlaySoftness", 0.45f);
            EditorUtility.SetDirty(m);
            return m;
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
            a.night = Toon("Night", new Color32(0x0E, 0x12, 0x30, 0xFF), O);
            a.surface = Toon("Surface", new Color32(0x6C, 0x4B, 0xC8, 0xFF), O);
            a.magenta = Toon("NeonMagenta", new Color32(0xFF, 0x3D, 0xA6, 0xFF), O);
            a.cyan = Toon("NeonCyan", new Color32(0x2E, 0xE6, 0xFF, 0xFF), O);
            a.amber = Toon("LanternAmber", new Color32(0xFF, 0xB5, 0x47, 0xFF), O);
            a.text = Toon("Text", new Color32(0xF4, 0xF6, 0xFF, 0xFF), O);
            a.success = Toon("Success", new Color32(0x3D, 0xFF, 0xA2, 0xFF), O);
            a.danger = Toon("Danger", new Color32(0xFF, 0x5A, 0x5A, 0xFF), O);
            a.vanBody = Toon("VanBody", new Color32(0x2E, 0xC4, 0xB6, 0xFF), O);
            a.glass = Toon("Glass", new Color32(0x10, 0x16, 0x3A, 0xFF), O);
            a.wood = Toon("Wood", new Color32(0xC0, 0x74, 0x34, 0xFF), O);
            // Street
            // "Lantern Festival at dusk": warm, many-coloured, painterly (D27).
            a.ground = Toon("Ground", new Color32(0x45, 0x3A, 0x5E, 0xFF), 0f, EnvRim);
            a.asphalt = a.ground;
            a.pavement = Toon("Pavement", new Color32(0xC9, 0x71, 0x4F, 0xFF), 0f, EnvRim);
            a.kerb = Toon("Kerb", new Color32(0xF2, 0xDC, 0xC0, 0xFF), 0f, EnvRim);
            a.laneLine = Unlit("LaneLine", new Color32(0xFF, 0xE3, 0xB0, 0xFF));
            var colormap = Asset<Texture2D>($"{EnvDir}/Commercial/Textures/colormap.png");
            a.buildingToon = Toon("Building", Color.white, 0f, 0.15f, null, colormap);
            a.buildingToon.SetColor("_ShadowTint", new Color(0.55f, 0.42f, 0.72f));
            // Every building gets its own painted colour (tints over the Kenney colormap).
            var paints = new[]
            {
                new Color32(0xFF, 0x8A, 0x7A, 0xFF), new Color32(0x3F, 0xC1, 0xB0, 0xFF), new Color32(0xF2, 0xB6, 0x40, 0xFF),
                new Color32(0x9B, 0xE3, 0xB8, 0xFF), new Color32(0x7F, 0xB8, 0xF0, 0xFF), new Color32(0xB7, 0x9C, 0xF0, 0xFF),
                new Color32(0xF2, 0x7B, 0xA8, 0xFF),
            };
            a.buildingPaints = paints.Select((c, i) =>
            {
                var m = Toon("BuildingPaint" + i, c, 0f, 0.18f, null, colormap);
                m.SetColor("_ShadowTint", new Color(0.55f, 0.42f, 0.72f));
                return m;
            }).ToArray();
            // Lantern City trams: one bold livery each, cream upper body, warm lit windows.
            a.tramPaints = new[]
            {
                Toon("TramTeal", new Color32(0x14, 0xA8, 0x9C, 0xFF), O), Toon("TramCoral", new Color32(0xFF, 0x5E, 0x5B, 0xFF), O),
                Toon("TramMustard", new Color32(0xF5, 0xA6, 0x23, 0xFF), O), Toon("TramCobalt", new Color32(0x3D, 0x5A, 0xFE, 0xFF), O),
            };
            a.tramCream = Toon("TramCream", new Color32(0xFF, 0xF1, 0xDC, 0xFF), O);
            a.tramRoof = Toon("TramRoof", new Color32(0x5B, 0x4A, 0x7A, 0xFF), O);
            a.tramWindow = Glowing("TramWindow", new Color32(0xFF, 0xC8, 0x6B, 0xFF), 1.0f);
            a.rail = Toon("Rail", new Color32(0x8E, 0x82, 0xA8, 0xFF), 0f, EnvRim);
            a.sky = Load("SkyDusk", "GameFactory/SkyGradient");
            a.sky.SetColor("_Top", new Color32(0x35, 0x26, 0x78, 0xFF));
            a.sky.SetColor("_Middle", new Color32(0xF2, 0x66, 0x8B, 0xFF));
            a.sky.SetColor("_Bottom", new Color32(0xFF, 0xB5, 0x61, 0xFF));
            a.sky.SetFloat("_MiddleHeight", 0.2f);
            a.sky.SetVector("_SunDir", new Vector4(0.1f, 0.06f, 1f, 0f));
            a.sky.SetColor("_SunColor", new Color(1f, 0.85f, 0.55f));
            a.sky.SetFloat("_SunSize", 40f);
            a.awningMagenta = Toon("AwningMagenta", new Color32(0xFF, 0x3D, 0xA6, 0xFF), 0f, EnvRim);
            a.awningCyan = Toon("AwningCyan", new Color32(0x2E, 0xE6, 0xFF, 0xFF), 0f, EnvRim);
            a.awningAmber = Toon("AwningAmber", new Color32(0xFF, 0xB5, 0x47, 0xFF), 0f, EnvRim);
            a.lanternAmber = Glowing("LanternGlowAmber", new Color32(0xFF, 0xB5, 0x47, 0xFF), 1.0f);
            a.lanternMagenta = Glowing("LanternGlowMagenta", new Color32(0xFF, 0x3D, 0xA6, 0xFF), 1.0f);
            a.lanternCyan = Glowing("LanternGlowCyan", new Color32(0x2E, 0xE6, 0xFF, 0xFF), 1.0f);
            a.lanternCap = Toon("LanternCap", new Color32(0x24, 0x18, 0x40, 0xFF), 0f, EnvRim);
            a.neonMagenta = Glowing("NeonTubeMagenta", new Color32(0xFF, 0x3D, 0xA6, 0xFF), 1.0f);
            a.neonCyan = Glowing("NeonTubeCyan", new Color32(0x2E, 0xE6, 0xFF, 0xFF), 1.0f);
            a.neonAmber = Glowing("NeonTubeAmber", new Color32(0xFF, 0xB5, 0x47, 0xFF), 1.0f);
            a.signBack = Toon("SignBack", new Color32(0x14, 0x10, 0x34, 0xFF), 0f, EnvRim);
            a.cord = Toon("LanternCord", new Color32(0x24, 0x18, 0x40, 0xFF), 0f, EnvRim);
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
            // Trails (D32): colours come from the trail/particle vertex colours.
            a.trailGlow = Glow("TrailGlow", new Color32(0xFF, 0xFF, 0xFF, 0xFF), 1.1f, false, 1.3f);
            a.trailGlow.SetFloat("_Shape", 2f);
            a.trailGlow.SetFloat("_UseVertexColor", 1f);
            a.kiteToon = Toon("KitePaper", Color.white, 0f, 0.3f);
            a.kiteToon.SetFloat("_UseVertexColor", 1f);
            // Lantern Hunt (D32): five festival colours.
            var hunt = new[]
            {
                ("Amber", new Color32(0xFF, 0xB5, 0x47, 0xFF)), ("Pink", new Color32(0xFF, 0x3D, 0xA6, 0xFF)), ("Blue", new Color32(0x2E, 0xE6, 0xFF, 0xFF)),
                ("Mint", new Color32(0x3D, 0xFF, 0xA2, 0xFF)), ("Violet", new Color32(0xB0, 0x7C, 0xFF, 0xFF)),
            };
            a.huntBodies = hunt.Select(h => Glowing("HuntLantern" + h.Item1, h.Item2, 1.0f)).ToArray();
            a.huntHalos = hunt.Select(h => Glow("HuntHalo" + h.Item1, h.Item2, 0.7f, false, 1.6f)).ToArray();
            a.huntBeams = hunt.Select(h => Glow("HuntBeam" + h.Item1, h.Item2, 0.45f, true, 1.2f)).ToArray();
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
            a.fontShadow = ShadowMaterial(a.font, "InstrumentSans Shadow");
            a.displayFontShadow = ShadowMaterial(a.displayFont, "BricolageGrotesque Shadow");
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
            // No Bloom (D31): the pipeline renders without HDR, so nothing ever crossed the bloom threshold, yet the
            // bloom chain cost several full-screen passes per frame. The glow comes from the additive halo sprites.
            Add<ColorAdjustments>(c => { c.saturation.Override(14f); c.contrast.Override(16f); });
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
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.backgroundColor = new Color32(0xE9, 0x87, 0x9A, 0xFF);
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.6f; // high chase camera (D27): nothing sits between it and the runner
            cam.farClipPlane = 170f;
            var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
            camGo.AddComponent<AudioListener>();

            var moon = new GameObject("Moonlight").AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.shadows = LightShadows.None;
            moon.name = "Sunset";
            moon.color = new Color32(0xFF, 0xD2, 0xA1, 0xFF);
            moon.intensity = 1.15f;
            moon.transform.rotation = Quaternion.Euler(28, -32, 0);
            RenderSettings.skybox = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialDir}/SkyDusk.mat");
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color32(0x9C, 0x7B, 0xD6, 0xFF);
            RenderSettings.ambientEquatorColor = new Color32(0xE8, 0x9A, 0xA0, 0xFF);
            RenderSettings.ambientGroundColor = new Color32(0x3A, 0x2C, 0x5E, 0xFF);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color32(0xF0, 0x93, 0x8E, 0xFF);
            RenderSettings.fogStartDistance = 35f;
            RenderSettings.fogEndDistance = 150f;

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
