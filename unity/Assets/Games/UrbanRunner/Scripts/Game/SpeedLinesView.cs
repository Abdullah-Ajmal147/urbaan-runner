using UnityEngine;

namespace UrbanRunner.Game
{
    /// <summary>
    /// Wind streaks rushing past the camera at the screen edges, stronger as the run speeds up (D28). A fixed set of
    /// quads parented to the camera, recycled in place: no allocations. Off with Reduce motion.
    /// </summary>
    public sealed class SpeedLinesView : MonoBehaviour
    {
        const int Count = 18;
        const float Near = 1.2f, Far = 16f;

        readonly Transform[] _lines = new Transform[Count];
        readonly float[] _z = new float[Count];
        readonly Vector2[] _xy = new Vector2[Count];
        System.Random _rng;

        public bool ReduceMotion { get; set; }

        public void Init(GameAssets a, Camera cam)
        {
            transform.SetParent(cam.transform, false);
            _rng = new System.Random(77);
            var mats = new[] { a.streakCyan, a.streakAmber, a.streakMagenta };
            for (var i = 0; i < Count; i++)
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Destroy(q.GetComponent<Collider>());
                q.name = "SpeedLine" + i;
                q.transform.SetParent(transform, false);
                var r = q.GetComponent<MeshRenderer>();
                r.sharedMaterial = mats[i % mats.Length];
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _lines[i] = q.transform;
                Respawn(i, Random01() * (Far - Near) + Near);
            }
            gameObject.SetActive(false);
        }

        float Random01() => (float)_rng.NextDouble();

        void Respawn(int i, float z)
        {
            // Around the edges of the view, never across the runner in the middle.
            var angle = Random01() * Mathf.PI * 2f;
            var radius = 1.0f + Random01() * 0.9f;
            _xy[i] = new Vector2(Mathf.Cos(angle) * radius * 0.55f, Mathf.Sin(angle) * radius);
            _z[i] = z;
        }

        /// <param name="intensity">0 = no lines, 1 = top speed.</param>
        public void Sync(float intensity, float speed, float dt)
        {
            var on = !ReduceMotion && intensity > 0.05f;
            if (gameObject.activeSelf != on) gameObject.SetActive(on);
            if (!on) return;
            var len = Mathf.Lerp(1.2f, 4.5f, intensity);
            var width = Mathf.Lerp(0.03f, 0.075f, intensity);
            var visible = Mathf.RoundToInt(Count * intensity);
            for (var i = 0; i < Count; i++)
            {
                var t = _lines[i];
                var show = i < visible;
                if (t.gameObject.activeSelf != show) t.gameObject.SetActive(show);
                if (!show) continue;
                _z[i] -= speed * 1.6f * dt;
                if (_z[i] < Near) Respawn(i, Far);
                var p = _xy[i] * (_z[i] * 0.55f);
                t.localPosition = new Vector3(p.x, p.y, _z[i]);
                // Length along the view direction, face turned toward the view axis (so it's never edge-on).
                t.localRotation = Quaternion.LookRotation(new Vector3(_xy[i].x, _xy[i].y, 0f), Vector3.forward);
                t.localScale = new Vector3(width * _z[i], len, 1f);
            }
        }
    }
}
