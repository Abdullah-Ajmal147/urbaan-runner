using GameFactory.Core.World;
using UnityEngine;
using UrbanRunner.Sim;

namespace UrbanRunner.Game
{
    /// <summary>
    /// Lantern City street: a ring of pre-built 26 m segments (road, pavements, buildings, awnings, neon signs,
    /// lantern strings with halos and wet-road reflections) recycled ahead of the runner. Built once at load;
    /// moving a segment is just a transform update, so nothing is created during a run.
    /// </summary>
    public sealed class EnvironmentView : MonoBehaviour
    {
        public const float SegmentLength = 26f;
        const int Segments = 8;
        const float BehindM = 26f;

        readonly Transform[] _segments = new Transform[Segments];
        GameAssets _a;
        System.Random _rng;

        public void Init(GameAssets assets)
        {
            _a = assets;
            _rng = new System.Random(20261005);
            for (var i = 0; i < Segments; i++) _segments[i] = BuildSegment(i);
        }

        public void Sync(double distance, FloatingOrigin origin)
        {
            var first = System.Math.Floor((distance - BehindM) / SegmentLength);
            for (var k = 0; k < Segments; k++)
            {
                var idx = (long)first + k;
                var slot = (int)(((idx % Segments) + Segments) % Segments);
                _segments[slot].localPosition = new Vector3(0, 0, origin.ToWorld(idx * SegmentLength));
            }
        }

        // ---------- construction (load time only) ----------

        GameObject Prim(PrimitiveType t, Transform parent, Vector3 pos, Vector3 scale, Material m, Vector3 euler = default)
        {
            var g = GameObject.CreatePrimitive(t);
            Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localScale = scale;
            g.transform.localEulerAngles = euler;
            var r = g.GetComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return g;
        }

        void Quad(Transform parent, Vector3 pos, Vector2 size, Material m, Vector3 euler) =>
            Prim(PrimitiveType.Quad, parent, pos, new Vector3(size.x, size.y, 1), m, euler);

        Transform BuildSegment(int index)
        {
            var seg = new GameObject("Segment" + index).transform;
            seg.SetParent(transform, false);
            const float L = SegmentLength;

            Prim(PrimitiveType.Cube, seg, new Vector3(0, -0.05f, L / 2), new Vector3(9.4f, 0.1f, L), _a.asphalt);
            Prim(PrimitiveType.Cube, seg, new Vector3(-6.2f, 0.08f, L / 2), new Vector3(3.2f, 0.16f, L), _a.pavement);
            Prim(PrimitiveType.Cube, seg, new Vector3(6.2f, 0.08f, L / 2), new Vector3(3.2f, 0.16f, L), _a.pavement);
            Prim(PrimitiveType.Cube, seg, new Vector3(-4.65f, 0.09f, L / 2), new Vector3(0.18f, 0.18f, L), _a.kerb);
            Prim(PrimitiveType.Cube, seg, new Vector3(4.65f, 0.09f, L / 2), new Vector3(0.18f, 0.18f, L), _a.kerb);
            for (var z = 1f; z < L; z += 4.33f)
            {
                Quad(seg, new Vector3(-TrackRules.LaneWidth / 2, 0.01f, z), new Vector2(0.1f, 2f), _a.laneLine, new Vector3(90, 0, 0));
                Quad(seg, new Vector3(TrackRules.LaneWidth / 2, 0.01f, z), new Vector2(0.1f, 2f), _a.laneLine, new Vector3(90, 0, 0));
            }

            // Buildings on both sides fill the segment.
            foreach (var side in new[] { -1, 1 })
            {
                var z = 0f;
                while (z < L - 2f)
                {
                    var prefab = _a.buildings[_rng.Next(_a.buildings.Length)];
                    var b = Instantiate(prefab, seg);
                    var bounds = new Bounds();
                    var first = true;
                    foreach (var r in b.GetComponentsInChildren<Renderer>())
                    {
                        r.sharedMaterial = _a.buildingToon;
                        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        if (first) { bounds = r.bounds; first = false; } else bounds.Encapsulate(r.bounds);
                    }
                    var footprint = Mathf.Max(bounds.size.x, bounds.size.z);
                    var scale = Mathf.Min(8.6f, L - z) / footprint;
                    b.transform.localScale = Vector3.one * scale;
                    b.transform.localRotation = Quaternion.Euler(0, side < 0 ? 90 : -90, 0);
                    var depth = footprint * scale;
                    b.transform.localPosition = new Vector3(side * (7.8f + depth * 0.5f), 0.16f, z + depth * 0.5f);
                    z += depth + 0.4f;
                }
            }

            // Market awnings over the pavement.
            var awnings = new[] { _a.awningMagenta, _a.awningCyan, _a.awningAmber };
            Prim(PrimitiveType.Cube, seg, new Vector3(-6.9f, 3.1f, 6f), new Vector3(1.8f, 0.12f, 4.5f), awnings[index % 3], new Vector3(0, 0, -14f));
            Prim(PrimitiveType.Cube, seg, new Vector3(6.9f, 3.1f, 17f), new Vector3(1.8f, 0.12f, 4.5f), awnings[(index + 1) % 3], new Vector3(0, 0, 14f));

            // One neon glyph sign per segment, alternating sides.
            var signSide = index % 2 == 0 ? -1 : 1;
            NeonSign(seg, new Vector3(signSide * 7.55f, 5.2f, 12f), signSide < 0 ? 90 : -90, index);

            // Lantern string across the street.
            LanternString(seg, 20f, index);

            // Merge the segment's static parts into batches (far fewer draw calls); the root can still move.
            StaticBatchingUtility.Combine(seg.gameObject);
            return seg;
        }

        void NeonSign(Transform parent, Vector3 pos, float yaw, int variant)
        {
            var tubes = new[] { _a.neonMagenta, _a.neonCyan, _a.neonAmber };
            var halos = new[] { _a.haloMagenta, _a.haloCyan, _a.haloAmber };
            var streaks = new[] { _a.streakMagenta, _a.streakCyan, _a.streakAmber };
            var tube = tubes[variant % 3];
            var s = new GameObject("NeonSign").transform;
            s.SetParent(parent, false);
            s.localPosition = pos;
            s.localEulerAngles = new Vector3(0, yaw, 0);
            Prim(PrimitiveType.Cube, s, new Vector3(0, 0, 0.06f), new Vector3(1.3f, 2.2f, 0.1f), _a.signBack);
            Prim(PrimitiveType.Cube, s, new Vector3(0, 1.05f, 0), new Vector3(1.2f, 0.07f, 0.06f), tube);
            Prim(PrimitiveType.Cube, s, new Vector3(0, -1.05f, 0), new Vector3(1.2f, 0.07f, 0.06f), tube);
            Prim(PrimitiveType.Cube, s, new Vector3(-0.6f, 0, 0), new Vector3(0.07f, 2.1f, 0.06f), tube);
            Prim(PrimitiveType.Cube, s, new Vector3(0.6f, 0, 0), new Vector3(0.07f, 2.1f, 0.06f), tube);
            // Invented glyphs: never real letters or brands (HANDOVER §3.1).
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
            Quad(s, new Vector3(0, 0, -0.08f), new Vector2(2.8f, 3.6f), halos[variant % 3], Vector3.zero);
            Quad(parent, new Vector3(pos.x * 0.8f, 0.02f, pos.z - 3.2f), new Vector2(1.1f, 7f), streaks[variant % 3], new Vector3(90, 0, 0));
        }

        void LanternString(Transform parent, float z, int seed)
        {
            const float halfW = 6.6f, top = 6.4f, sag = 1.1f;
            const int n = 9;
            var lanterns = new[] { _a.lanternAmber, _a.lanternMagenta, _a.lanternCyan };
            var halos = new[] { _a.haloAmber, _a.haloMagenta, _a.haloCyan };
            for (var i = 0; i < n; i++)
            {
                var t = i / (float)(n - 1);
                var x = Mathf.Lerp(-halfW, halfW, t);
                var y = top - sag * 4f * t * (1 - t);
                var k = (i + seed) % 3;
                var lantern = Prim(PrimitiveType.Sphere, parent, new Vector3(x, y - 0.35f, z), new Vector3(0.42f, 0.55f, 0.42f), lanterns[k]);
                Prim(PrimitiveType.Cylinder, lantern.transform, new Vector3(0, 0.55f, 0), new Vector3(0.5f, 0.08f, 0.5f), _a.lanternCap);
                Quad(parent, new Vector3(x, y - 0.35f, z - 0.05f), new Vector2(1.9f, 1.9f), halos[k], Vector3.zero);
            }
            var cord = new GameObject("Cord").AddComponent<LineRenderer>();
            cord.transform.SetParent(parent, false);
            cord.useWorldSpace = false;
            cord.positionCount = 16;
            for (var i = 0; i < 16; i++)
            {
                var t = i / 15f;
                cord.SetPosition(i, new Vector3(Mathf.Lerp(-halfW - 0.4f, halfW + 0.4f, t), top - sag * 4f * t * (1 - t), z));
            }
            cord.widthMultiplier = 0.035f;
            cord.sharedMaterial = _a.cord;
            cord.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
}
