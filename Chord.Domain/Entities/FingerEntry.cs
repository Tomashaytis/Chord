namespace Chord.Domain.Entities;

public class FingerEntry(int start, FingerInterval interval, ChordNode node)
{
    public int Start { get; set; } = start;

    public FingerInterval Interval { get; set; } = interval;

    public ChordNode Node { get; set; } = node;
}