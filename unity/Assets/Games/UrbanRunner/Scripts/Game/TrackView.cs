using GameFactory.Core.Pooling;
using GameFactory.Core.World;
using UnityEngine;
using UrbanRunner.Sim;

namespace UrbanRunner.Game
{
    /// <summary>
    /// Renders the simulation's track with pooled objects. Each sim item has a stable id, mapped to a slot
    /// (id % slots), so visuals stay attached to the same item without dictionaries or per-frame allocations.
    /// Sparks are drawn with GPU instancing instead (hundreds on screen, two draws, no GameObjects).
    /// </summary>
    public sealed class TrackView : MonoBehaviour
    {
        const int RowSlots = 128;
        const int MaxSparks = 512;
        // A spark is a spinning diamond gem with a soft halo: big enough to read at speed (D28).
        static readonly Vector3 GemScale = new Vector3(0.42f, 0.42f, 0.14f);
        const float HaloSize = 1.4f;
        const int PowerUpSlots = 16;

        GameAssets _assets;
        ObjectPool<GameObject>[] _obstaclePools;
        ObjectPool<GameObject>[] _powerUpPools;

        readonly int[] _rowSlotId = new int[RowSlots];
        readonly GameObject[,] _rowSlot = new GameObject[RowSlots, TrackRules.LaneCount];
        readonly Obstacle[,] _rowSlotKind = new Obstacle[RowSlots, TrackRules.LaneCount];
        readonly Matrix4x4[] _sparkMatrices = new Matrix4x4[MaxSparks];
        readonly Matrix4x4[] _haloMatrices = new Matrix4x4[MaxSparks];
        Mesh _sparkMesh, _haloMesh;
        RenderParams _sparkParams, _haloParams;

        /// <summary>Sparks drawn last frame (tests and perf logs).</summary>
        public int VisibleSparks { get; private set; }
        readonly int[] _puSlotId = new int[PowerUpSlots];
        readonly GameObject[] _puSlot = new GameObject[PowerUpSlots];
        readonly PowerUpKind[] _puSlotKind = new PowerUpKind[PowerUpSlots];

        public int PoolGrowth
        {
            get
            {
                var g = 0;
                foreach (var p in _obstaclePools) if (p != null) g += p.GrowCount;
                foreach (var p in _powerUpPools) g += p.GrowCount;
                return g;
            }
        }

        public void Init(GameAssets assets)
        {
            _assets = assets;
            var obstacleKinds = System.Enum.GetValues(typeof(Obstacle));
            _obstaclePools = new ObjectPool<GameObject>[obstacleKinds.Length];
            foreach (Obstacle kind in obstacleKinds)
            {
                if (kind == Obstacle.None) continue;
                var k = kind;
                _obstaclePools[(int)k] = NewPool(() => VisualFactory.Obstacle(k, _assets, transform), 30);
            }
            _sparkMesh = BuiltinMesh(PrimitiveType.Cube);
            _haloMesh = BuiltinMesh(PrimitiveType.Quad);
            var bounds = new Bounds(Vector3.zero, new Vector3(40, 40, 2000)); // the whole visible street
            _sparkParams = new RenderParams(_assets.sparkGlow) { worldBounds = bounds, shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off, receiveShadows = false };
            _haloParams = new RenderParams(_assets.sparkHalo) { worldBounds = bounds, shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off, receiveShadows = false };
            _powerUpPools = new ObjectPool<GameObject>[3];
            for (var i = 0; i < 3; i++)
            {
                var k = (PowerUpKind)i;
                _powerUpPools[i] = NewPool(() => VisualFactory.PowerUp(k, _assets, transform), 3);
            }
            ResetSlots();
        }

        static Mesh BuiltinMesh(PrimitiveType t)
        {
            var g = GameObject.CreatePrimitive(t);
            var m = g.GetComponent<MeshFilter>().sharedMesh;
            Destroy(g);
            return m;
        }

        static ObjectPool<GameObject> NewPool(System.Func<GameObject> create, int prewarm)
        {
            var pool = new ObjectPool<GameObject>(create, g => g.SetActive(true), g => g.SetActive(false), prewarm);
            pool.Prewarm(prewarm);
            return pool;
        }

        readonly System.Collections.Generic.List<(GameObject go, int pool, bool powerUp)> _warm = new System.Collections.Generic.List<(GameObject, int, bool)>();

        /// <summary>
        /// Load-time shader warm-up (D31): shows one of every obstacle and power-up in front of the camera so the GPU
        /// driver compiles their shaders now, not the first time one appears mid-run (a visible hitch on phones).
        /// </summary>
        public void ShowAllForWarmup(bool show, Vector3 inFrontOfCamera)
        {
            if (show)
            {
                var i = 0;
                for (var k = 0; k < _obstaclePools.Length; k++)
                {
                    if (_obstaclePools[k] == null) continue;
                    var go = _obstaclePools[k].Get();
                    go.transform.localPosition = inFrontOfCamera + new Vector3((i % 3 - 1) * 2.5f, 0, 4f * (i / 3));
                    _warm.Add((go, k, false));
                    i++;
                }
                for (var k = 0; k < _powerUpPools.Length; k++)
                {
                    var go = _powerUpPools[k].Get();
                    go.transform.localPosition = inFrontOfCamera + new Vector3((k - 1) * 2.5f, 1f, -2f);
                    _warm.Add((go, k, true));
                }
                return;
            }
            foreach (var (go, pool, powerUp) in _warm)
            {
                if (powerUp) _powerUpPools[pool].Release(go);
                else _obstaclePools[pool].Release(go);
            }
            _warm.Clear();
        }

        /// <summary>Returns every visual to its pool (new session).</summary>
        public void ResetSlots()
        {
            for (var s = 0; s < RowSlots; s++)
            {
                for (var l = 0; l < TrackRules.LaneCount; l++) ReleaseObstacle(s, l);
                _rowSlotId[s] = -1;
            }
            VisibleSparks = 0;
            for (var s = 0; s < PowerUpSlots; s++)
            {
                if (_puSlot[s] != null) _powerUpPools[(int)_puSlotKind[s]].Release(_puSlot[s]);
                _puSlot[s] = null;
                _puSlotId[s] = -1;
            }
        }

        void ReleaseObstacle(int slot, int lane)
        {
            var go = _rowSlot[slot, lane];
            if (go == null) return;
            _obstaclePools[(int)_rowSlotKind[slot, lane]].Release(go);
            _rowSlot[slot, lane] = null;
        }

        public void Sync(RunSimulation sim, FloatingOrigin origin, float time)
        {
            SyncRows(sim, origin);
            SyncSparks(sim, origin, time);
            SyncPowerUps(sim, origin, time);
        }

        void SyncRows(RunSimulation sim, FloatingOrigin origin)
        {
            var rows = sim.Track.Rows;
            var firstId = rows.Count > 0 ? rows[0].Id : int.MaxValue;
            for (var s = 0; s < RowSlots; s++)
            {
                if (_rowSlotId[s] >= 0 && _rowSlotId[s] < firstId)
                {
                    for (var l = 0; l < TrackRules.LaneCount; l++) ReleaseObstacle(s, l);
                    _rowSlotId[s] = -1;
                }
            }
            for (var i = 0; i < rows.Count; i++)
            {
                ref var row = ref rows[i];
                var slot = row.Id % RowSlots;
                if (_rowSlotId[slot] != row.Id)
                {
                    for (var l = 0; l < TrackRules.LaneCount; l++) ReleaseObstacle(slot, l);
                    _rowSlotId[slot] = row.Id;
                    for (var l = 0; l < TrackRules.LaneCount; l++)
                    {
                        var o = row.Get(l);
                        if (o == Obstacle.None) continue;
                        _rowSlot[slot, l] = _obstaclePools[(int)o].Get();
                        _rowSlotKind[slot, l] = o;
                    }
                }
                for (var l = 0; l < TrackRules.LaneCount; l++)
                {
                    var go = _rowSlot[slot, l];
                    if (go == null) continue;
                    if (row.Effective(l) == Obstacle.None) { ReleaseObstacle(slot, l); continue; }
                    var z = row.Z;
                    if (_rowSlotKind[slot, l] == Obstacle.Cart && sim.Distance < row.Z)
                    {
                        // Rolls toward the player and reaches the row start exactly when the player does.
                        z = row.Z + (TrackRules.CartSpeed / sim.Speed) * (row.Z - sim.Distance);
                    }
                    go.transform.localPosition = new Vector3(TrackRules.LaneX(l), 0, origin.ToWorld(z));
                }
            }
        }

        void SyncSparks(RunSimulation sim, FloatingOrigin origin, float time)
        {
            var sparks = sim.Track.Sparks;
            var bob = Mathf.Sin(time * 4f) * 0.08f;
            var toWorld = transform.localToWorldMatrix;
            var spin = Quaternion.Euler(0, time * 160f, 45f); // turns about Y, stood on a corner
            var n = 0;
            for (var i = 0; i < sparks.Count && n < MaxSparks; i++)
            {
                ref var sp = ref sparks[i];
                if (sp.Collected) continue;
                var pos = new Vector3(TrackRules.LaneX(sp.Lane), sp.Y + bob, origin.ToWorld(sp.Z));
                if (sim.MagnetLeft > 0 && sp.Z - sim.Distance < 6 && Mathf.Abs(sp.Lane - sim.Lane) <= 1)
                    pos.x = Mathf.Lerp(pos.x, sim.X, Mathf.Clamp01(1f - (float)(sp.Z - sim.Distance) / 6f)); // visible pull
                _sparkMatrices[n] = toWorld * Matrix4x4.TRS(pos, spin, GemScale);
                // The halo sits just in front of the spark; additive, so overlapping halos simply brighten.
                _haloMatrices[n] = toWorld * Matrix4x4.TRS(pos + new Vector3(0, 0, -0.05f), Quaternion.identity, new Vector3(HaloSize, HaloSize, HaloSize));
                n++;
            }
            VisibleSparks = n;
        }

        /// <summary>Two instanced draws for every visible spark. Called every frame, also while paused.</summary>
        void LateUpdate() => DrawSparks();

        /// <summary>Queues the instanced spark draws for this frame's camera renders.</summary>
        public void DrawSparks()
        {
            if (VisibleSparks == 0 || _sparkMesh == null) return;
            Graphics.RenderMeshInstanced(_sparkParams, _sparkMesh, 0, _sparkMatrices, VisibleSparks);
            Graphics.RenderMeshInstanced(_haloParams, _haloMesh, 0, _haloMatrices, VisibleSparks);
        }

        void SyncPowerUps(RunSimulation sim, FloatingOrigin origin, float time)
        {
            var list = sim.Track.PowerUps;
            var firstId = list.Count > 0 ? list[0].Id : int.MaxValue;
            for (var s = 0; s < PowerUpSlots; s++)
            {
                if (_puSlot[s] != null && (_puSlotId[s] < firstId))
                {
                    _powerUpPools[(int)_puSlotKind[s]].Release(_puSlot[s]);
                    _puSlot[s] = null;
                    _puSlotId[s] = -1;
                }
            }
            for (var i = 0; i < list.Count; i++)
            {
                ref var p = ref list[i];
                var slot = p.Id % PowerUpSlots;
                if (p.Collected)
                {
                    if (_puSlot[slot] != null) _powerUpPools[(int)_puSlotKind[slot]].Release(_puSlot[slot]);
                    _puSlot[slot] = null;
                    _puSlotId[slot] = p.Id;
                    continue;
                }
                if (_puSlotId[slot] != p.Id)
                {
                    if (_puSlot[slot] != null) _powerUpPools[(int)_puSlotKind[slot]].Release(_puSlot[slot]);
                    _puSlot[slot] = _powerUpPools[(int)p.Kind].Get();
                    _puSlotKind[slot] = p.Kind;
                    _puSlotId[slot] = p.Id;
                }
                var t = _puSlot[slot].transform;
                t.localPosition = new Vector3(TrackRules.LaneX(p.Lane), 1.1f + Mathf.Sin(time * 3f) * 0.12f, origin.ToWorld(p.Z));
                t.localRotation = Quaternion.Euler(0, time * 120f, 0);
            }
        }

    }
}
