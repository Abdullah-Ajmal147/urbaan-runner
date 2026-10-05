namespace GameFactory.Core.Perf
{
    /// <summary>
    /// Allocation-free frame-time accumulator. Reports average and minimum FPS over a window (HANDOVER §10.4).
    /// </summary>
    public sealed class FrameStats
    {
        double _sum;
        float _worst;
        int _frames;

        public int Frames => _frames;
        public double Seconds => _sum;

        public void Add(float dt)
        {
            if (dt <= 0f) return;
            _sum += dt;
            _frames++;
            if (dt > _worst) _worst = dt;
        }

        public float AverageFps => _sum > 0 ? (float)(_frames / _sum) : 0f;
        public float MinFps => _worst > 0 ? 1f / _worst : 0f;

        public void Reset()
        {
            _sum = 0;
            _worst = 0;
            _frames = 0;
        }
    }
}
