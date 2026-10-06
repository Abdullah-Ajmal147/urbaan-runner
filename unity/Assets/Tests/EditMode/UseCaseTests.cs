using GameFactory.Core.Input;
using NUnit.Framework;
using UrbanRunner.Sim;

namespace Tests.EditMode
{
    /// <summary>
    /// Owner-requested gameplay use cases, one controlled scenario each: a hand-placed row in front of the runner,
    /// then the right action (passes) and the wrong action (crashes).
    /// </summary>
    public class UseCaseTests
    {
        const float Dt = 1f / 60f;
        const double RowZ = 60;

        static RunSimulation EmptyTrack(Obstacle l0, Obstacle l1, Obstacle l2, Upgrades up = default)
        {
            var templates = GeneratorTests.Templates ?? (GeneratorTests.Templates = UrbanRunner.Editor.ChunkCatalog.EnsureAssets().chunks);
            var sim = new RunSimulation(templates, RunConfig.Defaults, up, 1);
            sim.Track.AutoGenerate = false;
            sim.Track.Rows.Clear();
            sim.Track.Sparks.Clear();
            sim.Track.PowerUps.Clear();
            if (l0 != Obstacle.None || l1 != Obstacle.None || l2 != Obstacle.None) sim.Track.PlaceRow(RowZ, l0, l1, l2);
            return sim;
        }

        /// <summary>Runs until the runner is <paramref name="before"/> metres from the row, then gives the input.</summary>
        static void ActAt(RunSimulation sim, double before, SwipeDirection input)
        {
            while (sim.Distance < RowZ - before && sim.State == RunState.Running) sim.Step(Dt);
            if (input != SwipeDirection.None) sim.Input(input);
        }

        static void PastTheRow(RunSimulation sim)
        {
            while (sim.Distance < RowZ + 8 && sim.State == RunState.Running) sim.Step(Dt);
        }

        /// <summary>Jump so the crate's middle is under the top of the jump (same rule as the bot).</summary>
        static double JumpLead(RunSimulation sim, Obstacle o) => sim.Speed * TrackRules.JumpDuration * 0.5 - TrackRules.Length(o) * 0.5;

        // ---- speed ----

        [Test]
        public void Speed_IncreasesWithTime_AndStopsAtMax()
        {
            var sim = EmptyTrack(Obstacle.None, Obstacle.None, Obstacle.None);
            var at0 = sim.Speed;
            for (var i = 0; i < 60 * 60; i++) sim.Step(Dt);
            var at60 = sim.Speed;
            for (var i = 0; i < 60 * 60 * 9; i++) sim.Step(Dt);
            Assert.AreEqual(8f, at0, 0.01f);
            Assert.AreEqual(11f, at60, 0.05f, "+3 m/s per minute");
            Assert.AreEqual(22f, sim.Speed, 0.01f, "capped at max_speed");
        }

        // ---- crates: jump ----

        [Test]
        public void Crate_JumpOver_Passes()
        {
            var sim = EmptyTrack(Obstacle.None, Obstacle.Crate, Obstacle.None);
            ActAt(sim, JumpLead(sim, Obstacle.Crate), SwipeDirection.Up);
            PastTheRow(sim);
            Assert.AreEqual(RunState.Running, sim.State);
        }

        [TestCase(SwipeDirection.None)]
        [TestCase(SwipeDirection.Down)]
        public void Crate_RunIntoOrSlide_Crashes(SwipeDirection wrong)
        {
            var sim = EmptyTrack(Obstacle.None, Obstacle.Crate, Obstacle.None);
            ActAt(sim, 2.0, wrong);
            PastTheRow(sim);
            Assert.AreEqual(RunState.Crashed, sim.State);
            Assert.AreEqual(Obstacle.Crate, sim.CrashedInto);
        }

        // ---- hanging signs: slide ----

        [Test]
        public void Banner_SlideUnder_Passes()
        {
            var sim = EmptyTrack(Obstacle.None, Obstacle.Banner, Obstacle.None);
            ActAt(sim, sim.Speed * 0.25 + TrackRules.PlayerHalfDepth, SwipeDirection.Down);
            PastTheRow(sim);
            Assert.AreEqual(RunState.Running, sim.State);
        }

        [TestCase(SwipeDirection.None)]
        [TestCase(SwipeDirection.Up)]
        public void Banner_RunIntoOrJump_Crashes(SwipeDirection wrong)
        {
            var sim = EmptyTrack(Obstacle.None, Obstacle.Banner, Obstacle.None);
            ActAt(sim, JumpLead(sim, Obstacle.Banner), wrong);
            PastTheRow(sim);
            Assert.AreEqual(RunState.Crashed, sim.State);
            Assert.AreEqual(Obstacle.Banner, sim.CrashedInto);
        }

        // ---- stalls, vans, carts: change lane ----

        [TestCase(Obstacle.Stall, SwipeDirection.Left)]
        [TestCase(Obstacle.Van, SwipeDirection.Right)]
        [TestCase(Obstacle.Cart, SwipeDirection.Left)]
        public void Blocker_ChangeLane_Passes(Obstacle o, SwipeDirection dodge)
        {
            var sim = EmptyTrack(Obstacle.None, o, Obstacle.None);
            ActAt(sim, 6.0, dodge);
            PastTheRow(sim);
            Assert.AreEqual(RunState.Running, sim.State);
        }

        [TestCase(Obstacle.Stall, SwipeDirection.None)]
        [TestCase(Obstacle.Stall, SwipeDirection.Up)]
        [TestCase(Obstacle.Van, SwipeDirection.Down)]
        [TestCase(Obstacle.Cart, SwipeDirection.Up)]
        public void Blocker_JumpSlideOrStay_Crashes(Obstacle o, SwipeDirection wrong)
        {
            var sim = EmptyTrack(Obstacle.None, o, Obstacle.None);
            ActAt(sim, 2.0, wrong);
            PastTheRow(sim);
            Assert.AreEqual(RunState.Crashed, sim.State);
            Assert.AreEqual(o, sim.CrashedInto);
        }

        [Test]
        public void ChangingIntoABlockedLane_Crashes()
        {
            var sim = EmptyTrack(Obstacle.Stall, Obstacle.None, Obstacle.None);
            ActAt(sim, 1.0, SwipeDirection.Left);
            PastTheRow(sim);
            Assert.AreEqual(RunState.Crashed, sim.State);
        }

        // ---- shield ----

        [Test]
        public void Shield_AbsorbsExactlyOneHit()
        {
            var sim = EmptyTrack(Obstacle.None, Obstacle.Stall, Obstacle.None);
            sim.Track.PlaceRow(RowZ + 30, Obstacle.None, Obstacle.Stall, Obstacle.None);
            sim.Activate(PowerUpKind.Shield);
            ActAt(sim, 2.0, SwipeDirection.None);
            PastTheRow(sim);
            Assert.AreEqual(RunState.Running, sim.State, "first hit absorbed");
            Assert.AreEqual(0f, sim.ShieldLeft, "shield used up");
            while (sim.Distance < RowZ + 40 && sim.State == RunState.Running) sim.Step(Dt);
            Assert.AreEqual(RunState.Crashed, sim.State, "second hit crashes");
        }

        [Test]
        public void Shield_ExpiresAfterItsDuration()
        {
            var sim = EmptyTrack(Obstacle.None, Obstacle.None, Obstacle.None);
            sim.Activate(PowerUpKind.Shield);
            var steps = (int)((PowerUpRules.Duration(PowerUpKind.Shield, default) + 0.2f) / Dt);
            for (var i = 0; i < steps; i++) sim.Step(Dt);
            Assert.AreEqual(0f, sim.ShieldLeft);
        }

        [Test]
        public void ShieldUpgrade_LastsLonger()
        {
            var sim = EmptyTrack(Obstacle.None, Obstacle.None, Obstacle.None, new Upgrades { Shield = 5 });
            sim.Activate(PowerUpKind.Shield);
            Assert.AreEqual(35f, sim.ShieldLeft, 1e-3);
        }

        // ---- sparks, magnet, double ----

        static void SparkLine(RunSimulation sim, int lane, int count, double fromZ)
        {
            for (var i = 0; i < count; i++)
                sim.Track.Sparks.Push(new Spark { Id = 10000 + i, Z = fromZ + i * 2, Lane = (byte)lane, Y = 1f });
        }

        [Test]
        public void Sparks_InYourLane_AreCollected()
        {
            var sim = EmptyTrack(Obstacle.None, Obstacle.None, Obstacle.None);
            SparkLine(sim, 1, 10, 20);
            while (sim.Distance < 50) sim.Step(Dt);
            Assert.AreEqual(10, sim.Sparks);
        }

        [Test]
        public void Sparks_InOtherLanes_NeedTheMagnet()
        {
            var plain = EmptyTrack(Obstacle.None, Obstacle.None, Obstacle.None);
            SparkLine(plain, 0, 10, 20);
            while (plain.Distance < 50) plain.Step(Dt);
            Assert.AreEqual(0, plain.Sparks, "no magnet: neighbouring lane sparks stay");

            var magnet = EmptyTrack(Obstacle.None, Obstacle.None, Obstacle.None);
            SparkLine(magnet, 0, 10, 20);
            magnet.Activate(PowerUpKind.Magnet);
            while (magnet.Distance < 50) magnet.Step(Dt);
            Assert.AreEqual(10, magnet.Sparks, "magnet pulls them in");
        }

        [Test]
        public void DoubleSparks_CountsEverySparkTwice()
        {
            var sim = EmptyTrack(Obstacle.None, Obstacle.None, Obstacle.None);
            SparkLine(sim, 1, 10, 20);
            sim.Activate(PowerUpKind.DoubleSparks);
            while (sim.Distance < 50) sim.Step(Dt);
            Assert.AreEqual(20, sim.Sparks);
        }

        [Test]
        public void PowerUpPickup_StartsItsTimer()
        {
            var sim = EmptyTrack(Obstacle.None, Obstacle.None, Obstacle.None);
            sim.Track.PowerUps.Push(new PowerUpPickup { Id = 1, Z = 20, Lane = 1, Kind = PowerUpKind.Magnet });
            while (sim.Distance < 25) sim.Step(Dt);
            Assert.Greater(sim.MagnetLeft, 7f);
            Assert.AreEqual(1, sim.Stats.Magnet);
        }

        // ---- tutorial ----

        [Test]
        public void Tutorial_CorrectSwipes_CompleteIt_WrongSwipesAreIgnored_AndYouCantLose()
        {
            var templates = GeneratorTests.Templates ?? (GeneratorTests.Templates = UrbanRunner.Editor.ChunkCatalog.EnsureAssets().chunks);
            var sim = new RunSimulation(templates, RunConfig.Defaults, default, 1, RunMode.Tutorial);
            var tut = new TutorialDirector(sim);
            var prompts = 0;
            for (var frame = 0; frame < 60 * 120 && !tut.Completed; frame++)
            {
                if (tut.Phase == TutorialPhase.WaitingForInput)
                {
                    prompts++;
                    var required = tut.Required;
                    tut.Input(required == SwipeDirection.Left ? SwipeDirection.Right : SwipeDirection.Left); // wrong first
                    Assert.AreEqual(TutorialPhase.WaitingForInput, tut.Phase, "a wrong swipe must not continue");
                    tut.Input(required);
                }
                var dt = tut.Tick(Dt) ? Dt * TutorialDirector.SpeedFactor : 0f;
                sim.Step(dt);
                Assert.AreEqual(RunState.Running, sim.State, "can't lose in the tutorial");
            }
            Assert.IsTrue(tut.Completed);
            Assert.AreEqual(4, prompts);
            Assert.IsFalse(sim.GodMode, "normal run after the tutorial");
            Assert.IsTrue(sim.Track.AutoGenerate, "track generation resumed");
        }

        [Test]
        public void Tutorial_EachActionActuallyClearsItsObstacle()
        {
            // Same as above but checks that the runner really passes each obstacle without god-mode help:
            // the action given at the freeze point must clear the obstacle on its own.
            var templates = GeneratorTests.Templates ?? (GeneratorTests.Templates = UrbanRunner.Editor.ChunkCatalog.EnsureAssets().chunks);
            var sim = new RunSimulation(templates, RunConfig.Defaults, default, 1, RunMode.Tutorial);
            var tut = new TutorialDirector(sim);
            sim.GodMode = false; // prove the timing, not the safety net
            for (var frame = 0; frame < 60 * 120 && !tut.Completed; frame++)
            {
                if (tut.Phase == TutorialPhase.WaitingForInput) tut.Input(tut.Required);
                sim.Step(tut.Tick(Dt) ? Dt * TutorialDirector.SpeedFactor : 0f);
                Assert.AreEqual(RunState.Running, sim.State, $"crashed into {sim.CrashedInto} at step {tut.StepIndex}");
            }
            Assert.IsTrue(tut.Completed);
        }
    }
}
