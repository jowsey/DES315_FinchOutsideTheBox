namespace VOIP.Util
{
    public class RingBuffer<T>
    {
        private readonly int _capacity;
        private readonly T[] _buffer;

        private int _readPos;
        private int _writePos;

        private readonly object _lock = new();

        public RingBuffer(int capacity)
        {
            _capacity = capacity;
            _buffer = new T[capacity];
        }

        public int Available
        {
            get
            {
                lock (_lock) return (_writePos - _readPos + _capacity) % _capacity;
            }
        }

        public void Write(T[] data, int count = -1)
        {
            if (count < 0) count = data.Length;

            lock (_lock)
            {
                for (var i = 0; i < count; ++i)
                {
                    _buffer[_writePos] = data[i];
                    _writePos = (_writePos + 1) % _capacity;

                    // bump read position if overwriting unread data
                    if (_writePos == _readPos)
                    {
                        _readPos = (_readPos + 1) % _capacity;
                    }
                }
            }
        }

        public int ReadInto(T[] output, int count = -1)
        {
            if (count < 0) count = output.Length;

            lock (_lock)
            {
                var itemsRead = 0;
                for (var i = 0; i < count; ++i)
                {
                    if (_readPos == _writePos)
                    {
                        // no more data to read
                        break;
                    }

                    output[i] = _buffer[_readPos];
                    _readPos = (_readPos + 1) % _capacity;
                    itemsRead++;
                }

                return itemsRead;
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _readPos = 0;
                _writePos = 0;
            }
        }
    }
}