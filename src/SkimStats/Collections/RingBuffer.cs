using System;
using System.Collections;
using System.Collections.Generic;

namespace SkimStats.Collections;

// fixed-size history, once full the oldest item gets overwritten
public sealed class RingBuffer<T> : IEnumerable<T>
{
    private readonly T[] _items;
    private int _start; // index of the oldest item

    public RingBuffer(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _items = new T[capacity];
    }

    public int Capacity => _items.Length;
    public int Count { get; private set; }

    public void Add(T item)
    {
        if (Count < Capacity)
        {
            _items[(_start + Count) % Capacity] = item;
            Count++;
        }
        else
        {
            // full, overwrite the oldest and move the start forward
            _items[_start] = item;
            _start = (_start + 1) % Capacity;
        }
    }

    // oldest to newest
    public IEnumerator<T> GetEnumerator()
    {
        for (int i = 0; i < Count; i++)
            yield return _items[(_start + i) % Capacity];
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
