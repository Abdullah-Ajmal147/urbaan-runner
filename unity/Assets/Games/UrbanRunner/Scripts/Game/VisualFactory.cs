using GameFactory.Core.Rendering;
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
            // Editor tools (marketing renders) build these outside play mode, where Destroy isn't allowed.
            if (Application.isPlaying) Object.Destroy(go.GetComponent<Collider>());
            else Object.DestroyImmediate(go.GetComponent<Collider>());
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

        // One merged mesh per obstacle look, shared by every pooled copy (memory: D29).
        static readonly System.Collections.Generic.Dictionary<string, (Mesh mesh, Material[] mats)> Merged =
            new System.Collections.Generic.Dictionary<string, (Mesh, Material[])>();
        static GameAssets _cachedFor;

        /// <summary>Visual origin is the obstacle's front edge (z = row start), growing toward +z.</summary>
        public static GameObject Obstacle(Obstacle kind, GameAssets a, Transform parent)
        {
            if (_cachedFor != a) { Merged.Clear(); _cachedFor = a; }
            var isTram = kind == Sim.Obstacle.Tram || kind == Sim.Obstacle.TramRamp;
            var key = isTram ? kind + "#" + (_tramVariant % Mathf.Max(1, a.tramPaints?.Length ?? 1)) : kind.ToString();
            if (Merged.TryGetValue(key, out var cached) && cached.mesh != null)
            {
                if (isTram) _tramVariant++;
                var copy = Root(kind.ToString(), parent);
                var mgo = new GameObject(kind + "Mesh");
                mgo.transform.SetParent(copy, false);
                mgo.AddComponent<MeshFilter>().sharedMesh = cached.mesh;
                var mr = mgo.AddComponent<MeshRenderer>();
                mr.sharedMaterials = cached.mats;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                copy.gameObject.SetActive(false);
                return copy.gameObject;
            }
            var t = Root(kind.ToString(), parent);
            var len = TrackRules.Length(kind);
            switch (kind)
            {
                case Sim.Obstacle.Crate: // two stacked low crates: jump
                    BlobShadow(t, a, 2.0f, len);
                    Part(PrimitiveType.Cube, t, new Vector3(0, 0.23f, 0.5f), new Vector3(1.8f, 0.45f, 1.0f), a.wood);
                    var top = Part(PrimitiveType.Cube, t, new Vector3(0.1f, 0.68f, 0.5f), new Vector3(1.4f, 0.44f, 0.9f), a.amber, new Vector3(0, 6, 0)).transform;
                    // Festival gift crates: a ribbon cross and a bow on the top one.
                    Part(PrimitiveType.Cube, top, new Vector3(0, 0.02f, 0), new Vector3(0.14f, 1.04f, 1.03f), a.magenta);
                    Part(PrimitiveType.Cube, top, new Vector3(0, 0.02f, 0), new Vector3(1.02f, 1.04f, 0.16f), a.magenta);
                    Part(PrimitiveType.Sphere, top, new Vector3(-0.09f, 0.6f, 0), new Vector3(0.16f, 0.22f, 0.22f), a.magenta);
                    Part(PrimitiveType.Sphere, top, new Vector3(0.09f, 0.6f, 0), new Vector3(0.16f, 0.22f, 0.22f), a.magenta);
                    Part(PrimitiveType.Cube, t, new Vector3(0, 0.23f, 0.5f), new Vector3(1.82f, 0.1f, 1.02f), a.cyan); // band on the base crate
                    break;
                case Sim.Obstacle.Banner: // hanging sign between two poles: slide
                    Part(PrimitiveType.Cylinder, t, new Vector3(-1.05f, 1.5f, 0.15f), new Vector3(0.12f, 1.5f, 0.12f), a.surface);
                    Part(PrimitiveType.Cylinder, t, new Vector3(1.05f, 1.5f, 0.15f), new Vector3(0.12f, 1.5f, 0.12f), a.surface);
                    Part(PrimitiveType.Cube, t, new Vector3(0, 1.55f, 0.15f), new Vector3(2.1f, 1.1f, 0.12f), a.magenta);
                    // invented glyphs, never real words (§3.1)
                    Part(PrimitiveType.Cube, t, new Vector3(-0.5f, 1.6f, 0.08f), new Vector3(0.35f, 0.5f, 0.02f), a.text, new Vector3(0, 0, 20));
                    Part(PrimitiveType.Cube, t, new Vector3(0.1f, 1.6f, 0.08f), new Vector3(0.12f, 0.6f, 0.02f), a.text);
                    Part(PrimitiveType.Cube, t, new Vector3(0.55f, 1.6f, 0.08f), new Vector3(0.4f, 0.12f, 0.02f), a.text);
                    // Fabric fringe along the bottom edge (the part you slide under).
                    for (var i = 0; i < 9; i++)
                        Part(PrimitiveType.Cube, t, new Vector3(-0.96f + i * 0.24f, 0.93f, 0.15f), new Vector3(0.1f, 0.18f, 0.05f), i % 2 == 0 ? a.amber : a.cyan);
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
                case Sim.Obstacle.Tram: // parked Lantern City tram: change lane, or run along its roof
                    Tram(t, a, 0f);
                    break;
                case Sim.Obstacle.TramRamp: // boarding ramp + tram: run straight up onto the roof
                    BlobShadow(t, a, 2.2f, TrackRules.RampLength);
                    var rampLen = Mathf.Sqrt(TrackRules.RampLength * TrackRules.RampLength + TrackRules.TramHeight * TrackRules.TramHeight);
                    var angle = Mathf.Atan2(TrackRules.TramHeight, TrackRules.RampLength) * Mathf.Rad2Deg;
                    var ramp = Root("Ramp", t);
                    ramp.localPosition = new Vector3(0, TrackRules.TramHeight * 0.5f - 0.06f, TrackRules.RampLength * 0.5f);
                    ramp.localEulerAngles = new Vector3(-angle, 0, 0);
                    Part(PrimitiveType.Cube, ramp, Vector3.zero, new Vector3(1.9f, 0.12f, rampLen), a.wood);
                    for (var i = 0; i < 4; i++) // warning chevrons along the plank
                        Part(PrimitiveType.Cube, ramp, new Vector3(0, 0.07f, -rampLen * 0.38f + i * rampLen * 0.25f), new Vector3(1.5f, 0.02f, 0.22f), a.amber);
                    Part(PrimitiveType.Cube, t, new Vector3(-0.92f, 0.5f, 0.6f), new Vector3(0.08f, 1.0f, 0.08f), a.text); // handrail posts
                    Part(PrimitiveType.Cube, t, new Vector3(0.92f, 0.5f, 0.6f), new Vector3(0.08f, 1.0f, 0.08f), a.text);
                    Tram(t, a, TrackRules.RampLength);
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
            // Dozens of parts → a few draws: colours baked into one mesh (D29).
            var merged = MeshMerge.CombineByMaterial(t.gameObject, kind + "Mesh");
            if (merged != null && Application.isPlaying)
                Merged[key] = (merged.GetComponent<MeshFilter>().sharedMesh, merged.GetComponent<MeshRenderer>().sharedMaterials);
            t.gameObject.SetActive(false);
            return t.gameObject;
        }

        static int _tramVariant;

        /// <summary>A 10 m tram starting at <paramref name="z0"/>: painted body, lit windows, headlights, roof vents.</summary>
        static void Tram(Transform t, GameAssets a, float z0)
        {
            var paints = a.tramPaints != null && a.tramPaints.Length > 0 ? a.tramPaints : new[] { a.vanBody };
            var paint = paints[_tramVariant++ % paints.Length];
            const float W = 2.15f, Hh = TrackRules.TramHeight, L = TrackRules.TramLength;
            var mid = z0 + L * 0.5f;
            BlobShadow(t, a, 2.4f, L);
            var tram = Root("Tram", t);
            tram.localPosition = new Vector3(0, 0, z0);
            // Body: coloured lower skirt, cream upper, darker roof deck you can run on.
            Part(PrimitiveType.Cube, tram, new Vector3(0, 0.75f, L * 0.5f), new Vector3(W, 1.0f, L), paint);
            Part(PrimitiveType.Cube, tram, new Vector3(0, 1.75f, L * 0.5f), new Vector3(W, 1.0f, L), a.tramCream);
            Part(PrimitiveType.Cube, tram, new Vector3(0, Hh - 0.08f, L * 0.5f), new Vector3(W + 0.06f, 0.16f, L + 0.06f), a.tramRoof);
            // Window band: warm lit interior behind dark frames (both sides + front).
            Part(PrimitiveType.Cube, tram, new Vector3(0, 1.72f, L * 0.5f), new Vector3(W + 0.02f, 0.62f, L - 1.4f), a.tramWindow);
            for (var i = 1; i < 6; i++)
                Part(PrimitiveType.Cube, tram, new Vector3(0, 1.72f, 0.7f + i * (L - 1.4f) / 6f), new Vector3(W + 0.04f, 0.66f, 0.12f), a.tramCream);
            Part(PrimitiveType.Cube, tram, new Vector3(0, 1.75f, -0.01f), new Vector3(1.6f, 0.65f, 0.04f), a.tramWindow);
            // Front: bumper stripe, two headlights, a destination board with invented glyphs (§3.1).
            Part(PrimitiveType.Cube, tram, new Vector3(0, 0.38f, -0.03f), new Vector3(W, 0.22f, 0.08f), a.night);
            Part(PrimitiveType.Sphere, tram, new Vector3(-0.72f, 0.9f, -0.04f), new Vector3(0.26f, 0.26f, 0.1f), a.tramWindow);
            Part(PrimitiveType.Sphere, tram, new Vector3(0.72f, 0.9f, -0.04f), new Vector3(0.26f, 0.26f, 0.1f), a.tramWindow);
            Part(PrimitiveType.Quad, tram, new Vector3(-0.72f, 0.9f, -0.12f), Vector3.one * 1.1f, a.haloAmber);
            Part(PrimitiveType.Quad, tram, new Vector3(0.72f, 0.9f, -0.12f), Vector3.one * 1.1f, a.haloAmber);
            Part(PrimitiveType.Cube, tram, new Vector3(0, 2.2f, -0.03f), new Vector3(1.2f, 0.24f, 0.05f), a.night);
            Part(PrimitiveType.Cube, tram, new Vector3(-0.3f, 2.2f, -0.06f), new Vector3(0.3f, 0.08f, 0.02f), a.neonAmber);
            Part(PrimitiveType.Cube, tram, new Vector3(0.2f, 2.2f, -0.06f), new Vector3(0.08f, 0.16f, 0.02f), a.neonAmber);
            // Wheels (bogies) and roof vents kept low so the roof reads as a running deck.
            foreach (var z in new[] { 1.6f, L - 1.6f })
                Part(PrimitiveType.Cube, tram, new Vector3(0, 0.2f, z), new Vector3(W - 0.3f, 0.35f, 1.6f), a.night);
            Part(PrimitiveType.Cube, tram, new Vector3(0, Hh + 0.04f, L * 0.3f), new Vector3(0.9f, 0.1f, 1.2f), a.tramCream);
            Part(PrimitiveType.Cube, tram, new Vector3(0, Hh + 0.04f, L * 0.75f), new Vector3(0.9f, 0.1f, 1.2f), a.tramCream);
        }

        /// <summary>The Warden: a patrol drone with a siren and a searchlight. Origin at its body centre.</summary>
        public static GameObject Warden(GameAssets a, Transform parent, out Transform[] rotors, out Transform searchlight, out Renderer siren)
        {
            var t = Root("Warden", parent);
            Part(PrimitiveType.Sphere, t, Vector3.zero, new Vector3(1.1f, 0.42f, 1.0f), a.night);
            Part(PrimitiveType.Cube, t, new Vector3(0, 0.02f, 0), new Vector3(1.14f, 0.08f, 0.5f), a.magenta); // livery stripe
            Part(PrimitiveType.Sphere, t, new Vector3(0, -0.06f, 0.46f), new Vector3(0.34f, 0.24f, 0.2f), a.sparkGlow); // eye
            var s = Part(PrimitiveType.Sphere, t, new Vector3(0, 0.24f, 0), new Vector3(0.22f, 0.16f, 0.22f), a.danger);
            siren = s.GetComponent<Renderer>();
            rotors = new Transform[4];
            for (var i = 0; i < 4; i++)
            {
                var dir = Quaternion.Euler(0, 45 + 90 * i, 0) * Vector3.forward;
                var arm = Part(PrimitiveType.Cube, t, dir * 0.55f, new Vector3(0.1f, 0.06f, 0.6f), a.surface, new Vector3(0, 45 + 90 * i, 0));
                var hub = Root("Rotor" + i, t);
                hub.localPosition = dir * 0.85f + Vector3.up * 0.1f;
                Part(PrimitiveType.Cylinder, hub, Vector3.zero, new Vector3(0.62f, 0.01f, 0.62f), a.haloCyan);
                Part(PrimitiveType.Cube, hub, Vector3.zero, new Vector3(0.6f, 0.02f, 0.06f), a.text);
                rotors[i] = hub;
                _ = arm;
            }
            // Searchlight: a long soft beam toward the ground in front of the drone.
            searchlight = Root("Searchlight", t);
            searchlight.localPosition = new Vector3(0, -0.15f, 0.4f);
            Part(PrimitiveType.Quad, searchlight, new Vector3(0, 0, 1.6f), new Vector3(1.4f, 3.4f, 1f), a.streakAmber, new Vector3(90, 0, 0));
            return t.gameObject;
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
            Part(PrimitiveType.Quad, t, new Vector3(0, 0.1f, -0.3f), Vector3.one * 2.2f, kind == PowerUpKind.Magnet ? a.haloMagenta : kind == PowerUpKind.Shield ? a.shieldHalo : a.haloAmber);
            t.gameObject.SetActive(false);
            return t.gameObject;
        }


    }
}
