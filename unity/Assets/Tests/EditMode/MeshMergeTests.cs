using NUnit.Framework;
using GameFactory.Core.Rendering;
using UnityEngine;

namespace Tests.EditMode
{
    /// <summary>The street merge that cut draw calls (D19): one renderer, one sub-mesh per material, children kept.</summary>
    public class MeshMergeTests
    {
        GameObject _root;
        Material _a, _b;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("root");
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            _a = new Material(shader);
            _b = new Material(shader);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_a);
            Object.DestroyImmediate(_b);
        }

        GameObject Cube(Vector3 pos, Material m)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(g.GetComponent<Collider>());
            g.transform.SetParent(_root.transform, false);
            g.transform.localPosition = pos;
            g.GetComponent<MeshRenderer>().sharedMaterial = m;
            return g;
        }

        [Test]
        public void MergesByMaterial_KeepsWorldPositions_AndNonMeshChildren()
        {
            _root.transform.position = new Vector3(0, 0, 100);
            Cube(new Vector3(-5, 0, 0), _a);
            Cube(new Vector3(5, 0, 0), _a);
            var withCord = Cube(new Vector3(0, 3, 0), _b);
            new GameObject("Cord").AddComponent<LineRenderer>().transform.SetParent(withCord.transform, false);

            var merged = MeshMerge.CombineByMaterial(_root);

            var renderers = _root.GetComponentsInChildren<MeshRenderer>();
            Assert.AreEqual(1, renderers.Length, "all mesh renderers merged into one");
            Assert.AreEqual(new[] { _a, _b }, renderers[0].sharedMaterials);
            var mesh = merged.GetComponent<MeshFilter>().sharedMesh;
            Assert.AreEqual(2, mesh.subMeshCount);
            Assert.AreEqual(3 * 24, mesh.vertexCount);
            Assert.AreEqual(-5.5f, mesh.bounds.min.x, 1e-4f, "positions are relative to the root, not doubled");
            Assert.AreEqual(3.5f, mesh.bounds.max.y, 1e-4f);
            Assert.IsNotNull(_root.GetComponentInChildren<LineRenderer>(), "the cord child survived");
        }

        [Test]
        public void PartsDifferingOnlyByColour_ShareOneBakedMaterial()
        {
            var toon = Shader.Find("GameFactory/ToonLit");
            var red = new Material(toon);
            red.SetColor("_BaseColor", Color.red);
            var blue = new Material(toon);
            blue.SetColor("_BaseColor", Color.blue);
            var glowing = new Material(toon);
            glowing.SetColor("_BaseColor", Color.blue);
            glowing.SetColor("_EmissionColor", Color.yellow); // emission not the colour × k: kept as its own material
            try
            {
                Cube(new Vector3(-2, 0, 0), red);
                Cube(new Vector3(2, 0, 0), blue);
                Cube(new Vector3(0, 2, 0), glowing);
                var merged = MeshMerge.CombineByMaterial(_root);
                var mr = merged.GetComponent<MeshRenderer>();
                Assert.AreEqual(2, mr.sharedMaterials.Length, "red + blue baked into one material, emissive separate");
                var baked = mr.sharedMaterials[0];
                Assert.AreEqual(1f, baked.GetFloat("_UseVertexColor"));
                Assert.AreEqual(Color.white, baked.GetColor("_BaseColor"));
                Assert.AreSame(glowing, mr.sharedMaterials[1]);
            }
            finally
            {
                Object.DestroyImmediate(red);
                Object.DestroyImmediate(blue);
                Object.DestroyImmediate(glowing);
            }
        }

        [Test]
        public void NothingToMerge_ReturnsNull()
        {
            Assert.IsNull(MeshMerge.CombineByMaterial(_root));
        }
    }
}
