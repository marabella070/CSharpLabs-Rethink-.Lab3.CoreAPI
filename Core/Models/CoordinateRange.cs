namespace CoreAPI.Core.Models;

public struct CoordinateRange
{
    public int Min { get; }
    public int Max { get; }

    public CoordinateRange(int min, int max)
    {
        Min = min;
        Max = max;
    }
}