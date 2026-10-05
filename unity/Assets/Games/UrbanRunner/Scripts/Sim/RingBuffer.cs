namespace UrbanRunner.Sim
{
    /// <summary>Fixed-capacity FIFO of structs. No allocations after construction; items are accessed by ref.</summary>
    public sealed class RingBuffer<T> where T : struct
    {
        readonly T[] _items;
        int _head;

        public int Count { get; private set; }
        public int Capacity => _items.Length;

        public RingBuffer(int capacity) { _items = new T[capacity]; }

        public ref T this[int i] => ref _items[(_head + i) % _items.Length];

        /// <summary>Adds at the back. When full, the oldest item is dropped (callers size buffers so this never happens).</summary>
        public ref T Push(in T item)
        {
            if (Count == _items.Length) PopFront();
            var idx = (_head + Count) % _items.Length;
            _items[idx] = item;
            Count++;
            return ref _items[idx];
        }

        public void PopFront()
        {
            if (Count == 0) return;
            _items[_head] = default;
            _head = (_head + 1) % _items.Length;
            Count--;
        }

        public ref T Front => ref _items[_head];

        public void Clear()
        {
            while (Count > 0) PopFront();
            _head = 0;
        }
    }
}
