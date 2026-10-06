using GameFactory.Core.Input;
using NUnit.Framework;
using UrbanRunner.Sim;

namespace Tests.EditMode
{
    /// <summary>D28 movement: gravity jump, fast drop, tram ramps and roofs, side bumps and the Warden.</summary>
    public class MovementTests
    {
        const float Dt = 1f / 120f;

        /// <summary>A sim whose track contains only the rows a test places.</summary>
        static RunSimulation Empty()
        {
            var sim = new RunSimulation(GeneratorTests.Templates ?? (GeneratorTests.Templates = UrbanRunner.Editor.ChunkCatalog.EnsureAssets().chunks),
                RunConfig.Defaults, default, 5);
            sim.Track.AutoGenerate = false;
            while (sim.Track.Rows.Count > 0) sim.Track.Rows.PopFront();
            while (sim.Track.Sparks.Count > 0) sim.Track.Sparks.PopFront();
            while (sim.Track.PowerUps.Count > 0) sim.Track.PowerUps.PopFront();
            return sim;
        }

        static void RunUntil(RunSimulation sim, System.Func<bool> stop, float maxSeconds = 10f)
        {
            for (var t = 0f; t < maxSeconds && sim.State == RunState.Running && !stop(); t += Dt) sim.Step(Dt);
        }

        [Test]
        public void Jump_HangsAbout0_8s_AndPeaksAt1_9m()
        {
            var sim = Empty();
            sim.Input(SwipeDirection.Up);
            var t = 0f;
            var peak = 0f;
            sim.Step(Dt);
            while (sim.Airborne && t < 3f) { sim.Step(Dt); t += Dt; peak = System.Math.Max(peak, sim.Y); }
            Assert.AreEqual(TrackRules.JumpDuration, t, 0.03);
            Assert.AreEqual(TrackRules.JumpHeight, peak, 0.05);
            Assert.AreEqual(0f, sim.Y);
        }

        [Test]
        public void SwipeDownInTheAir_SlamsDownThenSlides()
        {
            var sim = Empty();
            sim.Input(SwipeDirection.Up);
            for (var i = 0; i < 30; i++) sim.Step(Dt); // rising
            sim.Input(SwipeDirection.Down);
            var t = 0f;
            while (sim.Airborne && t < 1f) { sim.Step(Dt); t += Dt; }
            Assert.Less(t, 0.2f, "fast drop is much quicker than the normal fall");
            sim.Step(Dt);
            Assert.IsTrue(sim.Sliding, "slides on landing");
        }

        [Test]
        public void Ramp_CarriesTheCourierOntoTheRoof_ThenTheyFallOffTheEnd()
        {
            var sim = Empty();
            var z = sim.Distance + 20;
            sim.Track.PlaceRow(z, Obstacle.None, Obstacle.TramRamp, Obstacle.None);
            RunUntil(sim, () => sim.Distance > z + TrackRules.RampLength + 2);
            Assert.AreEqual(RunState.Running, sim.State);
            Assert.IsTrue(sim.OnRoof, "running along the roof");
            Assert.AreEqual(TrackRules.TramHeight, sim.Y, 1e-3);
            RunUntil(sim, () => sim.Distance > z + TrackRules.Length(Obstacle.TramRamp) + 0.6);
            Assert.IsTrue(sim.Airborne, "ran off the end: falling");
            RunUntil(sim, () => !sim.Airborne);
            Assert.AreEqual(0f, sim.Y);
            Assert.AreEqual(RunState.Running, sim.State);
        }

        [Test]
        public void FromARoof_ACourierCanCrossOntoTheNextTramsRoof()
        {
            var sim = Empty();
            var z = sim.Distance + 20;
            sim.Track.PlaceRow(z, Obstacle.None, Obstacle.TramRamp, Obstacle.Tram);
            RunUntil(sim, () => sim.Distance > z + TrackRules.RampLength + 1);
            sim.Input(SwipeDirection.Right);
            RunUntil(sim, () => sim.Distance > z + TrackRules.RampLength + 3);
            Assert.AreEqual(RunState.Running, sim.State, "walked across onto the parked tram's roof");
            Assert.AreEqual(2, sim.Lane);
            Assert.IsTrue(sim.OnRoof);
        }

        [Test]
        public void TramFront_AtStreetLevel_IsACrash()
        {
            var sim = Empty();
            sim.Track.PlaceRow(sim.Distance + 15, Obstacle.Tram, Obstacle.Tram, Obstacle.Tram);
            RunUntil(sim, () => false, 5f);
            Assert.AreEqual(RunState.Crashed, sim.State);
            Assert.AreEqual(Obstacle.Tram, sim.CrashedInto);
            Assert.IsFalse(sim.Caught);
        }

        [Test]
        public void SideBump_Stumbles_ThenASecondBumpWithin8s_IsCaught()
        {
            var sim = Empty();
            var z = sim.Distance + 10;
            // A long tram in the right lane; move into it once we're alongside.
            sim.Track.PlaceRow(z, Obstacle.None, Obstacle.None, Obstacle.Tram);
            RunUntil(sim, () => sim.Distance > z + 2);
            sim.Input(SwipeDirection.Right);
            RunUntil(sim, () => sim.Events.Stumbled, 1f);
            Assert.AreEqual(RunState.Running, sim.State, "a side bump is not a crash");
            Assert.AreEqual(1, sim.Lane, "bounced back to the lane it came from");
            Assert.Greater(sim.ChaseLeft, 7f, "the Warden closes in");

            RunUntil(sim, () => System.Math.Abs(sim.X - TrackRules.LaneX(1)) < 0.01f, 1f);
            sim.Input(SwipeDirection.Right);
            RunUntil(sim, () => false, 1f);
            Assert.AreEqual(RunState.Crashed, sim.State);
            Assert.IsTrue(sim.Caught, "second bump inside the window: caught");
        }

        [Test]
        public void SideBump_AfterTheChaseWindow_IsJustAnotherStumble()
        {
            var sim = Empty();
            var z = sim.Distance + 10;
            sim.Track.PlaceRow(z, Obstacle.None, Obstacle.None, Obstacle.Tram);
            RunUntil(sim, () => sim.Distance > z + 2);
            sim.Input(SwipeDirection.Right);
            RunUntil(sim, () => sim.Events.Stumbled, 1f);
            RunUntil(sim, () => sim.ChaseLeft <= 0f, 10f);
            var z2 = sim.Distance + 10;
            sim.Track.PlaceRow(z2, Obstacle.None, Obstacle.None, Obstacle.Tram);
            RunUntil(sim, () => sim.Distance > z2 + 2);
            sim.Input(SwipeDirection.Right);
            RunUntil(sim, () => sim.Events.Stumbled, 1f);
            Assert.AreEqual(RunState.Running, sim.State);
        }

        [Test]
        public void RoofSparks_FollowEveryRampedTram()
        {
            var gen = new TrackGenerator(GeneratorTests.Templates ?? (GeneratorTests.Templates = UrbanRunner.Editor.ChunkCatalog.EnsureAssets().chunks), RunConfig.Defaults, 3);
            var ramps = 0;
            var checkedId = -1;
            while (gen.GeneratedTo < 6000)
            {
                gen.GenerateUntil(gen.GeneratedTo + 60);
                for (var i = 0; i < gen.Rows.Count; i++)
                {
                    var r = gen.Rows[i];
                    if (r.Id <= checkedId) continue;
                    checkedId = r.Id;
                    if (r.Get(0) != Obstacle.TramRamp && r.Get(1) != Obstacle.TramRamp && r.Get(2) != Obstacle.TramRamp) continue;
                    ramps++;
                    var onRoof = 0;
                    for (var k = 0; k < gen.Sparks.Count; k++)
                        if (gen.Sparks[k].Z > r.Z + TrackRules.RampLength && gen.Sparks[k].Z < r.End && gen.Sparks[k].Y > TrackRules.TramHeight) onRoof++;
                    Assert.Greater(onRoof, 2, $"row {r.Id}: no spark line on the roof");
                }
                gen.Recycle(gen.GeneratedTo - 150);
            }
            Assert.Greater(ramps, 3, "ramped trams appear");
        }

        [Test]
        public void LastMomentDodge_IsANearMiss_AnEarlyOneIsNot()
        {
            var sim = Empty();
            var z = sim.Distance + 12;
            sim.Track.PlaceRow(z, Obstacle.None, Obstacle.Tram, Obstacle.None);
            // Dodge 0.2 s before reaching the tram.
            RunUntil(sim, () => z - TrackRules.PlayerHalfDepth - sim.Distance < sim.Speed * 0.2f);
            sim.Input(SwipeDirection.Left);
            var nearMiss = false;
            for (var t = 0f; t < 1f && sim.State == RunState.Running; t += Dt) { sim.Step(Dt); nearMiss |= sim.Events.NearMiss; }
            Assert.IsTrue(nearMiss, "last-moment dodge");

            var early = Empty();
            var z2 = early.Distance + 30;
            early.Track.PlaceRow(z2, Obstacle.None, Obstacle.Tram, Obstacle.None);
            early.Input(SwipeDirection.Left); // long before the tram
            var any = false;
            for (var t = 0f; t < 4f && early.State == RunState.Running; t += Dt) { early.Step(Dt); any |= early.Events.NearMiss; }
            Assert.IsFalse(any, "an early lane change is just a lane change");
        }

        // ---- Lantern Hunt (D32, bridge v3) ----

        [Test]
        public void HuntLantern_NeverChangesTheTrack_AndSitsInAGap()
        {
            var t = GeneratorTests.Templates ?? (GeneratorTests.Templates = UrbanRunner.Editor.ChunkCatalog.EnsureAssets().chunks);
            var plain = new TrackGenerator(t, RunConfig.Defaults, 77);
            var hunted = new TrackGenerator(t, RunConfig.Defaults, 77) { HuntAtM = 400 };
            plain.GenerateUntil(900);
            hunted.GenerateUntil(900);
            Assert.AreEqual(plain.Rows.Count, hunted.Rows.Count);
            for (var i = 0; i < plain.Rows.Count; i++)
            {
                Assert.AreEqual(plain.Rows[i].Z, hunted.Rows[i].Z, "the Daily Run stays the same for everyone");
                Assert.AreEqual(plain.Rows[i].L0, hunted.Rows[i].L0);
                Assert.AreEqual(plain.Rows[i].L1, hunted.Rows[i].L1);
                Assert.AreEqual(plain.Rows[i].L2, hunted.Rows[i].L2);
            }
            Assert.IsTrue(hunted.Hunt.Placed);
            Assert.Greater(hunted.Hunt.Z, 380);
            for (var i = 0; i < hunted.Rows.Count; i++)
                Assert.IsFalse(hunted.Hunt.Z >= hunted.Rows[i].Z && hunted.Hunt.Z <= hunted.Rows[i].End, "never inside an obstacle row");
            Assert.IsFalse(plain.Hunt.Placed);
        }

        [Test]
        public void HuntLantern_IsCollectedByRunningThroughItsLane()
        {
            var sim = Empty();
            sim.Track.Hunt = new HuntLantern { Z = sim.Distance + 15, Lane = 1, Placed = true };
            var found = false;
            for (var t = 0f; t < 3f; t += Dt) { sim.Step(Dt); found |= sim.Events.HuntFound; }
            Assert.IsTrue(found);
            Assert.IsTrue(sim.HuntCollected);

            var missed = Empty();
            missed.Track.Hunt = new HuntLantern { Z = missed.Distance + 15, Lane = 0, Placed = true };
            for (var t = 0f; t < 3f; t += Dt) missed.Step(Dt);
            Assert.IsFalse(missed.HuntCollected, "a lantern in another lane is missed");
        }
    }
}
