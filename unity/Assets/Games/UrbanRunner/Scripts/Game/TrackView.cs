using GameFactory.Core.Pooling;
using GameFactory.Core.World;
using UnityEngine;
using UrbanRunner.Sim;

namespace UrbanRunner.Game
{
    /// <summary>
    /// Renders the simulation's track with pooled objects. Each sim item has a stable id, mapped to a slot
    /// (id % slots), so visuals stay attached to the same item without dictionaries or per-frame allocations.
    /// </summary>
    public sealed class TrackView : MonoBehaviour
    {
        const int RowSlots = 128;
        const int SparkSlots = 512;
        const int PowerUpSlots = 16;

        GameAssets _assets;
        ObjectPool<GameObject>[] _obstaclePools;
        ObjectPool<GameObject> _sparkPool;
        ObjectPool<GameObject>[] _powerUpPools;

        readonly int[] _rowSlotId = new int[RowSlots];
        readonly GameObject[,] _rowSlot = new GameObject[RowSlots, TrackRules.LaneCount];
        readonly Obstacle[,] _rowSlotKind = new Obstacle[RowSlots, TrackRules.LaneCount];
        readonly int[] _sparkSlotId = new int[SparkSlots];
        readonly GameObject[] _sparkSlot = new GameObject[SparkSlots];
        readonly int[] _puSlotId = new int[PowerUpSlots];
        readonly GameObject[] _puSlot = new GameObject[PowerUpSlots];
        readonly PowerUpKind[] _puSlotKind = new PowerUpKind[PowerUpSlots];

        public int PoolGrowth
        {
            get
            {
                var g = _sparkPool.GrowCount;
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
            _sparkPool = NewPool(() => VisualFactory.Spark(_assets, transform), 160);
            _powerUpPools = new ObjectPool<GameObject>[3];
            for (var i = 0; i < 3; i++)
            {
                var k = (PowerUpKind)i;
                _powerUpPools[i] = NewPool(() => VisualFactory.PowerUp(k, _assets, transform), 3);
            }
            ResetSlots();
        }

        static ObjectPool<GameObject> NewPool(System.Func<GameObject> create, int prewarm)
        {
            var pool = new ObjectPool<GameObject>(create, g => g.SetActive(true), g => g.SetActive(false), prewarm);
            pool.Prewarm(prewarm);
            return pool;
        }

        /// <summary>Returns every visual to its pool (new session).</summary>
        public void ResetSlots()
        {
            for (var s = 0; s < RowSlots; s++)
            {
                for (var l = 0; l < TrackRules.LaneCount; l++) ReleaseObstacle(s, l);
                _rowSlotId[s] = -1;
            }
            for (var s = 0; s < SparkSlots; s++)
            {
                if (_sparkSlot[s] != null) _sparkPool.Release(_sparkSlot[s]);
                _sparkSlot[s] = null;
                _sparkSlotId[s] = -1;
            }
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
            var firstId = sparks.Count > 0 ? sparks[0].Id : int.MaxValue;
            for (var s = 0; s < SparkSlots; s++)
            {
                if (_sparkSlot[s] != null && _sparkSlotId[s] < firstId)
                {
                    _sparkPool.Release(_sparkSlot[s]);
                    _sparkSlot[s] = null;
                    _sparkSlotId[s] = -1;
                }
            }
            var bob = Mathf.Sin(time * 4f) * 0.08f;
            for (var i = 0; i < sparks.Count; i++)
            {
                ref var sp = ref sparks[i];
                var slot = sp.Id % SparkSlots;
                if (sp.Collected)
                {
                    // Whatever the slot holds (this spark, or an older one) is no longer visible.
                    if (_sparkSlot[slot] != null) _sparkPool.Release(_sparkSlot[slot]);
                    _sparkSlot[slot] = null;
                    _sparkSlotId[slot] = sp.Id;
                    continue;
                }
                if (_sparkSlotId[slot] != sp.Id)
                {
                    if (_sparkSlot[slot] != null) _sparkPool.Release(_sparkSlot[slot]);
                    _sparkSlot[slot] = _sparkPool.Get();
                    _sparkSlotId[slot] = sp.Id;
                }
                var go = _sparkSlot[slot];
                if (go == null) continue;
                var pos = new Vector3(TrackRules.LaneX(sp.Lane), sp.Y + bob, origin.ToWorld(sp.Z));
                if (sim.MagnetLeft > 0 && sp.Z - sim.Distance < 6 && Mathf.Abs(sp.Lane - sim.Lane) <= 1)
                    pos.x = Mathf.Lerp(pos.x, sim.X, Mathf.Clamp01(1f - (float)(sp.Z - sim.Distance) / 6f)); // visible pull
                go.transform.localPosition = pos;
            }
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
