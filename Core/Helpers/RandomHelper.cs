namespace CoreAPI.Core.Helpers;

public static class RandomHelper
{
    // Use Random.Shared for thread-safe random number generation (available from .NET 6+)
    private static readonly Random random = Random.Shared;

    // A universal method for generating a random value in a range
    public static uint GenerateRandomInRange(uint minValue, uint maxValue, string rangeName)
    {
        // Checking that the maximum value is suitable for int
        if (maxValue > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException($"{rangeName} exceeds valid int range.");
        }

        // If the minimum value is equal to the maximum, we return it
        return minValue == maxValue
            ? minValue
            : (uint)random.Next((int)minValue, (int)maxValue + 1);
    }

    public static List<T> GetRandomSubset<T>(List<T> sourceList, int count)
    {
        if (count > sourceList.Count)
        {
            throw new ArgumentException("Not enough elements in the source list.", nameof(sourceList));
        }

        return sourceList
            .OrderBy(_ => random.Next())  // Shuffle the list randomly
            .Take(count)                  // We take the required amount
            .ToList();
    }
}