using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UrbanRunner.Game;
using GameFactory.Editor;
using UrbanRunner.Sim;

namespace UrbanRunner.Editor
{
    /// <summary>
    /// Art-direction stills for the design mockups: a Lantern City street with the toon shader, rendered from the
    /// in-game chase camera. Nothing here ships; it previews the look before the art pass is built into the game.
    /// Batchmode: -executeMethod UrbanRunner.Editor.GameplayShots.Batch -outputDir build/renders
    /// </summary>
    public static class GameplayShots
    {
        const string EnvDir = "Assets/Games/UrbanRunner/Art/Environment/Kenney";
        static readonly Color32 Magenta = new Color32(0xFF, 0x3D, 0xA6, 0xFF);
        static readonly Color32 Cyan = new Color32(0x2E, 0xE6, 0xFF, 0xFF);
        static readonly Color32 Amber = new Color32(0xFF, 0xB5, 0x47, 0xFF);

        public static void Batch() => BuildScripts.RunBatch(() =>
        {
            AndroidProjectConfigurator.ApplyAll();
            CourierRenders.EnsureMaterials();
            var dir = BuildScripts.Arg("-outputDir") ?? "../build/renders";
            Directory.CreateDirectory(dir);
            Render(dir, "ingame", crash: false);
            Render(dir, "crash", crash: true);
        });

        // ---------- materials ----------

        static Shader Toon => Shader.Find("GameFactory/ToonLit");

        static Material ToonMat(Color color, Texture tex = null, float outline = 0f, Color? rim = null, float rimStrength = 0.5f, Color? emission = null)
        {
            var m = new Material(outline > 0f ? Shader.Find("GameFactory/ToonLitOutline") : Toon);
            m.SetColor("_BaseColor", color);
            if (tex != null) m.SetTexture("_BaseMap", tex);
            m.SetFloat("_OutlineWidth", outline);
            m.SetColor("_RimColor", rim ?? new Color(1f, 0.55f, 0.85f));
            m.SetFloat("_RimStrength", rimStrength);
            m.SetColor("_EmissionColor", emission ?? Color.black);
            m.enableInstancing = true;
            return m;
        }

        static Material Glow(Color c, float intensity, bool streak = false, float softness = 2f)
        {
            var m = new Material(Shader.Find("GameFactory/AdditiveGlow"));
            m.SetColor("_Color", c * intensity);
            m.SetFloat("_Shape", streak ? 1 : 0);
            m.SetFloat("_Softness", softness);
            return m;
        }

        static Material Unlit(Color c)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            m.SetColor("_BaseColor", c);
            return m;
        }

        /// <summary>Replace every material under <paramref name="root"/> with a toon one (cached per colour/texture).</summary>
        static void Toonify(GameObject root, float outline, Dictionary<string, Material> cache)
        {
            foreach (var r in root.GetComponentsInChildren<Renderer>())
            {
                var src = r.sharedMaterial;
                if (src != null && src.shader.name.Contains("Unlit")) continue; // glow parts stay unlit
                var tex = src != null && src.HasProperty("_BaseMap") ? src.GetTexture("_BaseMap") : null;
                if (tex == null && src != null && src.HasProperty("_MainTex")) tex = src.GetTexture("_MainTex");
                var col = src != null && src.HasProperty("_BaseColor") ? src.GetColor("_BaseColor") : Color.white;
                var key = $"{col}|{(tex ? tex.name : "-")}|{outline}";
                if (!cache.TryGetValue(key, out var m)) cache[key] = m = ToonMat(col, tex, outline);
                var mats = r.sharedMaterials;
                for (var i = 0; i < mats.Length; i++) mats[i] = m;
                r.sharedMaterials = mats;
                r.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        // ---------- building blocks ----------

        static GameObject Prim(PrimitiveType t, Transform parent, Vector3 pos, Vector3 scale, Material m, Vector3 euler = default)
        {
            var g = GameObject.CreatePrimitive(t);
            Object.DestroyImmediate(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localScale = scale;
            g.transform.localEulerAngles = euler;
            var r = g.GetComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = ShadowCastingMode.Off;
            return g;
        }

        static GameObject Quad(Transform parent, Vector3 pos, Vector2 size, Material m, Vector3 euler) =>
            Prim(PrimitiveType.Quad, parent, pos, new Vector3(size.x, size.y, 1), m, euler);

        static void PointLight(Vector3 pos, Color c, float intensity, float range)
        {
            var l = new GameObject("Lantern Light").AddComponent<Light>();
            l.type = LightType.Point;
            l.color = c;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
            l.transform.position = pos;
        }

        /// <summary>A string of paper lanterns across the street, with halos and a wet-road reflection.</summary>
        static void LanternString(Transform root, float z, float sag, int seed, Material[] lanternMats, Material[] halos, Material[] streaks)
        {
            const float halfW = 6.6f, top = 6.4f;
            const int n = 9;
            for (var i = 0; i < n; i++)
            {
                var t = i / (float)(n - 1);
                var x = Mathf.Lerp(-halfW, halfW, t);
                var y = top - sag * 4f * t * (1 - t);
                var k = (i + seed) % halos.Length; // lanternMats[3] is the dark cap, not a colour
                var lantern = Prim(PrimitiveType.Sphere, root, new Vector3(x, y - 0.35f, z), new Vector3(0.42f, 0.55f, 0.42f), lanternMats[k]);
                Prim(PrimitiveType.Cylinder, lantern.transform, new Vector3(0, 0.55f, 0), new Vector3(0.5f, 0.08f, 0.5f), lanternMats[3]);
                var halo = Quad(root, new Vector3(x, y - 0.35f, z - 0.05f), new Vector2(1.9f, 1.9f), halos[k], new Vector3(0, 0, 0));
                halo.name = "Halo";
                if (i % 2 == 0)
                    Quad(root, new Vector3(x, 0.02f, z - 2.5f), new Vector2(0.9f, 6.5f), streaks[k], new Vector3(90, 0, 0));
            }
            // the cord
            var cord = new GameObject("Cord").AddComponent<LineRenderer>();
            cord.transform.SetParent(root, false);
            cord.positionCount = 24;
            for (var i = 0; i < 24; i++)
            {
                var t = i / 23f;
                cord.SetPosition(i, new Vector3(Mathf.Lerp(-halfW - 0.4f, halfW + 0.4f, t), top - sag * 4f * t * (1 - t), z));
            }
            cord.widthMultiplier = 0.035f;
            cord.sharedMaterial = Unlit(new Color32(0x10, 0x0C, 0x28, 0xFF));
            PointLight(new Vector3(0, top - sag - 0.6f, z), new Color32(0xFF, 0xB0, 0x60, 0xFF), 2.2f, 11f);
        }

        /// <summary>Invented neon glyph sign (no real letters or brands): a frame plus a few strokes.</summary>
        static void NeonSign(Transform root, Vector3 pos, float yaw, Color c, int variant, Material[] streaks, int streakIdx)
        {
            var s = new GameObject("NeonSign").transform;
            s.SetParent(root, false);
            s.localPosition = pos;
            s.localEulerAngles = new Vector3(0, yaw, 0);
            var tube = Unlit(c * 1.6f);
            var back = ToonMat(new Color32(0x14, 0x10, 0x34, 0xFF), outline: 0f, rimStrength: 0f);
            Prim(PrimitiveType.Cube, s, new Vector3(0, 0, 0.06f), new Vector3(1.3f, 2.2f, 0.1f), back);
            // frame
            Prim(PrimitiveType.Cube, s, new Vector3(0, 1.05f, 0), new Vector3(1.2f, 0.07f, 0.06f), tube);
            Prim(PrimitiveType.Cube, s, new Vector3(0, -1.05f, 0), new Vector3(1.2f, 0.07f, 0.06f), tube);
            Prim(PrimitiveType.Cube, s, new Vector3(-0.6f, 0, 0), new Vector3(0.07f, 2.1f, 0.06f), tube);
            Prim(PrimitiveType.Cube, s, new Vector3(0.6f, 0, 0), new Vector3(0.07f, 2.1f, 0.06f), tube);
            // glyphs
            switch (variant % 3)
            {
                case 0:
                    Prim(PrimitiveType.Cube, s, new Vector3(0, 0.55f, 0), new Vector3(0.6f, 0.07f, 0.06f), tube);
                    Prim(PrimitiveType.Cube, s, new Vector3(0, 0.1f, 0), new Vector3(0.07f, 0.7f, 0.06f), tube);
                    Prim(PrimitiveType.Cylinder, s, new Vector3(0, -0.55f, 0), new Vector3(0.45f, 0.03f, 0.45f), tube, new Vector3(90, 0, 0));
                    break;
                case 1:
                    Prim(PrimitiveType.Cube, s, new Vector3(0, 0.45f, 0), new Vector3(0.45f, 0.45f, 0.06f), tube, new Vector3(0, 0, 45));
                    Prim(PrimitiveType.Cube, s, new Vector3(0, -0.45f, 0), new Vector3(0.7f, 0.07f, 0.06f), tube);
                    break;
                default:
                    Prim(PrimitiveType.Cube, s, new Vector3(-0.2f, 0.2f, 0), new Vector3(0.07f, 1.2f, 0.06f), tube);
                    Prim(PrimitiveType.Cube, s, new Vector3(0.2f, -0.1f, 0), new Vector3(0.07f, 0.9f, 0.06f), tube);
                    Prim(PrimitiveType.Cube, s, new Vector3(0, -0.65f, 0), new Vector3(0.6f, 0.07f, 0.06f), tube);
                    break;
            }
            Quad(s, new Vector3(0, 0, -0.08f), new Vector2(2.8f, 3.6f), Glow(c, 0.5f, softness: 1.6f), Vector3.zero);
            Quad(root, new Vector3(pos.x * 0.8f, 0.02f, pos.z - 3.2f), new Vector2(1.1f, 7f), streaks[streakIdx], new Vector3(90, 0, 0));
        }

        static GameObject LoadModel(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(path);

        // ---------- the shot ----------

        static void Render(string dir, string name, bool crash)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cache = new Dictionary<string, Material>();
            var world = new GameObject("World").transform;
            var rng = new System.Random(7);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color32(0x5A, 0x4E, 0xB0, 0xFF);
            RenderSettings.ambientEquatorColor = new Color32(0x3A, 0x2C, 0x7A, 0xFF);
            RenderSettings.ambientGroundColor = new Color32(0x1A, 0x14, 0x40, 0xFF);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color32(0x26, 0x1A, 0x5C, 0xFF);
            RenderSettings.fogStartDistance = 22f;
            RenderSettings.fogEndDistance = 95f;

            var moon = new GameObject("Moon").AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.color = new Color32(0xB8, 0xC2, 0xFF, 0xFF);
            moon.intensity = 0.9f;
            moon.shadows = LightShadows.None;
            moon.transform.rotation = Quaternion.Euler(38, -35, 0);

            var lanternMats = new[]
            {
                ToonMat(Amber, emission: new Color(1f, 0.62f, 0.2f) * 1.1f, rimStrength: 0f),
                ToonMat(Magenta, emission: new Color(1f, 0.2f, 0.6f) * 1.0f, rimStrength: 0f),
                ToonMat(Cyan, emission: new Color(0.15f, 0.85f, 1f) * 0.9f, rimStrength: 0f),
                ToonMat(new Color32(0x24, 0x18, 0x40, 0xFF), rimStrength: 0f),
            };
            var halos = new[] { Glow(Amber, 0.55f), Glow(Magenta, 0.5f), Glow(Cyan, 0.45f) };
            var streaks = new[] { Glow(Amber, 0.35f, true, 1.4f), Glow(Magenta, 0.35f, true, 1.4f), Glow(Cyan, 0.3f, true, 1.4f) };

            // Street: wet asphalt, kerbs, pavements, lane markings.
            var asphalt = ToonMat(new Color32(0x22, 0x22, 0x4E, 0xFF), rimStrength: 0f);
            Prim(PrimitiveType.Cube, world, new Vector3(0, -0.05f, 60), new Vector3(9.4f, 0.1f, 150), asphalt);
            var pavement = ToonMat(new Color32(0x3A, 0x34, 0x6E, 0xFF), rimStrength: 0f);
            Prim(PrimitiveType.Cube, world, new Vector3(-6.2f, 0.08f, 60), new Vector3(3.2f, 0.16f, 150), pavement);
            Prim(PrimitiveType.Cube, world, new Vector3(6.2f, 0.08f, 60), new Vector3(3.2f, 0.16f, 150), pavement);
            var kerb = ToonMat(new Color32(0xB9, 0xB4, 0xE6, 0xFF), rimStrength: 0f);
            Prim(PrimitiveType.Cube, world, new Vector3(-4.65f, 0.09f, 60), new Vector3(0.18f, 0.18f, 150), kerb);
            Prim(PrimitiveType.Cube, world, new Vector3(4.65f, 0.09f, 60), new Vector3(0.18f, 0.18f, 150), kerb);
            var dash = Unlit(new Color32(0x9C, 0xA4, 0xF0, 0xFF));
            for (var z = -6f; z < 120f; z += 4.5f)
            {
                Quad(world, new Vector3(-1.25f, 0.01f, z), new Vector2(0.1f, 2f), dash, new Vector3(90, 0, 0));
                Quad(world, new Vector3(1.25f, 0.01f, z), new Vector2(0.1f, 2f), dash, new Vector3(90, 0, 0));
            }

            // Buildings both sides (Kenney City Kit Commercial, CC0), toon-shaded and tinted for night.
            var buildingPaths = Enumerable.Range(0, 14).Select(i => $"{EnvDir}/Commercial/building-{(char)('a' + i)}.fbx").ToArray();
            foreach (var side in new[] { -1, 1 })
            {
                var z = -8f;
                while (z < 125f)
                {
                    var prefab = LoadModel(buildingPaths[rng.Next(buildingPaths.Length)]);
                    var b = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    b.transform.SetParent(world, false);
                    var bounds = b.GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((a, c) => { a.Encapsulate(c); return a; });
                    var scale = 7.2f / Mathf.Max(bounds.size.x, bounds.size.z);
                    b.transform.localScale = Vector3.one * scale;
                    b.transform.localRotation = Quaternion.Euler(0, side < 0 ? 90 : -90, 0);
                    var depth = Mathf.Max(bounds.size.x, bounds.size.z) * scale;
                    b.transform.localPosition = new Vector3(side * (7.8f + depth * 0.5f), 0.16f, z + depth * 0.5f);
                    Toonify(b, 0f, cache);
                    foreach (var r in b.GetComponentsInChildren<Renderer>())
                        r.sharedMaterial.SetColor("_ShadowTint", new Color(0.32f, 0.3f, 0.62f));
                    z += depth + 0.6f;
                }
            }
            // Awnings over the pavements in market colours.
            for (var z = 4f; z < 100f; z += 11f)
                foreach (var side in new[] { -1, 1 })
                {
                    var col = (int)(z / 11f + (side > 0 ? 1 : 0)) % 3 == 0 ? Magenta : (int)(z / 11f) % 2 == 0 ? Cyan : Amber;
                    Prim(PrimitiveType.Cube, world, new Vector3(side * 6.9f, 3.1f, z), new Vector3(1.8f, 0.12f, 4.5f), ToonMat(col, rimStrength: 0.2f), new Vector3(0, 0, side * 14f));
                }

            // Neon glyph signs on the facades.
            for (var i = 0; i < 9; i++)
            {
                var side = i % 2 == 0 ? -1 : 1;
                var z = 8f + i * 9.5f;
                var c = i % 3 == 0 ? Magenta : i % 3 == 1 ? Cyan : Amber;
                NeonSign(world, new Vector3(side * 7.55f, 5.2f, z), side < 0 ? 90 : -90, c, i, streaks, i % 3);
            }

            // Lantern strings over the street.
            for (var i = 0; i < 7; i++) LanternString(world, 6f + i * 13f, 1.1f, i, lanternMats, halos, streaks);

            // Obstacles from the game's own visual factory, toon-shaded.
            var assets = AssetDatabase.LoadAssetAtPath<GameAssets>(UrbanRunnerProjectSetup.GameAssetsPath);
            GameObject Obstacle(Obstacle kind, int lane, float z)
            {
                var o = VisualFactory.Obstacle(kind, assets, world);
                o.SetActive(true);
                o.transform.localPosition = new Vector3(TrackRules.LaneX(lane), 0, z);
                Toonify(o, 0.03f, cache);
                return o;
            }
            if (!crash)
            {
                Obstacle(Sim.Obstacle.Crate, 0, 13f);
                Obstacle(Sim.Obstacle.Stall, 2, 17f);
                Obstacle(Sim.Obstacle.Banner, 1, 28f);
                Obstacle(Sim.Obstacle.Van, 0, 36f);
                // Spark trail in the runner's lane, arcing over nothing: a straight line leading the eye.
                var spark = Unlit(new Color(0.55f, 1f, 1f));
                var sparkHalo = Glow(Cyan, 0.65f, softness: 2.2f);
                for (var z = 3.2f; z < 25f; z += 2f)
                {
                    var p = new Vector3(0, 1.0f, z);
                    Prim(PrimitiveType.Sphere, world, p, Vector3.one * 0.34f, spark);
                    Quad(world, p + new Vector3(0, 0, -0.05f), new Vector2(1.1f, 1.1f), sparkHalo, Vector3.zero);
                }
            }
            else
            {
                Obstacle(Sim.Obstacle.Stall, 1, 1.6f);
                Obstacle(Sim.Obstacle.Crate, 2, 9f);
            }

            // The courier.
            var courier = (GameObject)PrefabUtility.InstantiatePrefab(LoadModel(CharacterImport.ModelPath));
            var skin = AssetDatabase.LoadAssetAtPath<Texture2D>(SkinBaker.PathFor("mira"));
            var courierMat = ToonMat(Color.white, skin, 0.006f, new Color(1f, 0.45f, 0.8f), 0.45f);
            courierMat.SetColor("_ShadowTint", new Color(0.55f, 0.48f, 0.85f));
            courierMat.SetFloat("_RampThreshold", 0.0f);
            foreach (var r in courier.GetComponentsInChildren<Renderer>()) { r.sharedMaterial = courierMat; r.shadowCastingMode = ShadowCastingMode.Off; }
            var clipName = crash ? "jump" : "run";
            var clip = AssetDatabase.LoadAllAssetsAtPath($"{CharacterImport.Folder}Animations/{clipName}.fbx").OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
            clip.SampleAnimation(courier.transform.Find("Root").gameObject, clip.length * (crash ? 0.08f : 0.62f));
            var pivot = new GameObject("CourierPivot").transform;
            courier.transform.SetParent(pivot, false);
            if (!crash)
            {
                pivot.position = new Vector3(0, 0, 0);
                pivot.rotation = Quaternion.Euler(9f, 0, 0); // running lean
            }
            else
            {
                pivot.position = new Vector3(0.2f, 0, 0.3f);
                pivot.rotation = Quaternion.Euler(-24f, 160f, 8f); // knocked back, facing the camera
                var star = Unlit(new Color(1f, 0.85f, 0.4f));
                for (var i = 0; i < 4; i++)
                {
                    var a = i * Mathf.PI * 0.5f + 0.4f;
                    Prim(PrimitiveType.Sphere, world, new Vector3(0.2f + Mathf.Cos(a) * 0.45f, 2.05f, 0.1f + Mathf.Sin(a) * 0.45f), Vector3.one * 0.13f, star);
                }
            }
            // Contact shadow (blob) under the runner.
            var blob = new Material(Shader.Find("GameFactory/BlobShadow"));
            Quad(world, new Vector3(pivot.position.x, 0.03f, pivot.position.z), new Vector2(1.4f, 1.1f), blob, new Vector3(90, 0, 0));
            // Key light on the courier so it reads against the street.
            PointLight(new Vector3(-1.6f, 3.2f, -2.2f), new Color32(0xFF, 0xD6, 0xB0, 0xFF), 2.6f, 7f);
            PointLight(new Vector3(2.4f, 2.0f, 1.5f), Magenta, 2.2f, 6f);

            // Post-processing: bloom on neon, gentle contrast, vignette.
            var volume = new GameObject("PostFX").AddComponent<Volume>();
            volume.isGlobal = true;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(0.72f);
            bloom.intensity.Override(1.6f);
            bloom.scatter.Override(0.72f);
            var ca = profile.Add<ColorAdjustments>(true);
            ca.saturation.Override(18f);
            ca.contrast.Override(12f);
            var vig = profile.Add<Vignette>(true);
            vig.intensity.Override(0.3f);
            vig.smoothness.Override(0.5f);
            var tm = profile.Add<Tonemapping>(true);
            tm.mode.Override(TonemappingMode.Neutral);
            volume.sharedProfile = profile;

            // Chase camera.
            var cam = new GameObject("Camera").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(0x1E, 0x14, 0x4E, 0xFF);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 140f;
            cam.fieldOfView = crash ? 52f : 58f;
            if (!crash)
            {
                cam.transform.position = new Vector3(0, 4.1f, -6.6f);
                cam.transform.LookAt(new Vector3(0, 0.6f, 12f));
            }
            else
            {
                cam.transform.position = new Vector3(1.6f, 2.4f, -3.6f);
                cam.transform.LookAt(new Vector3(0.1f, 0.9f, 0.8f));
            }
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;

            const int w = 1080, h = 2340;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.aspect = (float)w / h;
            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
            Debug.Log("UR_GAME rendered " + name);
        }
    }
}
