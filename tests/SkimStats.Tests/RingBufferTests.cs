using SkimStats.Collections;

namespace SkimStats.Tests;

public class RingBufferTests
{
    [Fact]
    public void StartsEmpty()
    {
        var buffer = new RingBuffer<int>(3);

        Assert.Empty(buffer);
        Assert.Equal(3, buffer.Capacity);
    }

    [Fact]
    public void KeepsItemsInOrderBeforeFull()
    {
        var buffer = new RingBuffer<int>(3);
        buffer.Add(1);
        buffer.Add(2);

        Assert.Equal([1, 2], buffer);
    }

    [Fact]
    public void OverwritesOldestOnceFull()
    {
        var buffer = new RingBuffer<int>(3);
        for (int i = 1; i <= 5; i++)
            buffer.Add(i);

        // 1 and 2 got pushed out, still oldest to newest
        Assert.Equal([3, 4, 5], buffer);
        Assert.Equal(3, buffer.Count);
    }

    [Fact]
    public void StaysCorrectAfterWrappingManyTimes()
    {
        var buffer = new RingBuffer<int>(4);
        for (int i = 1; i <= 103; i++)
            buffer.Add(i);

        Assert.Equal([100, 101, 102, 103], buffer);
    }

    [Fact]
    public void CapacityOfOneKeepsOnlyNewest()
    {
        var buffer = new RingBuffer<string>(1);
        buffer.Add("a");
        buffer.Add("b");

        Assert.Equal(["b"], buffer);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RejectsCapacityBelowOne(int capacity)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RingBuffer<int>(capacity));
    }
}
