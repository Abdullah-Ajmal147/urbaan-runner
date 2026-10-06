using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace UrbanRunner.Editor
{
    /// <summary>
    /// Courier materials + marketing/menu renders (transparent PNGs) of the real in-game model.
    /// Batchmode: -executeMethod UrbanRunner.Editor.CourierRenders.Batch -outputDir build/renders
    /// </summary>
    public static class CourierRenders
    {
        public const string MaterialDir = "Assets/Games/UrbanRunner/Art/Characters/Materials";

        public static void Batch() => BuildScripts.RunBatch(() =>
        {
            // Force the character FBX through the current import rules (a script change alone doesn't reimport).
            foreach (var path in new[] { "run", "idle", "jump" })
                AssetDatabase.ImportAsset($"{CharacterImport.Folder}Animations/{path}.fbx", ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(CharacterImport.ModelPath, ImportAssetOptions.ForceUpdate);
            SkinBaker.BakeAll();
            if (BuildScripts.Arg("-uvdebug") != null) SkinBaker.BakeUvDebug();
            AssetDatabase.Refresh();
            EnsureMaterials();
            var dir = BuildScripts.Arg("-outputDir") ?? "../build/renders";
            Directory.CreateDirectory(dir);
            RenderAll(dir);
        });

        /// <summary>Logs the model hierarchy, mesh/UV info and each clip's bone paths (import debugging).</summary>
        public static void Diagnose() => BuildScripts.RunBatch(() =>
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterImport.ModelPath);
            void Walk(Transform t, string path)
            {
                Debug.Log($"UR_DIAG node {path} rot={t.localEulerAngles} scale={t.localScale}");
                foreach (Transform c in t) Walk(c, path + "/" + c.name);
            }
            Walk(prefab.transform, prefab.name);
            foreach (var smr in prefab.GetComponentsInChildren<SkinnedMeshRenderer>())
                Debug.Log($"UR_DIAG mesh {smr.name} verts={smr.sharedMesh.vertexCount} uv={smr.sharedMesh.uv.Length} bounds={smr.sharedMesh.bounds} root={smr.rootBone?.name}");
            foreach (var n in new[] { "run", "idle", "jump" })
            {
                var all = AssetDatabase.LoadAllAssetsAtPath($"{CharacterImport.Folder}Animations/{n}.fbx");
                foreach (var a in all) Debug.Log($"UR_DIAG asset in {n}.fbx: {a.GetType().Name} '{a.name}'");
                var clip = Clip(n);
                foreach (var b in AnimationUtility.GetCurveBindings(clip).Take(6))
                    Debug.Log($"UR_DIAG clip {n} path='{b.path}' prop={b.propertyName}");
                Debug.Log($"UR_DIAG clip {n} length={clip.length} legacy={clip.legacy} bindings={AnimationUtility.GetCurveBindings(clip).Length}");
            }
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(SkinBaker.PathFor("mira"));
            Debug.Log($"UR_DIAG skin tex={(tex == null ? "NULL" : tex.width + "x" + tex.height)} mat tex={MaterialFor("mira")?.GetTexture("_BaseMap")}");
        });

        public static Material MaterialFor(string id) => AssetDatabase.LoadAssetAtPath<Material>($"{MaterialDir}/Courier_{id}.mat");

        public static void EnsureMaterials()
        {
            Directory.CreateDirectory(MaterialDir);
            var ids = SkinBaker.Couriers.Select(c => c.Id).ToList();
            if (File.Exists(SkinBaker.PathFor("uvdebug"))) ids.Add("uvdebug");
            foreach (var id in ids)
            {
                var path = $"{MaterialDir}/Courier_{id}.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                var toon = Shader.Find("GameFactory/ToonLitOutline");
                if (mat == null)
                {
                    mat = new Material(toon);
                    AssetDatabase.CreateAsset(mat, path);
                }
                else if (mat.shader != toon) mat.shader = toon;
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(SkinBaker.PathFor(id));
                mat.SetTexture("_BaseMap", tex);
                mat.SetColor("_BaseColor", Color.white);
                mat.SetColor("_ShadowTint", new Color(0.55f, 0.48f, 0.85f));
                mat.SetFloat("_RampThreshold", 0f);
                mat.SetColor("_RimColor", new Color(1f, 0.45f, 0.8f));
                mat.SetFloat("_RimStrength", 0.45f);
                mat.SetFloat("_OutlineWidth", 0.006f);
                mat.enableInstancing = true;
                EditorUtility.SetDirty(mat);
            }
            AssetDatabase.SaveAssets();
        }

        static AnimationClip Clip(string name) =>
            AssetDatabase.LoadAllAssetsAtPath($"{CharacterImport.Folder}Animations/{name}.fbx")
                .OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));

        static GameObject Spawn(string id, Vector3 pos, float yaw, string clip, float phase)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterImport.ModelPath);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                r.sharedMaterial = MaterialFor(id);
                r.shadowCastingMode = ShadowCastingMode.Off;
            }
            // Clips bind bones relative to the skeleton's "Root" node, not the model's top object.
            var a = Clip(clip);
            a.SampleAnimation(go.transform.Find("Root").gameObject, a.length * phase);
            return go;
        }

        static Bounds BoundsOf(GameObject[] gos)
        {
            var b = new Bounds(gos[0].transform.position, Vector3.zero);
            foreach (var g in gos) foreach (var r in g.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
            return b;
        }

        static void Light(string name, LightType type, Color color, float intensity, Vector3 pos, Vector3 lookAt, float range = 10f)
        {
            var go = new GameObject(name);
            var l = go.AddComponent<Light>();
            l.type = type;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.spotAngle = 70f;
            l.shadows = LightShadows.None;
            go.transform.position = pos;
            go.transform.LookAt(lookAt);
        }

        public static void RenderAll(string dir)
        {
            if (BuildScripts.Arg("-uvdebug") != null)
            {
                foreach (var yaw in new[] { 0, 90, 180, 270 })
                foreach (var id in new[] { "uvdebug", "mira" })
                {
                    Shot(dir, $"dbg_{id}_{yaw}", 512, 700, () =>
                    {
                        // Created per shot: a new scene unloads unreferenced in-memory materials.
                        var unlit = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                        unlit.SetTexture("_BaseMap", MaterialFor(id).GetTexture("_BaseMap"));
                        var g = Spawn(id, Vector3.zero, yaw, "idle", 0f);
                        foreach (var r in g.GetComponentsInChildren<Renderer>()) r.sharedMaterial = unlit;
                        return new[] { g };
                    }, new Vector3(0, 0.1f, 1f));
                }
                return;
            }
            foreach (var c in SkinBaker.Couriers)
            {
                // Yaw 0 faces the camera (+Z).
                Shot(dir, $"{c.Id}_run", 1024, 1400, () => new[] { Spawn(c.Id, Vector3.zero, -28, "run", 0.3f) }, new Vector3(-0.2f, 0.12f, 1f));
                Shot(dir, $"{c.Id}_idle", 1024, 1400, () => new[] { Spawn(c.Id, Vector3.zero, 14, "idle", 0.2f) }, new Vector3(0.1f, 0.06f, 1f));
            }
            Shot(dir, "crew_run", 2048, 1400, () => new[]
            {
                Spawn("dex", new Vector3(-1.15f, 0, -0.7f), -18, "run", 0.55f),
                Spawn("mira", new Vector3(0, 0, 0.2f), -24, "run", 0.3f),
                Spawn("juno", new Vector3(1.15f, 0, -0.6f), -30, "run", 0.8f),
            }, new Vector3(-0.25f, 0.12f, 1f));
        }

        static void Shot(string dir, string name, int w, int h, System.Func<GameObject[]> build, Vector3 viewDir)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color32(0x8A, 0x8C, 0xB8, 0xFF);
            RenderSettings.fog = false;

            var gos = build();
            var b = BoundsOf(gos);
            var target = b.center;

            Light("Key", LightType.Directional, new Color(1f, 0.95f, 0.88f), 1.7f, target + new Vector3(1.5f, 2.5f, 3.5f), target);
            Light("RimMagenta", LightType.Spot, new Color32(0xFF, 0x3D, 0xA6, 0xFF), 22f, target + new Vector3(-2.2f, 1.6f, -1.6f), target, 8f);
            Light("RimCyan", LightType.Spot, new Color32(0x2E, 0xE6, 0xFF, 0xFF), 18f, target + new Vector3(2.2f, 1.2f, -1.8f), target, 8f);
            Light("Bounce", LightType.Point, new Color32(0xFF, 0xB5, 0x47, 0xFF), 1.2f, target + new Vector3(0, -0.6f, 1.6f), target, 4f);

            var camGo = new GameObject("Camera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0, 0, 0, 0);
            cam.fieldOfView = 24f;
            cam.nearClipPlane = 0.05f;
            var aspect = (float)w / h;
            var size = Mathf.Max(b.size.y, b.size.x / aspect) * 0.62f;
            var dist = size / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            camGo.transform.position = target + viewDir.normalized * dist;
            camGo.transform.LookAt(target);

            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
            cam.targetTexture = rt;
            cam.aspect = aspect;
            cam.Render();

            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
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
