using UnityEngine;

namespace GameFactory.Core
{
    /// <summary>Tagged logging. Tags are shared with Flutter so one logcat filter sees both sides.</summary>
    public static class GameLog
    {
        public const string Game = "UR_GAME";
        public const string Bridge = "UR_BRIDGE";
        public const string Perf = "UR_PERF";
        public const string Error = "UR_ERROR";

        public static void Write(string tag, string message)
        {
            if (tag == Error) Debug.LogWarning(tag + " " + message);
            else Debug.Log(tag + " " + message);
        }
    }
}
