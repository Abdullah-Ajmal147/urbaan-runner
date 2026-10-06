using UnityEngine;

namespace UrbanRunner.Game
{
    /// <summary>
    /// The equipped trail, behind the courier (D32). Trails are sold in the Locker and the Starter Pack, so they
    /// must show in the run: Neon Ribbon (a pink-to-blue glowing ribbon), Paper Kite (a tail of tiny paper
    /// diamonds), Lantern (warm glowing orbs drifting up). Built once; switching is just enabling one of them.
    /// </summary>
    public sealed class TrailView : MonoBehaviour
    {
        TrailRenderer _ribbon;
        ParticleSystem _kites;
        ParticleSystem _lanterns;
        TrailRenderer _lanternGlow;
        string _current = "";

        public void Init(GameAssets a, Transform follow)
        {
            transform.SetParent(follow, false);
            transform.localPosition = new Vector3(0, 0.95f, -0.15f); // the courier's back, at the waist

            _ribbon = Ribbon("NeonRibbon", a.trailGlow, 0.62f, 0.45f,
                new Gradient
                {
                    colorKeys = new[] { new GradientColorKey(new Color(1f, 0.24f, 0.65f), 0f), new GradientColorKey(new Color(0.18f, 0.9f, 1f), 1f) },
                    alphaKeys = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) },
                });

            _kites = Particles("PaperKites", a.kiteToon, ParticleSystemRenderMode.Mesh, 0.22f, 0.7f, 14f);
            var kr = _kites.GetComponent<ParticleSystemRenderer>();
            kr.mesh = KiteMesh();
            var kMain = _kites.main;
            kMain.startColor = new ParticleSystem.MinMaxGradient(new Gradient
            {
                mode = GradientMode.Fixed,
                colorKeys = new[]
                {
                    new GradientColorKey(new Color(1f, 0.24f, 0.65f), 0.25f), new GradientColorKey(new Color(0.18f, 0.9f, 1f), 0.5f),
                    new GradientColorKey(new Color(1f, 0.71f, 0.28f), 0.75f), new GradientColorKey(new Color(0.24f, 1f, 0.64f), 1f),
                },
            }) { mode = ParticleSystemGradientMode.RandomColor };
            kMain.startRotation3D = true;
            kMain.startRotationX = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);
            kMain.startRotationY = new ParticleSystem.MinMaxCurve(-3.1f, 3.1f);
            kMain.startRotationZ = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);
            kMain.gravityModifier = 0.15f;

            _lanterns = Particles("LanternOrbs", a.haloAmber, ParticleSystemRenderMode.Billboard, 0.55f, 1.1f, 7f);
            var lMain = _lanterns.main;
            lMain.gravityModifier = -0.12f; // they float up
            _lanternGlow = Ribbon("LanternGlow", a.trailGlow, 0.22f, 0.25f,
                new Gradient
                {
                    colorKeys = new[] { new GradientColorKey(new Color(1f, 0.75f, 0.3f), 0f), new GradientColorKey(new Color(1f, 0.45f, 0.2f), 1f) },
                    alphaKeys = new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) },
                });
            Set("none");
        }

        TrailRenderer Ribbon(string name, Material m, float width, float time, Gradient colour)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var t = go.AddComponent<TrailRenderer>();
            t.sharedMaterial = m;
            t.time = time;
            t.minVertexDistance = 0.25f;
            t.widthMultiplier = width;
            t.widthCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);
            t.colorGradient = colour;
            t.alignment = LineAlignment.View;
            t.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            t.receiveShadows = false;
            t.emitting = false;
            return t;
        }

        ParticleSystem Particles(string name, Material m, ParticleSystemRenderMode mode, float size, float life, float perMetre)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.startLifetime = life;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.7f, size * 1.2f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 60;
            var em = ps.emission;
            em.rateOverTime = 0f;
            em.rateOverDistance = perMetre;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.15f;
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0, 1, 1, 0));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = m;
            r.renderMode = mode;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return ps;
        }

        /// <summary>A flat paper diamond (kite).</summary>
        static Mesh KiteMesh()
        {
            var m = new Mesh { name = "Kite" };
            m.vertices = new[] { new Vector3(0, 0.6f, 0), new Vector3(0.38f, 0, 0), new Vector3(0, -0.9f, 0), new Vector3(-0.38f, 0, 0) };
            m.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back };
            m.uv = new[] { new Vector2(0.5f, 1), new Vector2(1, 0.5f), new Vector2(0.5f, 0), new Vector2(0, 0.5f) };
            m.triangles = new[] { 0, 1, 2, 0, 2, 3, 0, 2, 1, 0, 3, 2 }; // both sides
            return m;
        }

        /// <summary>Shows the trail with this item id ("trail_neon", "trail_kite", "trail_lantern", or none).</summary>
        public void Set(string id)
        {
            _current = id ?? "";
            Emit(false);
            _ribbon.Clear();
            _lanternGlow.Clear();
            _kites.Clear();
            _lanterns.Clear();
        }

        /// <summary>Trails only while running (not at the start line, in menus or after a crash).</summary>
        public void Emit(bool on)
        {
            var neon = on && _current == "trail_neon";
            var kite = on && _current == "trail_kite";
            var lantern = on && _current == "trail_lantern";
            _ribbon.emitting = neon;
            _lanternGlow.emitting = lantern;
            Toggle(_kites, kite);
            Toggle(_lanterns, lantern);
        }

        static void Toggle(ParticleSystem ps, bool on)
        {
            if (on && !ps.isEmitting) ps.Play();
            else if (!on && ps.isEmitting) ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        }

        /// <summary>The floating origin moved the world: world-space trails would streak across the screen.</summary>
        public void OnOriginShift()
        {
            _ribbon.Clear();
            _lanternGlow.Clear();
            _kites.Clear();
            _lanterns.Clear();
        }
    }
}
