using System;
using System.Collections.Generic;
using GameFactory.Core.Random;

namespace UrbanRunner.Sim
{
    /// <summary>
    /// Seeded track generator (HANDOVER §3.4). Picks chunk templates by tier with a PCG32 RNG, so a seed
    /// reproduces a run exactly. Gaps are placed in time, not metres: the player always gets
    /// at least gapBeats × <see cref="TrackRules.BeatS"/> seconds between the end of one row and the start of the next,
    /// using the exact distance↔time relation of the speed curve.
    /// </summary>
    public sealed class TrackGenerator
    {
        public const double RunInM = 40.0;

        struct ParsedRow
        {
            public Obstacle A, B, C;
            public int Gap;
            public bool Sparks;
        }

        struct ParsedChunk
        {
            public int Tier;
            public float Weight;
            public ParsedRow[] Rows;
        }

        readonly ParsedChunk[] _chunks;
        readonly RunConfig _config;
        SeededRandom _rng;

        int _chunk = -1;
        int _row;
        bool _mirror;
        double _prevEnd;
        double _nextPowerUpZ;
        int _nextId;
        int _nextSparkId;
        int _nextPowerUpId;

        public RingBuffer<TrackRow> Rows { get; } = new RingBuffer<TrackRow>(96);
        public RingBuffer<Spark> Sparks { get; } = new RingBuffer<Spark>(384);
        public RingBuffer<PowerUpPickup> PowerUps { get; } = new RingBuffer<PowerUpPickup>(8);

        /// <summary>Absolute distance up to which the track has been generated.</summary>
        public double GeneratedTo => _prevEnd;
        public int RowsGenerated => _nextId;

        public TrackGenerator(IReadOnlyList<ChunkTemplate> templates, RunConfig config, ulong seed)
        {
            if (templates == null || templates.Count == 0) throw new ArgumentException("no chunk templates");
            _config = config;
            _rng = new SeededRandom(seed);
            _chunks = new ParsedChunk[templates.Count];
            for (var i = 0; i < templates.Count; i++)
            {
                var t = templates[i];
                var rows = new ParsedRow[t.rows.Count];
                for (var r = 0; r < rows.Length; r++)
                {
                    var src = t.rows[r];
                    rows[r] = new ParsedRow
                    {
                        A = ChunkTemplate.Parse(src.lanes[0]),
                        B = ChunkTemplate.Parse(src.lanes[1]),
                        C = ChunkTemplate.Parse(src.lanes[2]),
                        Gap = Math.Max(1, src.gapBeats),
                        Sparks = src.sparks,
                    };
                }
                _chunks[i] = new ParsedChunk { Tier = t.tier, Weight = t.weight, Rows = rows };
            }
            _prevEnd = RunInM;
            _nextPowerUpZ = _rng.Range(PowerUpRules.MinSpacingM, PowerUpRules.MaxSpacingM);
        }

        /// <summary>Generate rows until the track reaches <paramref name="z"/>.</summary>
        public void GenerateUntil(double z)
        {
            while (_prevEnd < z) EmitRow();
        }

        /// <summary>Drops everything that is fully behind <paramref name="z"/>.</summary>
        public void Recycle(double z)
        {
            while (Rows.Count > 0 && Rows.Front.End < z) Rows.PopFront();
            while (Sparks.Count > 0 && Sparks.Front.Z < z) Sparks.PopFront();
            while (PowerUps.Count > 0 && PowerUps.Front.Z < z) PowerUps.PopFront();
        }

        /// <summary>Distance reached <paramref name="seconds"/> after passing <paramref name="from"/>.</summary>
        double AdvanceByTime(double from, double seconds) =>
            DifficultyCurve.DistanceAt(_config, DifficultyCurve.TimeAt(_config, from) + seconds);

        void EmitRow()
        {
            if (_chunk < 0 || _row >= _chunks[_chunk].Rows.Length) PickChunk();
            var src = _chunks[_chunk].Rows[_row++];

            var gapStart = _prevEnd;
            var z = AdvanceByTime(gapStart, src.Gap * TrackRules.BeatS);

            var row = new TrackRow
            {
                Z = z,
                L0 = _mirror ? src.C : src.A,
                L1 = src.B,
                L2 = _mirror ? src.A : src.C,
                Tier = DifficultyCurve.Tier(z),
                Id = _nextId++,
            };
            Rows.Push(row);

            if (src.Sparks) PlaceSparks(gapStart, row);
            // Targets that fell inside the previous row land at the start of this gap.
            if (_nextPowerUpZ < z) PlacePowerUp(gapStart, row);

            _prevEnd = row.End;
        }

        void PickChunk()
        {
            var tier = DifficultyCurve.Tier(_prevEnd);
            float total = 0;
            for (var i = 0; i < _chunks.Length; i++) total += ChunkWeight(i, tier);
            var pick = _rng.Value() * total;
            var chosen = 0;
            for (var i = 0; i < _chunks.Length; i++)
            {
                var w = ChunkWeight(i, tier);
                if (w <= 0) continue;
                chosen = i;
                if (pick < w) break;
                pick -= w;
            }
            _chunk = chosen;
            _row = 0;
            _mirror = _rng.Chance(0.5f);
        }

        /// <summary>Only unlocked tiers; the current tier is twice as likely as easier ones.</summary>
        float ChunkWeight(int i, int tier)
        {
            var c = _chunks[i];
            if (c.Tier > tier) return 0;
            return c.Weight * (c.Tier == tier ? 2f : 1f);
        }

        void PlaceSparks(double gapStart, in TrackRow row)
        {
            // Lead the player into a lane they can pass: prefer a free lane, else one to jump.
            var lane = -1;
            var start = _rng.Range(0, TrackRules.LaneCount);
            for (var k = 0; k < TrackRules.LaneCount && lane < 0; k++)
            {
                var l = (start + k) % TrackRules.LaneCount;
                if (row.Get(l) == Obstacle.None) lane = l;
            }
            for (var k = 0; k < TrackRules.LaneCount && lane < 0; k++)
            {
                var l = (start + k) % TrackRules.LaneCount;
                if (row.Get(l) == Obstacle.Crate) lane = l;
            }
            if (lane < 0) return;

            for (var z = gapStart + TrackRules.SparkSpacing; z < row.Z - 1.0; z += TrackRules.SparkSpacing)
                Sparks.Push(new Spark { Id = _nextSparkId++, Z = z, Lane = (byte)lane, Y = 1.0f });

            if (row.Get(lane) == Obstacle.Crate)
            {
                // Arc over the crate, at the top of a well-timed jump.
                Sparks.Push(new Spark { Id = _nextSparkId++, Z = row.Z - 0.5, Lane = (byte)lane, Y = 1.9f });
                Sparks.Push(new Spark { Id = _nextSparkId++, Z = row.Z + 0.5, Lane = (byte)lane, Y = 2.2f });
                Sparks.Push(new Spark { Id = _nextSparkId++, Z = row.Z + 1.5, Lane = (byte)lane, Y = 1.9f });
            }
        }

        void PlacePowerUp(double gapStart, in TrackRow row)
        {
            var lane = -1;
            var start = _rng.Range(0, TrackRules.LaneCount);
            for (var k = 0; k < TrackRules.LaneCount && lane < 0; k++)
            {
                var l = (start + k) % TrackRules.LaneCount;
                if (row.Get(l) == Obstacle.None) lane = l;
            }
            if (lane < 0) lane = start; // no free lane: the gap itself is still free
            var kind = (PowerUpKind)_rng.Range(0, 3);
            // At the target distance, kept clear of both rows; spacing stays 250–400 m (+ at most one row's length).
            var z = Math.Max(gapStart + 0.5, Math.Min(_nextPowerUpZ, row.Z - 0.5));
            PowerUps.Push(new PowerUpPickup { Id = _nextPowerUpId++, Z = z, Lane = (byte)lane, Kind = kind });
            _nextPowerUpZ = z + _rng.Range(PowerUpRules.MinSpacingM, PowerUpRules.MaxSpacingM);
        }
    }
}
