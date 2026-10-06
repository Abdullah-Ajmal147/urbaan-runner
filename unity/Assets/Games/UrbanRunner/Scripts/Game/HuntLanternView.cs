using GameFactory.Core.World;
using UnityEngine;

namespace UrbanRunner.Game
{
    /// <summary>
    /// Today's Lantern Hunt lantern (D32): a big glowing paper lantern with a halo and a tall beam of light, so it
    /// can be spotted far down the street. One per run, in the colour the player still needs.
    /// </summary>
    public sealed class HuntLanternView : MonoBehaviour
    {
        Transform _root, _body;
        Renderer _bodyRenderer, _haloRenderer, _beamRenderer;
        GameAssets _a;
        ParticleSystem _burst;

        public void Init(GameAssets a)
        {
            _a = a;
            _root = new GameObject("HuntLantern").transform;
            _root.SetParent(transform, false);
            _body = Part(PrimitiveType.Sphere, _root, Vector3.zero, new Vector3(0.75f, 0.95f, 0.75f), a.lanternAmber, out _bodyRenderer).transform;
            Part(PrimitiveType.Cylinder, _body, new Vector3(0, 0.55f, 0), new Vector3(0.55f, 0.08f, 0.55f), a.lanternCap, out _);
            Part(PrimitiveType.Cylinder, _body, new Vector3(0, -0.55f, 0), new Vector3(0.42f, 0.06f, 0.42f), a.lanternCap, out _);
            Part(PrimitiveType.Cube, _body, new Vector3(0, -0.85f, 0), new Vector3(0.06f, 0.5f, 0.06f), a.lanternCap, out _); // tassel
            Part(PrimitiveType.Quad, _root, new Vector3(0, 0, -0.3f), Vector3.one * 3.4f, a.haloAmber, out _haloRenderer);
            Part(PrimitiveType.Quad, _root, new Vector3(0, 6f, 0.1f), new Vector3(0.9f, 14f, 1f), a.streakAmber, out _beamRenderer);

            var fx = new GameObject("HuntBurst");
            fx.transform.SetParent(transform, false);
            _burst = fx.AddComponent<ParticleSystem>();
            _burst.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = _burst.main;
            main.playOnAwake = false;
            main.loop = false;
            main.startLifetime = 0.9f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 9f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
            main.gravityModifier = 0.4f;
            main.maxParticles = 80;
            var em = _burst.emission;
            em.enabled = false;
            var sol = _burst.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0, 1, 1, 0));
            _burst.GetComponent<ParticleSystemRenderer>().sharedMaterial = a.haloAmber;
            _root.gameObject.SetActive(false);
        }

        static GameObject Part(PrimitiveType t, Transform parent, Vector3 pos, Vector3 scale, Material m, out Renderer r)
        {
            var g = GameObject.CreatePrimitive(t);
            Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localScale = scale;
            r = g.GetComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return g;
        }

        /// <summary>0 amber · 1 pink · 2 blue · 3 mint · 4 violet.</summary>
        public void SetColour(int colour)
        {
            var i = Mathf.Clamp(colour, 0, 4);
            _bodyRenderer.sharedMaterial = _a.huntBodies[i];
            _haloRenderer.sharedMaterial = _a.huntHalos[i];
            _beamRenderer.sharedMaterial = _a.huntBeams[i];
            _burst.GetComponent<ParticleSystemRenderer>().sharedMaterial = _a.huntHalos[i];
        }

        public void Sync(Sim.RunSimulation sim, FloatingOrigin origin, float time)
        {
            var h = sim.Track.Hunt;
            var show = h.Placed && !h.Collected && h.Z > sim.Distance - 2;
            if (_root.gameObject.activeSelf != show) _root.gameObject.SetActive(show);
            if (!show) return;
            _root.localPosition = new Vector3(Sim.TrackRules.LaneX(h.Lane), 1.3f + Mathf.Sin(time * 2.6f) * 0.15f, origin.ToWorld(h.Z));
            _body.localRotation = Quaternion.Euler(0, time * 70f, Mathf.Sin(time * 1.7f) * 6f);
        }

        /// <summary>A burst of light where the lantern was.</summary>
        public void Found()
        {
            _burst.transform.localPosition = _root.localPosition;
            _burst.Emit(60);
            _root.gameObject.SetActive(false);
        }
    }
}
