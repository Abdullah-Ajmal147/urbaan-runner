using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GameFactory.Core.Input
{
    /// <summary>
    /// Reads touch (or mouse) from the Input System, plus keyboard arrows in the Editor, and raises swipes.
    /// </summary>
    public sealed class PointerSwipeSource : MonoBehaviour
    {
        public event Action<SwipeDirection> Swiped;

        readonly SwipeDetector _detector = new SwipeDetector();
        bool _wasDown;

        void Update()
        {
            var now = Time.unscaledTime;
            var minSide = Mathf.Min(Screen.width, Screen.height);
            var pointer = Pointer.current;
            if (pointer != null)
            {
                var down = pointer.press.isPressed;
                var pos = pointer.position.ReadValue();
                if (down && !_wasDown) _detector.Down(pos, now);
                else if (down) Raise(_detector.Move(pos, now, minSide));
                else if (_wasDown) Raise(_detector.Up(pos, now, minSide));
                _wasDown = down;
            }

            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.leftArrowKey.wasPressedThisFrame) Raise(SwipeDirection.Left);
                if (kb.rightArrowKey.wasPressedThisFrame) Raise(SwipeDirection.Right);
                if (kb.upArrowKey.wasPressedThisFrame) Raise(SwipeDirection.Up);
                if (kb.downArrowKey.wasPressedThisFrame) Raise(SwipeDirection.Down);
            }
        }

        void Raise(SwipeDirection d)
        {
            if (d != SwipeDirection.None) Swiped?.Invoke(d);
        }
    }
}
