using GameFactory.Core.Input;
using NUnit.Framework;
using UrbanRunner.Sim;

namespace Tests.EditMode
{
    public class SimulationTests
    {
        const float Dt = 1f / 60f;

        static RunSimulation NewSim(ulong seed = 7, RunConfig? cfg = null, Upgrades up = default) =>
            new RunSimulation(GeneratorTests.Templates ?? Load(), cfg ?? RunConfig.Defaults, up, seed);

        static System.Collections.Generic.IReadOnlyList<ChunkTemplate> Load() =>
            GeneratorTests.Templates = UrbanRunner.Editor.ChunkCatalog.EnsureAssets().chunks;

        static void Run(RunSimulation sim, float seconds, AutoplayBot bot = null)
        {
            for (var t = 0f; t < seconds && sim.State == RunState.Running; t += Dt)
            {
                if (bot != null) sim.Input(bot.Decide());
                sim.Step(Dt);
            }
        }

        // ---- difficulty curve ----

        [Test]
        public void Speed_RampsAndCaps()
        {
            var c = RunConfig.Defaults;
            Assert.AreEqual(8f, DifficultyCurve.Speed(c, 0), 1e-4);
            Assert.AreEqual(11f, DifficultyCurve.Speed(c, 60), 1e-4);
            Assert.AreEqual(22f, DifficultyCurve.Speed(c, 60 * 10), 1e-4);
        }

        [Test]
        public void DistanceAndTime_AreInverse()
        {
            var c = RunConfig.Defaults;
            foreach (var t in new[] { 0.0, 1, 30, 280, 281, 600, 3600 })
                Assert.AreEqual(t, DifficultyCurve.TimeAt(c, DifficultyCurve.DistanceAt(c, t)), 1e-6);
        }

        [Test]
        public void SimDistance_MatchesClosedForm()
        {
            var sim = NewSim(cfg: new RunConfig { StartSpeed = 8, MaxSpeed = 22, SpeedRampPerMin = 3, RevivesPerRun = 1, ReviveEnabled = true });
            for (var i = 0; i < 60 * 30; i++) sim.Step(Dt);
            // The sim may crash into an obstacle (no input): compare at the time it stopped.
            Assert.AreEqual(DifficultyCurve.DistanceAt(sim.Config, sim.Time), sim.Distance, 0.05);
        }

        [Test]
        public void Config_IsClamped()
        {
            var c = new RunConfig { StartSpeed = 100, MaxSpeed = float.NaN, SpeedRampPerMin = -1, RevivesPerRun = 9 }.Clamped();
            Assert.AreEqual(12f, c.StartSpeed);
            Assert.AreEqual(22f, c.MaxSpeed);
            Assert.AreEqual(0.5f, c.SpeedRampPerMin);
            Assert.AreEqual(2, c.RevivesPerRun);
        }

        // ---- power-ups ----

        [TestCase(PowerUpKind.Magnet, 0, 8f)]
        [TestCase(PowerUpKind.Magnet, 5, 15.5f)]
        [TestCase(PowerUpKind.Shield, 0, 20f)]
        [TestCase(PowerUpKind.Shield, 5, 35f)]
        [TestCase(PowerUpKind.DoubleSparks, 0, 10f)]
        [TestCase(PowerUpKind.DoubleSparks, 5, 20f)]
        public void PowerUp_Durations(PowerUpKind kind, int level, float expected)
        {
            var u = new Upgrades { Magnet = level, Shield = level, Double = level };
            Assert.AreEqual(expected, PowerUpRules.Duration(kind, u), 1e-4);
        }

        [Test]
        public void PowerUp_TimersCountDown()
        {
            var sim = NewSim(up: new Upgrades { Magnet = 2 });
            sim.Activate(PowerUpKind.Magnet);
            Assert.AreEqual(11f, sim.MagnetLeft, 1e-4);
            var bot = new AutoplayBot(sim, 1f, 1);
            Run(sim, 5f, bot);
            Assert.AreEqual(6f, sim.MagnetLeft, 0.05);
            Run(sim, 7f, bot);
            Assert.AreEqual(0f, sim.MagnetLeft);
            Assert.AreEqual(1, sim.Stats.Magnet);
        }

        [Test]
        public void DoubleSparks_DoublesPickups()
        {
            var a = NewSim(seed: 3);
            var b = NewSim(seed: 3);
            b.Activate(PowerUpKind.DoubleSparks);
            Run(a, 9f, new AutoplayBot(a, 1f, 1));
            Run(b, 9f, new AutoplayBot(b, 1f, 1));
            Assert.Greater(a.Sparks, 0);
            Assert.AreEqual(a.Sparks * 2, b.Sparks);
        }

        [Test]
        public void Shield_AbsorbsOneHit()
        {
            var sim = NewSim();
            sim.Activate(PowerUpKind.Shield);
            Run(sim, 120f); // no input: we will hit things
            Assert.AreEqual(RunState.Crashed, sim.State, "second hit should crash");
            Assert.AreEqual(0f, sim.ShieldLeft);
        }

        // ---- input and stats ----

        [Test]
        public void Stats_CountActions()
        {
            var sim = NewSim();
            sim.Input(SwipeDirection.Left); sim.Step(Dt);
            sim.Input(SwipeDirection.Right); sim.Step(Dt);
            sim.Input(SwipeDirection.Up); sim.Step(Dt);
            for (var i = 0; i < 60; i++) sim.Step(Dt);
            sim.Input(SwipeDirection.Down); sim.Step(Dt);
            Assert.AreEqual(2, sim.Stats.LaneChanges);
            Assert.AreEqual(1, sim.Stats.Jumps);
            Assert.AreEqual(1, sim.Stats.Slides);
        }

        [Test]
        public void LaneChange_TakesAbout120ms()
        {
            var sim = NewSim();
            sim.Input(SwipeDirection.Right);
            var t = 0f;
            do { sim.Step(Dt); t += Dt; } while (sim.X < TrackRules.LaneX(2) && t < 1f);
            Assert.AreEqual(0.12f, t, 0.02f);
        }

        [Test]
        public void EdgeLane_SwipeOutwardIsABumpNotAChange()
        {
            var sim = NewSim();
            sim.Input(SwipeDirection.Left); sim.Step(Dt);
            sim.Input(SwipeDirection.Left); sim.Step(Dt);
            Assert.AreEqual(0, sim.Lane);
            Assert.IsTrue(sim.Events.Bumped);
            Assert.AreEqual(1, sim.Stats.LaneChanges);
        }

        [Test]
        public void JumpPressedJustBeforeLanding_IsBuffered()
        {
            var sim = NewSim();
            sim.Input(SwipeDirection.Up); sim.Step(Dt);
            var steps = (int)((TrackRules.JumpDuration - 0.1f) / Dt);
            for (var i = 0; i < steps; i++) sim.Step(Dt);
            Assert.IsTrue(sim.Airborne);
            sim.Input(SwipeDirection.Up); // 0.1 s before landing, inside the 0.15 s buffer
            for (var i = 0; i < 12; i++) sim.Step(Dt);
            Assert.AreEqual(2, sim.Stats.Jumps);
        }

        // ---- autoplay, crash, revive ----

        [Test]
        public void PerfectBot_SurvivesFiveMinutes_20Seeds()
        {
            for (ulong seed = 1; seed <= 20; seed++)
            {
                var sim = NewSim(seed);
                Run(sim, 300f, new AutoplayBot(sim, 1f, seed));
                Assert.AreEqual(RunState.Running, sim.State,
                    $"seed {seed}: crashed into {sim.CrashedInto} at {sim.Distance:F0} m ({sim.Time:F0} s)");
                Assert.Greater(sim.Distance, 3000);
                Assert.Greater(sim.Sparks, 100);
            }
        }

        [Test]
        public void SameSeedAndInputs_SameResult()
        {
            var a = NewSim(11);
            var b = NewSim(11);
            Run(a, 60f, new AutoplayBot(a, 0.7f, 5));
            Run(b, 60f, new AutoplayBot(b, 0.7f, 5));
            Assert.AreEqual(a.Distance, b.Distance);
            Assert.AreEqual(a.Sparks, b.Sparks);
            Assert.AreEqual(a.State, b.State);
        }

        [Test]
        public void ForcedCrash_StopsTheRun()
        {
            var sim = NewSim();
            Run(sim, 2f, new AutoplayBot(sim, 1f, 1));
            var d = sim.Distance;
            sim.ForceCrash();
            sim.Step(Dt);
            Assert.AreEqual(RunState.Crashed, sim.State);
            Assert.AreEqual(d, sim.Distance);
        }

        [Test]
        public void Revive_ClearsAheadAndGivesInvulnerability()
        {
            var sim = NewSim();
            Run(sim, 120f); // no input: crashes quickly
            Assert.AreEqual(RunState.Crashed, sim.State);
            Assert.IsTrue(sim.CanRevive);
            sim.Revive();
            Assert.AreEqual(RunState.Running, sim.State);
            Assert.AreEqual(2f, sim.InvulnerableLeft);
            Assert.AreEqual(1, sim.RevivesUsed);
            Run(sim, 1.9f);
            Assert.AreEqual(RunState.Running, sim.State, "must not crash during invulnerability");
        }

        [Test]
        public void Revive_LimitedByConfig()
        {
            var cfg = RunConfig.Defaults;
            cfg.RevivesPerRun = 1;
            var sim = NewSim(cfg: cfg);
            Run(sim, 120f);
            sim.Revive();
            Run(sim, 120f);
            Assert.AreEqual(RunState.Crashed, sim.State);
            Assert.IsFalse(sim.CanRevive);
            cfg.ReviveEnabled = false;
            var off = NewSim(cfg: cfg);
            Run(off, 120f);
            Assert.IsFalse(off.CanRevive);
        }
    }
}
