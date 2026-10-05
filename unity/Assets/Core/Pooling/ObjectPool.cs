using System;
using System.Collections.Generic;

namespace GameFactory.Core.Pooling
{
    /// <summary>
    /// Fixed-growth pool: prewarm before the run so nothing is created during it (HANDOVER §3.11).
    /// <see cref="GrowCount"/> counts creations after prewarm; the perf test asserts it stays 0.
    /// </summary>
    public sealed class ObjectPool<T> where T : class
    {
        readonly Func<T> _create;
        readonly Action<T> _onGet;
        readonly Action<T> _onRelease;
        readonly Stack<T> _free;
        bool _prewarmed;

        public int CountAll { get; private set; }
        public int CountActive => CountAll - _free.Count;
        public int GrowCount { get; private set; }

        public ObjectPool(Func<T> create, Action<T> onGet = null, Action<T> onRelease = null, int capacity = 32)
        {
            _create = create ?? throw new ArgumentNullException(nameof(create));
            _onGet = onGet;
            _onRelease = onRelease;
            _free = new Stack<T>(capacity);
        }

        public void Prewarm(int count)
        {
            for (var i = CountAll; i < count; i++)
            {
                var item = _create();
                CountAll++;
                _onRelease?.Invoke(item);
                _free.Push(item);
            }
            _prewarmed = true;
        }

        public T Get()
        {
            T item;
            if (_free.Count > 0) item = _free.Pop();
            else
            {
                item = _create();
                CountAll++;
                if (_prewarmed)
                {
                    GrowCount++;
                    GameLog.Write(GameLog.Perf, "pool grew to " + CountAll + " (" + typeof(T).Name + ")");
                }
            }
            _onGet?.Invoke(item);
            return item;
        }

        public void Release(T item)
        {
            _onRelease?.Invoke(item);
            _free.Push(item);
        }
    }
}
