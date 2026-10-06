using GameFactory.Core.Perf;
using NUnit.Framework;

namespace Tests.EditMode
{
    /// <summary>D31: adaptive quality steps down fast when a phone can't hold its target, up slowly with headroom.</summary>
    public class AdaptiveQualityTests
    {
        static void Run(AdaptiveQualityPolicy p, float fps, float seconds)
        {
            for (var t = 0f; t < seconds; t += 1f / fps) p.Sample(1f / fps);
        }

        [Test]
        public void HoldingSixty_StaysAtTheTop()
        {
            var p = new AdaptiveQualityPolicy();
            p.ResetWindow(0f);
            Run(p, 60, 30);
            Assert.AreEqual(0, p.Level);
        }

        [Test]
        public void ASlowPhone_StepsDownUntilItHoldsItsTarget()
        {
            var p = new AdaptiveQualityPolicy();
            p.ResetWindow(0f);
            Run(p, 40, 2.5f);
            Assert.AreEqual(1, p.Level, "one step after one bad 2 s window");
            Run(p, 40, 12f);
            Assert.AreEqual(AdaptiveQualityPolicy.Ladder.Length - 1, p.Level, "ends on the steady-30 step");
            Assert.AreEqual(30, p.Current.TargetFps);
        }

        [Test]
        public void Headroom_StepsBackUpSlowly_ButNeverFromThirty()
        {
            var p = new AdaptiveQualityPolicy(2);
            p.ResetWindow(0f);
            Run(p, 60, 5f);
            Assert.AreEqual(2, p.Level, "needs 8 s of headroom");
            Run(p, 60, 12f);
            Assert.AreEqual(1, p.Level);

            var capped = new AdaptiveQualityPolicy(AdaptiveQualityPolicy.Ladder.Length - 1);
            capped.ResetWindow(0f);
            Run(capped, 30, 60f);
            Assert.AreEqual(AdaptiveQualityPolicy.Ladder.Length - 1, capped.Level);
        }

        [Test]
        public void PausesAndLoadHitches_DontCount()
        {
            var p = new AdaptiveQualityPolicy();
            p.ResetWindow(0f);
            for (var i = 0; i < 10; i++) p.Sample(2f); // a 2 s frame = a pause or a load, not gameplay
            Run(p, 60, 4f);
            Assert.AreEqual(0, p.Level);
        }

        [Test]
        public void Ladder_OnlyEverGetsCheaper()
        {
            var l = AdaptiveQualityPolicy.Ladder;
            for (var i = 1; i < l.Length; i++)
            {
                Assert.LessOrEqual(l[i].RenderScale, l[i - 1].RenderScale);
                Assert.LessOrEqual(l[i].TargetFps, l[i - 1].TargetFps);
                Assert.IsTrue(!l[i].PostFx || l[i - 1].PostFx);
            }
        }
    }
}
