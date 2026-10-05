using UnityEngine;

namespace GameFactory.Core.Input
{
    public enum SwipeDirection { None, Left, Right, Up, Down }

    /// <summary>
    /// Turns pointer down / move / up samples into swipes. Pure logic: feed it positions in pixels and
    /// times in seconds. A swipe fires as soon as the finger travels far enough, so the player doesn't
    /// have to lift the finger first.
    /// </summary>
    public sealed class SwipeDetector
    {
        /// <summary>Minimum travel, as a fraction of the screen's shorter side.</summary>
        public float MinDistanceFraction { get; set; } = 0.06f;

        /// <summary>Gestures slower than this are ignored (drags, not swipes).</summary>
        public float MaxDurationS { get; set; } = 0.6f;

        Vector2 _start;
        float _startTime;
        bool _tracking;
        bool _fired;

        public void Down(Vector2 pos, float time)
        {
            _start = pos;
            _startTime = time;
            _tracking = true;
            _fired = false;
        }

        /// <summary>Returns a swipe the first time the movement crosses the threshold.</summary>
        public SwipeDirection Move(Vector2 pos, float time, float screenMinSide)
        {
            if (!_tracking || _fired) return SwipeDirection.None;
            if (time - _startTime > MaxDurationS)
            {
                // Too slow: restart from here so a fresh flick still counts.
                _start = pos;
                _startTime = time;
                return SwipeDirection.None;
            }
            var delta = pos - _start;
            var min = MinDistanceFraction * screenMinSide;
            if (delta.sqrMagnitude < min * min) return SwipeDirection.None;
            _fired = true;
            return Classify(delta);
        }

        public SwipeDirection Up(Vector2 pos, float time, float screenMinSide)
        {
            var result = Move(pos, time, screenMinSide);
            _tracking = false;
            return result;
        }

        public static SwipeDirection Classify(Vector2 delta)
        {
            if (delta == Vector2.zero) return SwipeDirection.None;
            if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
                return delta.x > 0 ? SwipeDirection.Right : SwipeDirection.Left;
            return delta.y > 0 ? SwipeDirection.Up : SwipeDirection.Down; // screen space: +y is up
        }
    }

    /// <summary>
    /// Holds one pending input for a short window (HANDOVER §3.3: 0.15 s) so a slightly early swipe still counts.
    /// </summary>
    public sealed class InputBuffer
    {
        public float WindowS { get; }
        SwipeDirection _pending;
        float _pendingTime;

        public InputBuffer(float windowS = 0.15f) { WindowS = windowS; }

        public void Push(SwipeDirection dir, float time)
        {
            if (dir == SwipeDirection.None) return;
            _pending = dir;
            _pendingTime = time;
        }

        /// <summary>Returns the buffered input if it is still fresh, without consuming it.</summary>
        public SwipeDirection Peek(float time) =>
            _pending != SwipeDirection.None && time - _pendingTime <= WindowS ? _pending : SwipeDirection.None;

        public SwipeDirection Consume(float time)
        {
            var d = Peek(time);
            _pending = SwipeDirection.None;
            return d;
        }
    }
}
