using GameFactory.Core.World;
using UnityEngine;
using UrbanRunner.Sim;

namespace UrbanRunner.Game
{
    /// <summary>The courier and the chase camera, driven by the simulation each frame.</summary>
    public sealed class PlayerView : MonoBehaviour
    {
        Transform _body;
        Transform _shieldBubble;
        Camera _camera;
        float _shake;
        Vector3 _camVelocity;

        public bool ReduceMotion { get; set; }

        static readonly Vector3 CameraOffset = new Vector3(0, 3.6f, -6.2f);
        static readonly Quaternion CameraTilt = Quaternion.Euler(14f, 0, 0);

        public void Init(GameAssets assets, Camera cam)
        {
            var courier = VisualFactory.Player(assets, transform);
            _body = courier.transform.Find("Body");
            var bubble = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(bubble.GetComponent<Collider>());
            bubble.transform.SetParent(transform, false);
            bubble.transform.localPosition = new Vector3(0, 0.95f, 0);
            bubble.transform.localScale = new Vector3(1.4f, 2.1f, 1.4f);
            bubble.GetComponent<MeshRenderer>().sharedMaterial = assets.shieldGlow;
            _shieldBubble = bubble.transform;
            _shieldBubble.gameObject.SetActive(false);
            _camera = cam;
            _camera.transform.rotation = CameraTilt;
        }

        /// <summary>Short shake on crash; off with Reduce motion (HANDOVER §3.10).</summary>
        public void Shake() { if (!ReduceMotion) _shake = 0.35f; }

        public void Sync(RunSimulation sim, FloatingOrigin origin, float dt)
        {
            var z = origin.ToWorld(sim.Distance);
            transform.localPosition = new Vector3(sim.X, sim.Y, z);

            // Slide: squash; lane change: lean.
            var squash = sim.Sliding ? 0.5f : 1f;
            _body.localScale = new Vector3(1f, Mathf.Lerp(_body.localScale.y, squash, 1f - Mathf.Exp(-25f * dt)), 1f);
            var lean = Mathf.Clamp((TrackRules.LaneX(sim.Lane) - sim.X) * -8f, -15f, 15f);
            _body.localRotation = Quaternion.Euler(sim.Sliding ? -35f : 0f, 0, lean);

            var blink = sim.InvulnerableLeft > 0f && Mathf.Repeat(Time.unscaledTime * 10f, 1f) < 0.5f;
            _body.gameObject.SetActive(!blink);
            _shieldBubble.gameObject.SetActive(sim.ShieldLeft > 0f);

            // Camera follows lane smoothly, never the jump (keeps the horizon calm).
            var target = new Vector3(sim.X * 0.6f, 0, z) + CameraOffset;
            var cam = _camera.transform.localPosition;
            cam.x = Mathf.SmoothDamp(cam.x, target.x, ref _camVelocity.x, 0.12f, Mathf.Infinity, dt);
            cam.y = target.y;
            cam.z = target.z;
            if (_shake > 0f)
            {
                _shake -= dt;
                cam += new Vector3(Mathf.Sin(Time.unscaledTime * 90f), Mathf.Cos(Time.unscaledTime * 77f), 0) * (0.25f * _shake);
            }
            _camera.transform.localPosition = cam;
        }

        public void SnapCamera(FloatingOrigin origin, RunSimulation sim)
        {
            _camera.transform.localPosition = new Vector3(sim.X * 0.6f, 0, origin.ToWorld(sim.Distance)) + CameraOffset;
            _camVelocity = Vector3.zero;
        }
    }
}
