using GameFactory.Core.World;
using UnityEngine;
using UrbanRunner.Sim;

namespace UrbanRunner.Game
{
    /// <summary>
    /// The Warden drone, the reason to run (D28). It hunts the courier for the first seconds of every run, then
    /// drops back out of view. A side bump brings it right behind the courier for <see cref="TrackRules.ChaseWindowS"/>;
    /// a second bump in that window and its searchlight pins the courier (caught).
    /// </summary>
    public sealed class ChaserView : MonoBehaviour
    {
        const float IntroS = 3f;
        // Hovers low, right behind the courier: from the chase camera it shows just below the runner, never over
        // the road ahead (negative test: hovering high hid the next obstacles), inside a portrait screen's width.
        const float CloseBehind = 2.6f, FarBehind = 16f;
        const float Height = 1.5f;
        const float Shoulder = 0f;
        const float Scale = 0.7f;

        Transform _drone;
        Transform[] _rotors;
        Transform _searchlight;
        Renderer _siren;
        Transform _spot;
        Material _sirenA, _sirenB;
        float _behind = FarBehind;
        float _x;
        float _bob;

        public void Init(GameAssets a)
        {
            _drone = VisualFactory.Warden(a, transform, out _rotors, out _searchlight, out _siren).transform;
            _drone.localScale = Vector3.one * Scale;
            // The searchlight's spot on the ground around the courier.
            var spot = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(spot.GetComponent<Collider>());
            spot.name = "SearchlightSpot";
            spot.transform.SetParent(transform, false);
            spot.transform.localEulerAngles = new Vector3(90, 0, 0);
            var sr = spot.GetComponent<MeshRenderer>();
            sr.sharedMaterial = a.haloAmber;
            sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _spot = spot.transform;
            _spot.gameObject.SetActive(false);
            _drone.gameObject.SetActive(true);
            _sirenA = a.danger;
            _sirenB = a.cyan;
            _drone.gameObject.SetActive(false);
        }

        /// <summary>How close the drone should be: 1 = right behind the courier, 0 = out of view.</summary>
        static float Closeness(RunSimulation sim)
        {
            if (sim.State != RunState.Running) return sim.Caught ? 1.25f : 0.6f;
            if (sim.ChaseLeft > 0f) return 1f;
            if (sim.Time < IntroS) return 1f;
            return 0f;
        }

        public void Sync(RunSimulation sim, FloatingOrigin origin, float dt, bool frozen)
        {
            var want = Closeness(sim);
            var target = Mathf.Lerp(FarBehind, CloseBehind, Mathf.Min(want, 1f)) - (want > 1f ? 2.2f : 0f);
            var visible = _behind < FarBehind - 0.5f || want > 0f;
            if (_drone.gameObject.activeSelf != visible) _drone.gameObject.SetActive(visible);
            if (_spot.gameObject.activeSelf != visible) _spot.gameObject.SetActive(visible);
            if (!visible) { _behind = FarBehind; return; }
            if (!frozen)
            {
                // Closes in fast, falls back slowly: it never gives up easily.
                var rate = target < _behind ? 6f : 1.6f;
                _behind = Mathf.MoveTowards(_behind, target, rate * dt * Mathf.Max(1f, Mathf.Abs(_behind - target)));
                // Shoulder side: toward the middle of the street, so it stays on screen.
                var side = sim.X > 0.1f ? -Shoulder : Shoulder;
                _x = Mathf.Lerp(_x, sim.X + side, 1f - Mathf.Exp(-dt * 3f));
                _bob += dt;
                for (var i = 0; i < _rotors.Length; i++) _rotors[i].Rotate(0, 1500f * dt, 0, Space.Self);
            }
            var z = origin.ToWorld(sim.Distance) - _behind;
            var y = sim.GroundY + Height + Mathf.Sin(_bob * 3.1f) * 0.12f;
            _drone.localPosition = new Vector3(_x, y, z);
            // Leans forward when chasing, tilts into turns, and aims its searchlight at the courier.
            var tiltX = Mathf.Clamp(_xVelHint(sim) * -6f, -20f, 20f);
            // Faces the courier: a hunter, not a passer-by.
            var yaw = Mathf.Atan2(sim.X - _x, _behind) * Mathf.Rad2Deg;
            _drone.localRotation = Quaternion.Euler(14f, yaw, tiltX);
            _searchlight.localRotation = Quaternion.Euler(Mathf.Lerp(30f, 58f, Mathf.InverseLerp(FarBehind, CloseBehind, _behind)), 0, 0);
            // Spot: tightens on the courier as the drone closes in.
            var close = Mathf.InverseLerp(FarBehind, CloseBehind, _behind);
            var spotSize = Mathf.Lerp(0.5f, 2.6f, close);
            _spot.localPosition = new Vector3(sim.X, sim.GroundY + 0.04f, origin.ToWorld(sim.Distance));
            _spot.localScale = new Vector3(spotSize, spotSize * 1.2f, 1f);
            var flash = Mathf.Repeat(Time.unscaledTime * 3f, 1f) < 0.5f;
            _siren.sharedMaterial = flash ? _sirenA : _sirenB;
        }

        float _lastX;
        float _xVelHint(RunSimulation sim)
        {
            var v = sim.X - _lastX;
            _lastX = sim.X;
            return v * 30f;
        }

        public void SnapAway()
        {
            _behind = FarBehind;
            if (_drone != null) _drone.gameObject.SetActive(false);
            if (_spot != null) _spot.gameObject.SetActive(false);
        }
    }
}
