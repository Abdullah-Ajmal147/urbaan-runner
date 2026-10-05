using GameFactory.Core.Input;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode
{
    public class SwipeTests
    {
        const float MinSide = 1000f; // threshold = 60 px

        [TestCase(100, 10, SwipeDirection.Right)]
        [TestCase(-100, 10, SwipeDirection.Left)]
        [TestCase(10, 100, SwipeDirection.Up)]
        [TestCase(10, -100, SwipeDirection.Down)]
        public void Classify(float dx, float dy, SwipeDirection expected)
        {
            Assert.AreEqual(expected, SwipeDetector.Classify(new Vector2(dx, dy)));
        }

        [Test]
        public void FiresOnceWhileFingerStillDown()
        {
            var d = new SwipeDetector();
            d.Down(new Vector2(500, 500), 0f);
            Assert.AreEqual(SwipeDirection.None, d.Move(new Vector2(530, 500), 0.02f, MinSide));
            Assert.AreEqual(SwipeDirection.Right, d.Move(new Vector2(570, 500), 0.04f, MinSide));
            Assert.AreEqual(SwipeDirection.None, d.Move(new Vector2(700, 500), 0.06f, MinSide));
            Assert.AreEqual(SwipeDirection.None, d.Up(new Vector2(700, 500), 0.08f, MinSide));
        }

        [Test]
        public void TapIsNotASwipe()
        {
            var d = new SwipeDetector();
            d.Down(new Vector2(500, 500), 0f);
            Assert.AreEqual(SwipeDirection.None, d.Up(new Vector2(505, 498), 0.1f, MinSide));
        }

        [Test]
        public void SlowDragRestartsSoALaterFlickCounts()
        {
            var d = new SwipeDetector();
            d.Down(new Vector2(500, 500), 0f);
            Assert.AreEqual(SwipeDirection.None, d.Move(new Vector2(510, 500), 1.0f, MinSide));
            Assert.AreEqual(SwipeDirection.Up, d.Move(new Vector2(510, 600), 1.1f, MinSide));
        }

        [Test]
        public void InputBuffer_HoldsFor150ms()
        {
            var b = new InputBuffer();
            b.Push(SwipeDirection.Up, 1.0f);
            Assert.AreEqual(SwipeDirection.Up, b.Peek(1.14f));
            Assert.AreEqual(SwipeDirection.None, b.Peek(1.16f));
        }

        [Test]
        public void InputBuffer_ConsumeClears()
        {
            var b = new InputBuffer();
            b.Push(SwipeDirection.Left, 0f);
            Assert.AreEqual(SwipeDirection.Left, b.Consume(0.05f));
            Assert.AreEqual(SwipeDirection.None, b.Consume(0.06f));
        }
    }
}
