using GameFactory.Core.World;
using UnityEngine;

namespace UrbanRunner.Game
{
    /// <summary>
    /// The festival in the sky (D29): paper lanterns drifting up behind the city, and fireworks bursting over the
    /// street on big moments ("Faster!", a new best). Everything is created at load and recycled: no allocations.
    /// </summary>
    public sealed class FestivalSkyView : MonoBehaviour
    {
        const int PerColour = 12, Colours = 2, LanternCount = PerColour * Colours;
        const float Ahead = 70f, Spread = 46f;

        // Drawn with GPU instancing: 24 lanterns, 4 draws (body + halo per colour), no GameObjects.
        readonly Matrix4x4[][] _bodies = { new Matrix4x4[PerColour], new Matrix4x4[PerColour] };
        readonly Matrix4x4[][] _halos = { new Matrix4x4[PerColour], new Matrix4x4[PerColour] };
        readonly Vector3[] _offset = new Vector3[LanternCount];
        readonly float[] _rise = new float[LanternCount];
        readonly float[] _sway = new float[LanternCount];
        readonly float[] _scale = new float[LanternCount];
        RenderParams[] _bodyParams, _haloParams;
        Mesh _cylinder, _quad;
        ParticleSystem[] _fireworks;
        int _nextFirework;
        System.Random _rng;
        float _time;
        bool _visible;

        public void Init(GameAssets a)
        {
            _rng = new System.Random(1005);
            _cylinder = Builtin(PrimitiveType.Cylinder);
            _quad = Builtin(PrimitiveType.Quad);
            var bounds = new Bounds(Vector3.zero, new Vector3(200, 200, 400));
            RenderParams P(Material m) => new RenderParams(m) { worldBounds = bounds, shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off, receiveShadows = false };
            _bodyParams = new[] { P(a.lanternAmber), P(a.lanternMagenta) };
            _haloParams = new[] { P(a.haloAmber), P(a.haloMagenta) };
            for (var i = 0; i < LanternCount; i++) Respawn(i, (float)_rng.NextDouble());

            _fireworks = new ParticleSystem[4];
            var mats = new[] { a.haloMagenta, a.haloCyan, a.haloAmber, a.sparkHalo };
            for (var i = 0; i < _fireworks.Length; i++) _fireworks[i] = Burst(mats[i]);
        }

        static Mesh Builtin(PrimitiveType t)
        {
            var g = GameObject.CreatePrimitive(t);
            var m = g.GetComponent<MeshFilter>().sharedMesh;
            Destroy(g);
            return m;
        }

        /// <summary>A lantern somewhere in the sky band beyond the rooftops; <paramref name="height01"/> 0 = just risen.</summary>
        void Respawn(int i, float height01)
        {
            var side = _rng.NextDouble() < 0.5 ? -1f : 1f;
            var x = side * (8f + (float)_rng.NextDouble() * (Spread - 8f));
            var z = Ahead * (0.35f + (float)_rng.NextDouble() * 0.65f);
            _offset[i] = new Vector3(x, 10f + height01 * 28f, z);
            _rise[i] = 0.6f + (float)_rng.NextDouble() * 0.9f;
            _sway[i] = (float)_rng.NextDouble() * 6.28f;
            _scale[i] = 0.6f + (float)_rng.NextDouble() * 0.7f;
        }

        ParticleSystem Burst(Material mat)
        {
            var go = new GameObject("Firework");
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, 1.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(7f, 11f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.9f, 1.5f);
            main.gravityModifier = 0.35f;
            main.maxParticles = 90;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission;
            em.enabled = false;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.2f;
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0, 1, 1, 0));
            var drag = ps.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.drag = 2.2f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            return ps;
        }

        /// <summary>Two or three bursts high over the street ahead of the courier.</summary>
        public void Celebrate(float worldZ, int bursts = 3)
        {
            for (var b = 0; b < bursts; b++)
            {
                var ps = _fireworks[_nextFirework];
                _nextFirework = (_nextFirework + 1) % _fireworks.Length;
                var x = ((float)_rng.NextDouble() - 0.5f) * 18f;
                ps.transform.localPosition = new Vector3(x, 15f + (float)_rng.NextDouble() * 7f, worldZ + 38f + b * 7f);
                ps.Emit(80);
            }
        }

        /// <summary>Lanterns travel with the courier (they're "far away") and rise slowly.</summary>
        public void Sync(FloatingOrigin origin, double distance, float dt, bool frozen)
        {
            _visible = true;
            if (!frozen) _time += dt;
            var z0 = origin.ToWorld(distance);
            var toWorld = transform.localToWorldMatrix;
            for (var i = 0; i < LanternCount; i++)
            {
                if (!frozen)
                {
                    _offset[i].y += _rise[i] * dt;
                    if (_offset[i].y > 40f) Respawn(i, 0f);
                }
                var sway = Mathf.Sin(_time * 0.7f + _sway[i]) * 0.8f;
                var p = new Vector3(_offset[i].x + sway, _offset[i].y, z0 + _offset[i].z);
                var s = _scale[i];
                var g = i % Colours;
                var k = i / Colours;
                _bodies[g][k] = toWorld * Matrix4x4.TRS(p, Quaternion.identity, new Vector3(0.9f * s, 0.6f * s, 0.9f * s));
                _halos[g][k] = toWorld * Matrix4x4.TRS(p + new Vector3(0, 0, -0.6f), Quaternion.identity, new Vector3(4.2f * s, 4.2f * s, 1f));
            }
        }

        void LateUpdate()
        {
            if (!_visible) return;
            for (var g = 0; g < Colours; g++)
            {
                Graphics.RenderMeshInstanced(_bodyParams[g], _cylinder, 0, _bodies[g], PerColour);
                Graphics.RenderMeshInstanced(_haloParams[g], _quad, 0, _halos[g], PerColour);
            }
        }
    }
}
