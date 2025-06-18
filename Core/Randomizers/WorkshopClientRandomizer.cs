namespace CoreAPI.Core.Randomizers;

using Client_v.Core;
using CoreAPI.Core.Models;

public static class WorkshopClientRandomizer
{
    // Generate a random WorkshopClient with random details
    public static WorkshopClient Generate(string host, int port, CoordinateRange? xRange = null, CoordinateRange? yRange = null)
    {
        Workshop workshopPart = WorkshopRandomizer.Generate(xRange, yRange);

        Client clientPart = new Client(host, port);

        return new WorkshopClient(workshopPart, clientPart);
    }

    // Generate a sequence of random workshops based on the given count
    public static IEnumerable<WorkshopClient> GenerateMultiple(int count, string host, int port, CoordinateRange? xRange = null, CoordinateRange? yRange = null)
    {
        for (int i = 0; i < count; i++)
        {
            yield return Generate(host, port, xRange, yRange); // Lazily generates a random workshopClient
        }
    }
}