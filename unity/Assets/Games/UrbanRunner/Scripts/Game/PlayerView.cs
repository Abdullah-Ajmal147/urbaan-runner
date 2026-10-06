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
        static readonly Vector3 CameraOffset = new Vector3(0, 4.6f, -7.6f);
        static readonly Vector3 LookAhead = new Vector3(0, 0.6f, 12f);

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
        ParticleSystem _sparkFx, _powerFx, _crashFx;
        float _shake;
        float _camX;
        float _camXVel;
        float _slideBlend;
        bool _wasAirborne;
        float _fov;

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
            if (e.SparksCollected > 0) _sparkFx.Emit(4);
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
            _camX = sim.X * 0.6f;
            _camXVel = 0f;
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
            _pivot.localScale = new Vector3(1f, 1f - 0.28f * _slideBlend, 1f);

            var blink = sim.InvulnerableLeft > 0f && Mathf.Repeat(Time.unscaledTime * 10f, 1f) < 0.5f;
            for (var i = 0; i < _renderers.Length; i++) _renderers[i].enabled = !blink;
            var shield = sim.ShieldLeft > 0f;
            if (_shieldHalo.gameObject.activeSelf != shield) _shieldHalo.gameObject.SetActive(shield);

            var shadowScale = Mathf.Lerp(1f, 0.55f, sim.Y / TrackRules.JumpHeight);
            _shadow.localScale = new Vector3(1.4f * shadowScale, 1.1f * shadowScale, 1f);

            if (_wasAirborne && !sim.Airborne && !crashed) Landed?.Invoke();
            _wasAirborne = sim.Airborne;

            // Camera: follows the lane softly, widens a little with speed (sense of speed).
            _camX = Mathf.SmoothDamp(_camX, sim.X * 0.6f, ref _camXVel, 0.12f, Mathf.Infinity, dt);
            var speedT = Mathf.InverseLerp(sim.Config.StartSpeed, sim.Config.MaxSpeed, sim.Speed);
            _camera.fieldOfView = ReduceMotion ? _fov : _fov + 8f * speedT;
            PlaceCamera(z, dt);
        }

        void PlaceCamera(float z, float dt)
        {
            var pos = new Vector3(_camX, 0, z) + CameraOffset;
            if (_shake > 0f)
            {
                _shake -= dt;
                pos += new Vector3(Mathf.Sin(Time.unscaledTime * 90f), Mathf.Cos(Time.unscaledTime * 77f), 0) * (0.3f * _shake);
            }
            _camera.transform.localPosition = pos;
            _camera.transform.LookAt(new Vector3(_camX * 0.5f, 0, z) + LookAhead);
        }
    }
}
