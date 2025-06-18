using System;
using System.Collections.Generic;
using CoreAPI.Core.Interfaces;
using CoreAPI.Core.Models;

using CoreAPI.Core.Helpers; 

namespace CoreAPI.Core.Randomizers;

public static class WorkshopRandomizer
{
    // Use Random.Shared for thread-safe random number generation (available from .NET 6+)
    private static readonly Random random = Random.Shared;

    public static (string productionName, string manager, uint workerCount, List<string> productList, 
                uint workshopId, List<Brigade> brigades, List<Shift> shifts, List<ScheduleElement> schedule,
                int? x, int? y) 
    GenerateWorkshopFields(CoordinateRange? xRange = null, CoordinateRange? yRange = null)
    {
        // Randomly select a workshop name
        string productionName = productionsNames[random.Next(productionsNames.Count)];

        // Randomly choose a manager
        string manager = managerNames[random.Next(managerNames.Count)];

        // Generate random product list
        var productList = GenerateRandomProductList();

        // Generate worker count and workshop ID using the universal method
        uint workerCount = RandomHelper.GenerateRandomInRange(Production.MIN_EMPLOYEES_NUMBER, Production.MAX_EMPLOYEES_NUMBER, "Worker count");
        uint workshopId = RandomHelper.GenerateRandomInRange(Workshop.MIN_ID_NUMBER, Workshop.MAX_ID_NUMBER, "Workshop ID");

        // Generate a random schedule
        var (shifts, scheduleElements, brigadeCount) = GetRandomSchedule();

        // Generate random brigades
        var brigades = GenerateRandomBrigades(brigadeCount);

        int? x = (xRange.HasValue) ? (int)RandomHelper.GenerateRandomInRange((uint)xRange.Value.Min, (uint)xRange.Value.Max, "X")
                                   : null;

        int? y = (yRange.HasValue) ? (int)RandomHelper.GenerateRandomInRange((uint)yRange.Value.Min, (uint)yRange.Value.Max, "X")
                                   : null;

        return (productionName, manager, workerCount, productList, workshopId, brigades, shifts, scheduleElements, x, y);
    }




    // Generate a random workshop with random details
    public static Workshop Generate(CoordinateRange? xRange = null, CoordinateRange? yRange = null)
    {
        var fields = GenerateWorkshopFields(xRange, yRange);

        return new Workshop(fields.productionName, fields.manager, fields.workerCount, fields.productList,
                            fields.workshopId, fields.brigades, fields.shifts, fields.schedule, fields.x, fields.y);
    }

    // Generate a sequence of random workshops based on the given count
    public static IEnumerable<Workshop> GenerateMultiple(int count, CoordinateRange? xRange = null, CoordinateRange? yRange = null)
    {
        for (int i = 0; i < count; i++)
        {
            yield return Generate(xRange, yRange); // Lazily generates a random workshop
        }
    }

    private static MovementFunctionType GetRandomMovementFunctionType()
    {
        Array values = Enum.GetValues(typeof(MovementFunctionType));
        
        // Getting a random index value
        int randomIndex = random.Next(values.Length);   
        
        // Converting the array value to the desired MovementFunctionType
        return (MovementFunctionType)values.GetValue(randomIndex)!;
    }

    // Generate a random list of products (3 or more products, with no duplicates)
    private static List<string> GenerateRandomProductList()
    {
        int productCount = random.Next(3, availableProducts.Count + 1);
        return RandomHelper.GetRandomSubset(availableProducts, productCount);
    }

    // Generate a random list of brigades with unique brigade names
    private static List<Brigade> GenerateRandomBrigades(int brigadeCount)
    {
        return RandomHelper.GetRandomSubset(brigadeNames, brigadeCount)
               .Select((name, i) => new Brigade((uint)(i + 1), name))
               .ToList();
    }

    // Select a random schedule (either ThreeShiftFourBrigade or ThreeShiftFiveBrigade)
    private static (List<Shift> Shifts, List<ScheduleElement> ScheduleElements, int BrigadeCount) GetRandomSchedule()
    {
        // Randomly choose between 4 or 5 brigades
        int brigadeCount = random.Next(4, 6); // Generates either 4 or 5

        // Use switch to handle the chosen brigade count
        switch (brigadeCount)
        {
            case 4:
                return (StandardSchedules.ThreeShiftFourBrigade.Shifts, StandardSchedules.ThreeShiftFourBrigade.ScheduleElements, 4);
            case 5:
                return (StandardSchedules.ThreeShiftFiveBrigade.Shifts, StandardSchedules.ThreeShiftFiveBrigade.ScheduleElements, 5);
            default:
                throw new InvalidOperationException("Unexpected brigade count selected.");
        }
    }

    // List of possible workshop names
    private static List<string> productionsNames = new List<string>
    {
        "Global AutoWorks", "Future Motors", "EcoTech Engineering", "Titan Industries", "Mega Components",
        "AutoFusion", "Prime Parts Factory", "SpeedWorks", "Precision Systems", "EcoDrive Manufacturing"
    };

    // List of available products for generation
    private static List<string> availableProducts = new List<string>
    {
        "Engine Blocks", "Transmission Systems", "Brake Discs", "Suspension Components", "Electric Vehicle Batteries",
        "Fuel Injectors", "Turbochargers", "Hybrid Motors", "Gearbox Assemblies", "Automotive Sensors"
    };

    // List of possible brigade names
    private static List<string> brigadeNames = new List<string>
    {
        "Alpha", "Bronson", "Chuck-Norris", "Vortex", "Titan", "Omega", "Delta", "Eagle", "Phoenix", "Falcon"
    };

    // List of possible manager names
    private static List<string> managerNames = new List<string>
    {
        "Michael Reynolds", "John Doe", "Sarah Connors", "Bruce Wayne", "Tony Stark", "Clark Kent", "Steve Rogers"
    };
}