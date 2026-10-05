using UnityEngine;
using UrbanRunner.Sim;

namespace UrbanRunner.Game
{
    /// <summary>
    /// Builds the low-poly visuals from primitives (HANDOVER §3.12). Each obstacle has a distinct silhouette,
    /// so it reads by shape and not only by colour (§3.1). Called only while prewarming pools, never during a run.
    /// </summary>
    public static class VisualFactory
    {
        static GameObject Part(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Material mat, Vector3 euler = default)
        {
            var go = GameObject.CreatePrimitive(type);
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.transform.localEulerAngles = euler;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        static Transform Root(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        /// <summary>Flat dark disc under objects instead of realtime shadows (§3.11).</summary>
        static void BlobShadow(Transform t, GameAssets a, float w, float d) =>
            Part(PrimitiveType.Cylinder, t, new Vector3(0, 0.01f, d * 0.5f), new Vector3(w, 0.005f, d), a.night);

        /// <summary>Visual origin is the obstacle's front edge (z = row start), growing toward +z.</summary>
        public static GameObject Obstacle(Obstacle kind, GameAssets a, Transform parent)
        {
            var t = Root(kind.ToString(), parent);
            var len = TrackRules.Length(kind);
            switch (kind)
            {
                case Sim.Obstacle.Crate: // two stacked low crates: jump
                    BlobShadow(t, a, 2.0f, len);
                    Part(PrimitiveType.Cube, t, new Vector3(0, 0.23f, 0.5f), new Vector3(1.8f, 0.45f, 1.0f), a.wood);
                    Part(PrimitiveType.Cube, t, new Vector3(0.1f, 0.68f, 0.5f), new Vector3(1.4f, 0.44f, 0.9f), a.amber, new Vector3(0, 6, 0));
                    break;
                case Sim.Obstacle.Banner: // hanging sign between two poles: slide
                    Part(PrimitiveType.Cylinder, t, new Vector3(-1.05f, 1.5f, 0.15f), new Vector3(0.12f, 1.5f, 0.12f), a.surface);
                    Part(PrimitiveType.Cylinder, t, new Vector3(1.05f, 1.5f, 0.15f), new Vector3(0.12f, 1.5f, 0.12f), a.surface);
                    Part(PrimitiveType.Cube, t, new Vector3(0, 1.55f, 0.15f), new Vector3(2.1f, 1.1f, 0.12f), a.magenta);
                    // invented glyphs, never real words (§3.1)
                    Part(PrimitiveType.Cube, t, new Vector3(-0.5f, 1.6f, 0.08f), new Vector3(0.35f, 0.5f, 0.02f), a.text, new Vector3(0, 0, 20));
                    Part(PrimitiveType.Cube, t, new Vector3(0.1f, 1.6f, 0.08f), new Vector3(0.12f, 0.6f, 0.02f), a.text);
                    Part(PrimitiveType.Cube, t, new Vector3(0.55f, 1.6f, 0.08f), new Vector3(0.4f, 0.12f, 0.02f), a.text);
                    break;
                case Sim.Obstacle.Stall: // market stall with striped awning: change lane
                    BlobShadow(t, a, 2.2f, len);
                    Part(PrimitiveType.Cube, t, new Vector3(0, 0.55f, len * 0.5f), new Vector3(2.0f, 1.1f, len), a.surface);
                    Part(PrimitiveType.Cube, t, new Vector3(0, 2.2f, len * 0.5f), new Vector3(2.3f, 0.12f, len + 0.3f), a.cyan, new Vector3(-12, 0, 0));
                    Part(PrimitiveType.Cylinder, t, new Vector3(-0.95f, 1.6f, 0.1f), new Vector3(0.1f, 0.55f, 0.1f), a.text);
                    Part(PrimitiveType.Cylinder, t, new Vector3(0.95f, 1.6f, 0.1f), new Vector3(0.1f, 0.55f, 0.1f), a.text);
                    Part(PrimitiveType.Sphere, t, new Vector3(0, 1.85f, 0.0f), new Vector3(0.45f, 0.55f, 0.45f), a.amber); // paper lantern
                    break;
                case Sim.Obstacle.Van: // tall, long parked van: change lane
                    BlobShadow(t, a, 2.2f, len);
                    Part(PrimitiveType.Cube, t, new Vector3(0, 1.25f, len * 0.5f), new Vector3(2.0f, 2.1f, len), a.vanBody);
                    Part(PrimitiveType.Cube, t, new Vector3(0, 1.75f, 0.02f), new Vector3(1.7f, 0.7f, 0.06f), a.glass);
                    Part(PrimitiveType.Cylinder, t, new Vector3(-0.85f, 0.3f, 0.8f), new Vector3(0.55f, 0.12f, 0.55f), a.night, new Vector3(0, 0, 90));
                    Part(PrimitiveType.Cylinder, t, new Vector3(0.85f, 0.3f, 0.8f), new Vector3(0.55f, 0.12f, 0.55f), a.night, new Vector3(0, 0, 90));
                    Part(PrimitiveType.Cylinder, t, new Vector3(-0.85f, 0.3f, len - 0.8f), new Vector3(0.55f, 0.12f, 0.55f), a.night, new Vector3(0, 0, 90));
                    Part(PrimitiveType.Cylinder, t, new Vector3(0.85f, 0.3f, len - 0.8f), new Vector3(0.55f, 0.12f, 0.55f), a.night, new Vector3(0, 0, 90));
                    break;
                case Sim.Obstacle.Cart: // rolling food cart with a warning stripe: change lane
                    BlobShadow(t, a, 1.6f, len);
                    Part(PrimitiveType.Cube, t, new Vector3(0, 0.8f, len * 0.5f), new Vector3(1.5f, 0.9f, len), a.amber);
                    Part(PrimitiveType.Cube, t, new Vector3(0, 0.8f, -0.01f), new Vector3(1.5f, 0.25f, 0.04f), a.danger);
                    Part(PrimitiveType.Cylinder, t, new Vector3(-0.7f, 0.25f, 0.3f), new Vector3(0.45f, 0.08f, 0.45f), a.night, new Vector3(0, 0, 90));
                    Part(PrimitiveType.Cylinder, t, new Vector3(0.7f, 0.25f, 0.3f), new Vector3(0.45f, 0.08f, 0.45f), a.night, new Vector3(0, 0, 90));
                    Part(PrimitiveType.Cylinder, t, new Vector3(-0.7f, 0.25f, len - 0.3f), new Vector3(0.45f, 0.08f, 0.45f), a.night, new Vector3(0, 0, 90));
                    Part(PrimitiveType.Cylinder, t, new Vector3(0.7f, 0.25f, len - 0.3f), new Vector3(0.45f, 0.08f, 0.45f), a.night, new Vector3(0, 0, 90));
                    break;
            }
            t.gameObject.SetActive(false);
            return t.gameObject;
        }

        public static GameObject Spark(GameAssets a, Transform parent)
        {
            var go = Part(PrimitiveType.Sphere, parent, Vector3.zero, Vector3.one * 0.38f, a.sparkGlow);
            go.name = "Spark";
            go.SetActive(false);
            return go;
        }

        public static GameObject PowerUp(PowerUpKind kind, GameAssets a, Transform parent)
        {
            var t = Root("PowerUp_" + kind, parent);
            switch (kind)
            {
                case PowerUpKind.Magnet: // horseshoe: two prongs and a bar
                    Part(PrimitiveType.Cube, t, new Vector3(0, 0.0f, 0), new Vector3(0.8f, 0.2f, 0.2f), a.magnetGlow);
                    Part(PrimitiveType.Cube, t, new Vector3(-0.3f, 0.35f, 0), new Vector3(0.2f, 0.6f, 0.2f), a.magnetGlow);
                    Part(PrimitiveType.Cube, t, new Vector3(0.3f, 0.35f, 0), new Vector3(0.2f, 0.6f, 0.2f), a.magnetGlow);
                    break;
                case PowerUpKind.Shield: // bubble
                    Part(PrimitiveType.Sphere, t, Vector3.zero, Vector3.one * 0.8f, a.shieldGlow);
                    break;
                default: // stacked coins: double sparks
                    Part(PrimitiveType.Cylinder, t, new Vector3(-0.2f, 0, 0), new Vector3(0.55f, 0.06f, 0.55f), a.doubleGlow, new Vector3(90, 0, 0));
                    Part(PrimitiveType.Cylinder, t, new Vector3(0.2f, 0.15f, 0), new Vector3(0.55f, 0.06f, 0.55f), a.doubleGlow, new Vector3(90, 0, 0));
                    break;
            }
            t.gameObject.SetActive(false);
            return t.gameObject;
        }

        /// <summary>One 20 m piece of street: three lanes, lane lines and kerbs.</summary>
        public static GameObject GroundTile(GameAssets a, Transform parent, float length)
        {
            var t = Root("Ground", parent);
            Part(PrimitiveType.Cube, t, new Vector3(0, -0.05f, length * 0.5f), new Vector3(TrackRules.LaneWidth * 3 + 1f, 0.1f, length), a.ground);
            for (var i = 0; i < 2; i++)
            {
                var x = (i - 0.5f) * TrackRules.LaneWidth;
                for (var z = 1f; z < length; z += 4f)
                    Part(PrimitiveType.Cube, t, new Vector3(x, 0.005f, z), new Vector3(0.08f, 0.01f, 2f), a.laneLine);
            }
            Part(PrimitiveType.Cube, t, new Vector3(-4.4f, 0.1f, length * 0.5f), new Vector3(0.3f, 0.2f, length), a.surface);
            Part(PrimitiveType.Cube, t, new Vector3(4.4f, 0.1f, length * 0.5f), new Vector3(0.3f, 0.2f, length), a.surface);
            t.gameObject.SetActive(false);
            return t.gameObject;
        }

        public static GameObject Player(GameAssets a, Transform parent)
        {
            var t = Root("Courier", parent);
            var body = Root("Body", t);
            Part(PrimitiveType.Capsule, body, new Vector3(0, 0.9f, 0), new Vector3(0.7f, 0.9f, 0.6f), a.magenta);
            Part(PrimitiveType.Sphere, body, new Vector3(0, 1.62f, 0), Vector3.one * 0.42f, a.text);
            Part(PrimitiveType.Cube, body, new Vector3(0, 1.05f, -0.38f), new Vector3(0.55f, 0.6f, 0.3f), a.cyan); // parcel backpack
            Part(PrimitiveType.Cylinder, t, new Vector3(0, 0.01f, 0), new Vector3(0.9f, 0.005f, 0.9f), a.night); // blob shadow
            return t.gameObject;
        }
    }
}
