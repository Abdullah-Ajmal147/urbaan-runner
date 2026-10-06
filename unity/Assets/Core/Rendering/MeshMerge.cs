using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace GameFactory.Core.Rendering
{
    /// <summary>
    /// Merges every MeshRenderer under a root into one mesh drawn by a single renderer, one sub-mesh per material.
    /// Parts whose materials differ only by colour are merged further: the colour is baked into the vertices and
    /// they share one material (shader property <c>_UseVertexColor</c>), so N parts in K colours cost one draw
    /// instead of K. Load-time only (it allocates). Skinned meshes, line renderers and particles are left alone.
    /// </summary>
    public static class MeshMerge
    {
        static readonly int UseVertexColor = Shader.PropertyToID("_UseVertexColor");
        static readonly int Emission = Shader.PropertyToID("_EmissionColor");
        static readonly string[] ColourProps = { "_BaseColor", "_Color" };
        static readonly string[] KeyFloats = { "_OutlineWidth", "_RimStrength", "_RimPower", "_RampThreshold", "_RampSmooth", "_Shape", "_Softness" };
        static readonly string[] KeyColours = { "_ShadowTint", "_RimColor", "_OutlineColor" };

        /// <summary>Shared "colour from vertices" materials, one per look (shader + everything but the colour).</summary>
        static readonly Dictionary<string, Material> Baked = new Dictionary<string, Material>();

        /// <summary>Number of shared baked materials created so far (tests, perf logs).</summary>
        public static int BakedMaterialCount => Baked.Count;

        public static GameObject CombineByMaterial(GameObject root, string name = "Merged")
        {
            var toRoot = root.transform.worldToLocalMatrix;
            var groups = new Dictionary<Material, List<CombineInstance>>();
            var order = new List<Material>();
            var sources = new List<MeshRenderer>();
            var temps = new List<Mesh>();
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                var mesh = mf.sharedMesh;
                var mats = r.sharedMaterials;
                for (var sub = 0; sub < mesh.subMeshCount; sub++)
                {
                    var m = mats[Mathf.Min(sub, mats.Length - 1)];
                    if (m == null) continue;
                    var target = m;
                    var source = mesh;
                    if (TryBake(m, out var shared, out var colour))
                    {
                        target = shared;
                        source = Coloured(mesh, colour);
                        temps.Add(source);
                    }
                    if (!groups.TryGetValue(target, out var list))
                    {
                        groups[target] = list = new List<CombineInstance>();
                        order.Add(target);
                    }
                    list.Add(new CombineInstance { mesh = source, subMeshIndex = sub, transform = toRoot * r.transform.localToWorldMatrix });
                }
                sources.Add(r);
            }
            if (order.Count == 0) return null;

            // One merged sub-mesh per material, then all of them as sub-meshes of the final mesh.
            var perMaterial = new CombineInstance[order.Count];
            for (var i = 0; i < order.Count; i++)
            {
                var part = new Mesh { indexFormat = IndexFormat.UInt32 };
                part.CombineMeshes(groups[order[i]].ToArray(), true, true);
                perMaterial[i] = new CombineInstance { mesh = part, transform = Matrix4x4.identity };
            }
            var merged = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            merged.CombineMeshes(perMaterial, false, false);
            merged.RecalculateBounds();
            merged.UploadMeshData(true);
            foreach (var ci in perMaterial) Kill(ci.mesh);
            foreach (var t in temps) Kill(t);

            // Remove only the merged components: children such as line-renderer cords must survive.
            foreach (var r in sources)
            {
                var mf = r.GetComponent<MeshFilter>();
                Kill(r);
                if (mf != null) Kill(mf);
            }

            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = merged;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = order.ToArray();
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return go;
        }

        /// <summary>
        /// A material can be baked when its shader takes vertex colours. Emissive materials bake too when their
        /// emission is their colour × k (the shader multiplies emission by the vertex colour): k joins the key.
        /// Returns the shared material for its look and the colour to bake (in the shader's space).
        /// </summary>
        static bool TryBake(Material m, out Material shared, out Color colour)
        {
            shared = null;
            colour = Color.white;
            if (!m.HasProperty(UseVertexColor)) return false;
            string colourProp = null;
            foreach (var p in ColourProps) if (m.HasProperty(p)) { colourProp = p; break; }
            if (colourProp == null) return false;
            var baseColour = m.GetColor(colourProp);

            var k = 0f;
            if (m.HasProperty(Emission))
            {
                var e = m.GetColor(Emission);
                if (e.maxColorComponent > 0.001f)
                {
                    if (baseColour.maxColorComponent < 0.001f) return false;
                    k = e.maxColorComponent / baseColour.maxColorComponent;
                    // Only when the emission really is the colour scaled (same hue): otherwise keep the material.
                    var expected = baseColour * k;
                    if (Mathf.Abs(expected.r - e.r) + Mathf.Abs(expected.g - e.g) + Mathf.Abs(expected.b - e.b) > 0.06f) return false;
                }
            }

            var key = new StringBuilder(m.shader.name);
            key.Append("|E").Append(k.ToString("0.00"));
            foreach (var p in m.GetTexturePropertyNames())
            {
                var t = m.GetTexture(p);
                key.Append('|').Append(t != null ? t.GetInstanceID() : 0);
            }
            foreach (var p in KeyFloats) if (m.HasProperty(p)) key.Append('|').Append(m.GetFloat(p).ToString("0.###"));
            foreach (var p in KeyColours) if (m.HasProperty(p)) key.Append('|').Append(m.GetColor(p).ToString());
            var id = key.ToString();
            if (!Baked.TryGetValue(id, out shared) || shared == null)
            {
                shared = new Material(m) { name = "Baked " + m.shader.name, enableInstancing = true };
                shared.SetColor(colourProp, Color.white);
                if (k > 0f) shared.SetColor(Emission, Color.white * k);
                shared.SetFloat(UseVertexColor, 1f);
                Baked[id] = shared;
            }
            var c = baseColour;
            // Material colours are converted to linear on upload; vertex colours are not, so convert here.
            colour = QualitySettings.activeColorSpace == ColorSpace.Linear ? c.linear : c;
            colour.a = c.a;
            return true;
        }

        static Mesh Coloured(Mesh mesh, Color colour)
        {
            var copy = Object.Instantiate(mesh);
            var cols = new Color[copy.vertexCount];
            for (var i = 0; i < cols.Length; i++) cols[i] = colour;
            copy.colors = cols;
            return copy;
        }

        static void Kill(Object o)
        {
            if (Application.isPlaying) Object.Destroy(o);
            else Object.DestroyImmediate(o);
        }
    }
}
