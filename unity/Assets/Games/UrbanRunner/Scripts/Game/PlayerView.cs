using GameFactory.Core.World;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UrbanRunner.Sim;

namespace UrbanRunner.Game
{
    /// <summary>
    /// The courier (Kenney CC0 model + our skins), animated with a Playables mixer (run / jump / idle; slide is a
    /// procedural pose), plus the chase camera, contact shadow, shield glow and particle bursts.
    /// </summary>
    public sealed class PlayerView : MonoBehaviour
    {
        // High, pulled-back chase camera (D27): the courier is ~1/7 of the screen in the lower third,
        // all three lanes and ~70 m of street ahead stay readable.
        static readonly Vector3 CameraOffset = new Vector3(0, 5.4f, -7.9f);
        static readonly Vector3 LookAhead = new Vector3(0, 0.4f, 11f);
        const float CameraFollowX = 0.8f;

        /// <summary>The courier is scaled to this height whatever the source model's units are.</summary>
        public const float CourierHeight = 1.75f;

        GameAssets _a;
        Camera _camera;
        Transform _pivot;          // lean / slide pose
        Transform _courier;
        Renderer[] _renderers;
        Transform _shadow;
        Transform _shieldHalo;
        PlayableGraph _graph;
        AnimationMixerPlayable _mixer;
        AnimationClipPlayable _run, _jump, _idle;
        ParticleSystem _sparkFx, _powerFx, _crashFx, _dustFx;
        float _squash; // landing squash, 1 → 0 (D32 juice)

        /// <summary>The animated body (moves with jumps and slides): trails attach here.</summary>
        public Transform Body => _pivot;
        float _shake;
        float _camX;
        float _camXVel;
        float _slideBlend;
        bool _wasAirborne;
        float _fov;
        float _camY, _camYVel;

        public bool ReduceMotion { get; set; }
        public event System.Action Landed;

        public void Init(GameAssets assets, Camera cam)
        {
            _a = assets;
            _camera = cam;
            _fov = cam.fieldOfView;

            _pivot = new GameObject("Pivot").transform;
            _pivot.SetParent(transform, false);
            _courier = Instantiate(assets.courierModel, _pivot).transform;
            _courier.name = "Courier";
            _renderers = _courier.GetComponentsInChildren<Renderer>();
            foreach (var r in _renderers) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            SetCharacter("mira");

            // Clips bind bones relative to the skeleton's "Root" node.
            var root = _courier.Find("Root");
            var animator = root.gameObject.AddComponent<Animator>();
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            _graph = PlayableGraph.Create("Courier");
            _graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            _mixer = AnimationMixerPlayable.Create(_graph, 3);
            _run = AnimationClipPlayable.Create(_graph, assets.runClip);
            _jump = AnimationClipPlayable.Create(_graph, assets.jumpClip);
            _idle = AnimationClipPlayable.Create(_graph, assets.idleClip);
            _graph.Connect(_run, 0, _mixer, 0);
            _graph.Connect(_jump, 0, _mixer, 1);
            _graph.Connect(_idle, 0, _mixer, 2);
            _jump.Pause(); // driven by jump progress
            var output = AnimationPlayableOutput.Create(_graph, "Anim", animator);
            output.SetSourcePlayable(_mixer);
            _graph.Play();
            NormalizeHeight();

            _shadow = MakeQuad("BlobShadow", assets.blobShadow, new Vector2(1.4f, 1.1f), new Vector3(90, 0, 0)).transform;
            _shadow.SetParent(transform, false);
            _shadow.localPosition = new Vector3(0, 0.03f, 0);
            _shieldHalo = MakeQuad("ShieldHalo", assets.shieldHalo, new Vector2(2.6f, 3.2f), Vector3.zero).transform;
            _shieldHalo.SetParent(_pivot, false);
            _shieldHalo.localPosition = new Vector3(0, 1.1f, -0.2f);
            _shieldHalo.gameObject.SetActive(false);

            _sparkFx = Burst("SparkFx", assets.sparkHalo, 0.35f, 4.5f, 0.35f);
            _powerFx = Burst("PowerFx", assets.haloAmber, 0.7f, 6f, 0.5f);
            _crashFx = Burst("CrashFx", assets.haloMagenta, 0.6f, 7f, 0.6f);
            // Landing dust: a soft warm puff at the feet.
            _dustFx = Burst("DustFx", assets.haloAmber, 0.5f, 2.2f, 0.4f);
            _dustFx.transform.SetParent(transform, false);
            _dustFx.transform.localPosition = new Vector3(0, 0.1f, 0);
            var dustMain = _dustFx.main;
            dustMain.gravityModifier = -0.05f;
            var dustShape = _dustFx.shape;
            dustShape.shapeType = ParticleSystemShapeType.Circle;
            dustShape.radius = 0.35f;
            dustShape.rotation = new Vector3(90, 0, 0);
        }

        /// <summary>Scales the model to <see cref="CourierHeight"/>: measures the posed mesh in world space, so it works
        /// whatever the source file's units and axis conventions are.</summary>
        void NormalizeHeight()
        {
            _mixer.SetInputWeight(2, 1f); // idle pose
            _graph.Evaluate(0f);
            var minY = float.MaxValue;
            var maxY = float.MinValue;
            foreach (var r in _renderers)
            {
                if (r is SkinnedMeshRenderer s) s.updateWhenOffscreen = true; // bounds follow the evaluated pose
                var b = r.bounds; // world space: includes the file's unit scale
                if (b.min.y < minY) minY = b.min.y;
                if (b.max.y > maxY) maxY = b.max.y;
            }
            foreach (var r in _renderers) if (r is SkinnedMeshRenderer s2) s2.updateWhenOffscreen = false;
            var h = maxY - minY;
            if (h <= 0.01f || float.IsInfinity(h)) { Debug.LogWarning("UR_GAME courier height not measurable"); return; }
            _courier.localScale *= CourierHeight / h;
            Debug.Log($"UR_GAME courier source height {h:0.00} m -> {CourierHeight} m");
        }

        void OnDestroy()
        {
            if (_graph.IsValid()) _graph.Destroy();
        }

        static GameObject MakeQuad(string name, Material m, Vector2 size, Vector3 euler)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(q.GetComponent<Collider>());
            q.name = name;
            q.transform.localScale = new Vector3(size.x, size.y, 1);
            q.transform.localEulerAngles = euler;
            var r = q.GetComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return q;
        }

        ParticleSystem Burst(string name, Material mat, float size, float speed, float life)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_pivot, false);
            go.transform.localPosition = new Vector3(0, 1.1f, 0.3f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.startLifetime = life;
            main.startSpeed = speed;
            main.startSize = size;
            main.maxParticles = 64;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.gravityModifier = 0.6f;
            var em = ps.emission;
            em.enabled = false;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.3f;
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0));
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            return ps;
        }

        public void SetCharacter(string id)
        {
            var m = _a.CourierMaterial(id);
            foreach (var r in _renderers) r.sharedMaterial = m;
        }

        /// <summary>Short shake on crash; off with Reduce motion (HANDOVER §3.10).</summary>
        public void Shake() { if (!ReduceMotion) _shake = 0.35f; }

        public void OnEvents(in SimEvents e)
        {
            if (e.SparksCollected > 0) _sparkFx.Emit(7);
            if (e.Landed && !ReduceMotion) { _dustFx.Emit(10); _squash = 1f; }
            if (e.PowerUp) _powerFx.Emit(18);
            if (e.Crashed) { _crashFx.Emit(24); Shake(); }
        }

        /// <summary>Before any session: courier idle at the start line, camera in place.</summary>
        public void SnapIdle()
        {
            transform.localPosition = Vector3.zero;
            _mixer.SetInputWeight(0, 0f);
            _mixer.SetInputWeight(2, 1f);
            _graph.Evaluate(0f);
            _camX = 0f;
            PlaceCamera(0f, 0f);
        }

        public void SnapCamera(FloatingOrigin origin, RunSimulation sim)
        {
            _camX = sim.X * CameraFollowX;
            _camXVel = 0f;
            _camY = sim.GroundY;
            _camYVel = 0f;
            PlaceCamera(origin.ToWorld(sim.Distance), 0f);
        }

        public void Sync(RunSimulation sim, FloatingOrigin origin, float dt, bool frozen)
        {
            var z = origin.ToWorld(sim.Distance);
            transform.localPosition = new Vector3(sim.X, 0, z);

            // Animation weights: airborne → jump (scrubbed by progress), crashed → idle, else run (speed-synced).
            var crashed = sim.State != RunState.Running;
            var air = sim.Airborne ? 1f : 0f;
            var runW = crashed ? 0f : 1f - air;
            _mixer.SetInputWeight(0, runW);
            _mixer.SetInputWeight(1, crashed ? 0f : air);
            _mixer.SetInputWeight(2, crashed ? 1f : 0f);
            if (sim.Airborne) _jump.SetTime(Mathf.Lerp(0.15f, 0.85f, sim.JumpProgress) * _a.jumpClip.length);
            var runRate = Mathf.Clamp(sim.Speed / 9f, 0.9f, 1.8f);
            if (!frozen) _graph.Evaluate(crashed ? dt * 0.6f : dt * runRate);

            // Procedural slide: crouch + lean back; lane-change roll; running lean.
            _slideBlend = Mathf.MoveTowards(_slideBlend, sim.Sliding ? 1f : 0f, dt * 9f);
            var roll = Mathf.Clamp((TrackRules.LaneX(sim.Lane) - sim.X) * -6f, -14f, 14f);
            var pitch = crashed ? -18f : Mathf.Lerp(8f, -38f, _slideBlend);
            _pivot.localRotation = Quaternion.Euler(pitch, 0, roll);
            _pivot.localPosition = new Vector3(0, sim.Y - 0.45f * _slideBlend, 0);
            // Landing squash: wide and short for an instant, springing back (juice; off with Reduce motion).
            if (!frozen) _squash = Mathf.MoveTowards(_squash, 0f, dt * 7f);
            var sq = Mathf.Sin(_squash * Mathf.PI * 0.5f);
            _pivot.localScale = new Vector3(1f + 0.14f * sq, (1f - 0.28f * _slideBlend) * (1f - 0.18f * sq), 1f + 0.14f * sq);

            var blink = sim.InvulnerableLeft > 0f && Mathf.Repeat(Time.unscaledTime * 10f, 1f) < 0.5f;
            for (var i = 0; i < _renderers.Length; i++) _renderers[i].enabled = !blink;
            var shield = sim.ShieldLeft > 0f;
            if (_shieldHalo.gameObject.activeSelf != shield) _shieldHalo.gameObject.SetActive(shield);

            // Contact shadow stays on the surface below (street or roof) and shrinks with height.
            var shadowScale = Mathf.Lerp(1f, 0.55f, Mathf.Clamp01((sim.Y - sim.GroundY) / TrackRules.JumpHeight));
            _shadow.localPosition = new Vector3(0, sim.GroundY + 0.03f, 0);
            _shadow.localScale = new Vector3(1.4f * shadowScale, 1.1f * shadowScale, 1f);

            if (_wasAirborne && !sim.Airborne && !crashed) Landed?.Invoke();
            _wasAirborne = sim.Airborne;

            // Camera: follows the lane softly, widens a little with speed (sense of speed).
            _camX = Mathf.SmoothDamp(_camX, sim.X * CameraFollowX, ref _camXVel, 0.14f, Mathf.Infinity, dt);
            // Rises with the surface (street → roof) but ignores jumps, so the horizon stays calm.
            _camY = Mathf.SmoothDamp(_camY, sim.GroundY, ref _camYVel, 0.25f, Mathf.Infinity, dt);
            var speedT = Mathf.InverseLerp(sim.Config.StartSpeed, sim.Config.MaxSpeed, sim.Speed);
            _camera.fieldOfView = ReduceMotion ? _fov : _fov + 8f * speedT;
            PlaceCamera(z, dt);
        }

        void PlaceCamera(float z, float dt)
        {
            var pos = new Vector3(_camX, _camY, z) + CameraOffset;
            if (_shake > 0f)
            {
                _shake -= dt;
                pos += new Vector3(Mathf.Sin(Time.unscaledTime * 90f), Mathf.Cos(Time.unscaledTime * 77f), 0) * (0.3f * _shake);
            }
            _camera.transform.localPosition = pos;
            _camera.transform.LookAt(new Vector3(_camX * 0.7f, _camY, z) + LookAhead);
        }
    }
}
