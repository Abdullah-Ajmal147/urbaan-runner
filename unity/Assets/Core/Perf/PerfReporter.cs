using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;

namespace GameFactory.Core.Perf
{
    /// <summary>
    /// Logs "UR_PERF fps avg=.. min=.. ..." every <see cref="intervalS"/> seconds and on demand.
    /// The first <see cref="warmupS"/> seconds of each window set are ignored for the minimum.
    /// </summary>
    public sealed class PerfReporter : MonoBehaviour
    {
        public float intervalS = 10f;
        public float warmupS = 2f;

        readonly FrameStats _window = new FrameStats();
        readonly FrameStats _session = new FrameStats();
        float _sinceStart;
        float _sinceReport;

        ProfilerRecorder _drawCalls;
        ProfilerRecorder _batches;
        long _drawSum, _batchSum;
        int _drawSamples;

        public bool Measuring { get; set; }

        void OnEnable()
        {
            _drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            _batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
        }

        void OnDisable()
        {
            _drawCalls.Dispose();
            _batches.Dispose();
        }
        public FrameStats Session => _session;

        public void BeginSession()
        {
            _session.Reset();
            _window.Reset();
            _sinceStart = 0;
            _sinceReport = 0;
            Measuring = true;
        }

        void Update()
        {
            if (!Measuring) return;
            var dt = Time.unscaledDeltaTime;
            _sinceStart += dt;
            if (_sinceStart < warmupS) return;
            _window.Add(dt);
            _session.Add(dt);
            if (_drawCalls.Valid) { _drawSum += _drawCalls.LastValue; _batchSum += _batches.LastValue; _drawSamples++; }
            _sinceReport += dt;
            if (_sinceReport >= intervalS)
            {
                Report("window", _window);
                _window.Reset();
                _sinceReport = 0;
            }
        }

        public void Report(string label, FrameStats s)
        {
            GameLog.Write(GameLog.Perf,
                $"{label} fps_avg={s.AverageFps:F1} fps_min={s.MinFps:F1} frames={s.Frames} seconds={s.Seconds:F1} " +
                $"mono_mb={Profiler.GetMonoUsedSizeLong() / 1048576f:F1} total_alloc_mb={Profiler.GetTotalAllocatedMemoryLong() / 1048576f:F1} " +
                $"gc={System.GC.CollectionCount(0)} draw_calls_avg={(_drawSamples > 0 ? _drawSum / _drawSamples : -1)} batches_avg={(_drawSamples > 0 ? _batchSum / _drawSamples : -1)}");
            _drawSum = _batchSum = 0;
            _drawSamples = 0;
        }

        public void EndSession()
        {
            if (!Measuring) return;
            Report("session", _session);
            Measuring = false;
        }
    }
}
