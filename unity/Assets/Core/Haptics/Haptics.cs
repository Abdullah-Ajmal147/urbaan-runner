using UnityEngine;

namespace GameFactory.Core.Haptics
{
    public enum HapticKind { Light, Medium, Heavy, Success }

    /// <summary>
    /// Android vibration with amplitude control (API 26+). Works standalone and inside the Flutter embed
    /// (it uses Unity's current activity). Never throws: haptics are a nicety.
    /// </summary>
    public static class Haptics
    {
        public static bool Enabled { get; set; } = true;

#if UNITY_ANDROID && !UNITY_EDITOR
        static AndroidJavaObject _vibrator;
        static AndroidJavaClass _effect;
        static bool _ready;
        static bool _failed;

        static bool Init()
        {
            if (_ready) return true;
            if (_failed) return false;
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                    _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                _effect = new AndroidJavaClass("android.os.VibrationEffect");
                _ready = _vibrator != null && _vibrator.Call<bool>("hasVibrator");
            }
            catch (System.Exception e)
            {
                _failed = true;
                GameLog.Write(GameLog.Game, "haptics unavailable: " + e.Message);
            }
            return _ready;
        }

        static void OneShot(long ms, int amplitude)
        {
            using (var fx = _effect.CallStatic<AndroidJavaObject>("createOneShot", ms, amplitude))
                _vibrator.Call("vibrate", fx);
        }
#endif

        public static void Play(HapticKind kind)
        {
            if (!Enabled) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!Init()) return;
            try
            {
                switch (kind)
                {
                    case HapticKind.Light: OneShot(12, 60); break;
                    case HapticKind.Medium: OneShot(22, 140); break;
                    case HapticKind.Heavy: OneShot(70, 255); break;
                    case HapticKind.Success:
                        using (var fx = _effect.CallStatic<AndroidJavaObject>("createWaveform", new long[] { 0, 30, 60, 30, 60, 60 }, new[] { 0, 120, 0, 160, 0, 255 }, -1))
                            _vibrator.Call("vibrate", fx);
                        break;
                }
            }
            catch (System.Exception) { /* device without amplitude control: stay silent */ }
#endif
        }
    }
}
