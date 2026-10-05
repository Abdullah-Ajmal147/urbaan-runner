using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UrbanRunner.Editor;
using UrbanRunner.Sim;

namespace Tests.EditMode
{
    public class GeneratorTests
    {
        internal static IReadOnlyList<ChunkTemplate> Templates;

        [OneTimeSetUp]
        public void LoadLibrary()
        {
            Templates = ChunkCatalog.EnsureAssets().chunks;
        }

        [Test]
        public void Library_IsValid()
        {
            var lib = ChunkCatalog.EnsureAssets();
            CollectionAssert.IsEmpty(lib.Validate().ToList());
            Assert.GreaterOrEqual(lib.chunks.Count, 20);
        }

        /// <summary>
        /// HANDOVER §10.1: 100 seeds × 10,000 rows. Every row has a passable lane, and from any lane the player
        /// could be in after one row, a passable lane of the next row is reachable in time.
        /// </summary>
        [Test]
        public void Generator_NeverProducesAnImpossibleRow_100Seeds_x_10000Rows()
        {
            var cfg = RunConfig.Defaults;
            for (ulong seed = 1; seed <= 100; seed++)
                CheckSeed(cfg, seed, 10000);
        }

        [Test]
        public void Generator_HoldsAtExtremeConfig()
        {
            var fast = new RunConfig { StartSpeed = 12, MaxSpeed = 30, SpeedRampPerMin = 6, RevivesPerRun = 1, ReviveEnabled = true };
            var slow = new RunConfig { StartSpeed = 5, MaxSpeed = 12, SpeedRampPerMin = 0.5f, RevivesPerRun = 1, ReviveEnabled = true };
            for (ulong seed = 1; seed <= 10; seed++)
            {
                CheckSeed(fast, seed, 5000);
                CheckSeed(slow, seed, 5000);
            }
        }

        static void CheckSeed(RunConfig cfg, ulong seed, int rowCount)
        {
            var gen = new TrackGenerator(Templates, cfg, seed);
            var checkedId = -1;
            TrackRow prev = default;
            var hasPrev = false;
            var tiersSeen = new bool[4];
            while (checkedId < rowCount - 1)
            {
                gen.GenerateUntil(gen.GeneratedTo + 200);
                for (var i = 0; i < gen.Rows.Count; i++)
                {
                    var row = gen.Rows[i];
                    if (row.Id <= checkedId) continue;
                    checkedId = row.Id;
                    tiersSeen[row.Tier] = true;

                    var passable = Enumerable.Range(0, 3).Where(l => TrackRules.IsPassable(row.Get(l))).ToList();
                    Assert.IsNotEmpty(passable, $"seed {seed} row {row.Id}: no passable lane");
                    for (var l = 0; l < 3; l++)
                        if (row.Get(l) == Obstacle.Cart)
                            Assert.GreaterOrEqual(DifficultyCurve.Tier(row.Z), 3, $"seed {seed} row {row.Id}: cart before tier 3");

                    if (hasPrev)
                    {
                        Assert.Greater(row.Z, prev.End, $"seed {seed} row {row.Id}: rows overlap");
                        var gapS = DifficultyCurve.TimeAt(cfg, row.Z) - DifficultyCurve.TimeAt(cfg, prev.End);
                        Assert.GreaterOrEqual(gapS, TrackRules.MinActionGapS - 1e-6,
                            $"seed {seed} row {row.Id}: gap {gapS:F3}s < {TrackRules.MinActionGapS}s");

                        // Reachability: from every lane the player could be in, some passable lane is reachable
                        // within the gap after finishing the previous row's action (worst case: landing a jump).
                        var prevLanes = Enumerable.Range(0, 3).Where(l => TrackRules.IsPassable(prev.Get(l)));
                        foreach (var from in prevLanes)
                        {
                            var busyAfterEnd = prev.Get(from) == Obstacle.Crate ? WorstJumpLandingAfterEnd(cfg, prev) : 0.0;
                            var best = passable.Min(to => System.Math.Abs(to - from) * TrackRules.LaneChangeDuration);
                            Assert.LessOrEqual(busyAfterEnd + best, gapS,
                                $"seed {seed} row {row.Id}: lane {from} can't reach a passable lane in time");
                        }
                    }
                    prev = row;
                    hasPrev = true;
                    if (checkedId >= rowCount - 1) break;
                }
                gen.Recycle(gen.GeneratedTo - 300);
            }
            if (rowCount >= 5000 && cfg.StartSpeed < 10)
                Assert.IsTrue(tiersSeen[1] && tiersSeen[2] && tiersSeen[3], $"seed {seed}: not all tiers reached");
        }

        /// <summary>Latest a jump that clears the crate can land, measured from the crate's end (seconds).</summary>
        static double WorstJumpLandingAfterEnd(RunConfig cfg, TrackRow prev)
        {
            // The air window above crate height starts at u≈0.18; the latest take-off reaches the crate front then.
            var tCrateFront = DifficultyCurve.TimeAt(cfg, prev.Z - TrackRules.PlayerHalfDepth);
            var tLand = tCrateFront + TrackRules.JumpDuration * (1 - 0.18);
            var tEnd = DifficultyCurve.TimeAt(cfg, prev.End);
            return System.Math.Max(0, tLand - tEnd);
        }

        [Test]
        public void SameSeed_SameTrack()
        {
            var a = new TrackGenerator(Templates, RunConfig.Defaults, 42);
            var b = new TrackGenerator(Templates, RunConfig.Defaults, 42);
            a.GenerateUntil(3000);
            b.GenerateUntil(3000);
            Assert.AreEqual(a.Rows.Count, b.Rows.Count);
            for (var i = 0; i < a.Rows.Count; i++)
            {
                Assert.AreEqual(a.Rows[i].Z, b.Rows[i].Z);
                Assert.AreEqual(a.Rows[i].L0, b.Rows[i].L0);
                Assert.AreEqual(a.Rows[i].L1, b.Rows[i].L1);
                Assert.AreEqual(a.Rows[i].L2, b.Rows[i].L2);
            }
        }

        [Test]
        public void DifferentSeeds_DifferentTracks()
        {
            var a = new TrackGenerator(Templates, RunConfig.Defaults, 1);
            var b = new TrackGenerator(Templates, RunConfig.Defaults, 2);
            a.GenerateUntil(1000);
            b.GenerateUntil(1000);
            var same = true;
            for (var i = 0; i < System.Math.Min(a.Rows.Count, b.Rows.Count); i++)
                if (a.Rows[i].L0 != b.Rows[i].L0 || a.Rows[i].L1 != b.Rows[i].L1 || a.Rows[i].L2 != b.Rows[i].L2) same = false;
            Assert.IsFalse(same);
        }

        [Test]
        public void PowerUps_Every250To400m()
        {
            for (ulong seed = 1; seed <= 20; seed++)
            {
                var gen = new TrackGenerator(Templates, RunConfig.Defaults, seed);
                var zs = new List<double>();
                while (gen.GeneratedTo < 20000)
                {
                    gen.GenerateUntil(gen.GeneratedTo + 200);
                    for (var i = 0; i < gen.PowerUps.Count; i++)
                        if (zs.Count == 0 || gen.PowerUps[i].Z > zs[zs.Count - 1]) zs.Add(gen.PowerUps[i].Z);
                    gen.Recycle(gen.GeneratedTo - 100);
                }
                Assert.Greater(zs.Count, 40);
                Assert.That(zs[0], Is.InRange(PowerUpRules.MinSpacingM - 5, PowerUpRules.MaxSpacingM + 5));
                for (var i = 1; i < zs.Count; i++)
                    Assert.That(zs[i] - zs[i - 1], Is.InRange(PowerUpRules.MinSpacingM - 5, PowerUpRules.MaxSpacingM + 5),
                        $"seed {seed}: power-up spacing {zs[i] - zs[i - 1]:F0} m");
            }
        }

        /// <summary>Regression: a power-up target that fell inside a row was re-set behind the generator forever,
        /// so only the first power-up of a run ever spawned.</summary>
        [Test]
        public void PowerUps_KeepSpawningAfterTargetFallsInsideARow()
        {
            var gen = new TrackGenerator(Templates, RunConfig.Defaults, 1);
            var count = 0;
            double last = -1;
            while (gen.GeneratedTo < 5000)
            {
                gen.GenerateUntil(gen.GeneratedTo + 100);
                for (var i = 0; i < gen.PowerUps.Count; i++)
                    if (gen.PowerUps[i].Z > last) { count++; last = gen.PowerUps[i].Z; }
                gen.Recycle(gen.GeneratedTo - 50);
            }
            Assert.GreaterOrEqual(count, 12, "5 km should hold 12+ power-ups at 250–400 m spacing");
        }

        [Test]
        public void SparkDensity_StaysUnderTheContractCap()
        {
            for (ulong seed = 1; seed <= 20; seed++)
            {
                var gen = new TrackGenerator(Templates, RunConfig.Defaults, seed);
                var count = 0;
                double last = 0;
                while (gen.GeneratedTo < 10000)
                {
                    gen.GenerateUntil(gen.GeneratedTo + 200);
                    for (var i = 0; i < gen.Sparks.Count; i++)
                        if (gen.Sparks[i].Z > last) { count++; last = gen.Sparks[i].Z; }
                    gen.Recycle(gen.GeneratedTo - 100);
                }
                // ×2 for Double Sparks always on.
                Assert.Less(count * 2 / gen.GeneratedTo, TrackRules.MaxSparksPerMetre, $"seed {seed}");
            }
        }

        [TestCase(0.0, 1)]
        [TestCase(499.9, 1)]
        [TestCase(500.0, 2)]
        [TestCase(1499.9, 2)]
        [TestCase(1500.0, 3)]
        [TestCase(50000.0, 3)]
        public void Tiers_UnlockByDistance(double d, int tier) => Assert.AreEqual(tier, DifficultyCurve.Tier(d));
    }
}
