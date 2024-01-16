public class CircularBuffer<T>
{
    private T[] _buffer;

    private int _bufferSize;

    public CircularBuffer(int bufferSize)
    {
        _bufferSize = bufferSize;
        _buffer = new T[bufferSize];
    }

    public void Add(T item, int index) => _buffer[index % _bufferSize] = item;
    public T Get(int index) => _buffer[index % _bufferSize];
    public void Clear() => _buffer = new T[_bufferSize];
}
