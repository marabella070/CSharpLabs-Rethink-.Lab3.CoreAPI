using CoreAPI.Core.Helpers;
using CoreAPI.Core.Interfaces;
using CoreAPI.Core.Movements;

using System.Text;
using System.Drawing;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Versioning;

namespace CoreAPI.Core.Models;

public class Workshop : Production, IIdentifiable, IMoveable
{
    public const uint MIN_ID_NUMBER = 1;
    public const uint MAX_ID_NUMBER = int.MaxValue - 1;

    //! ID
    private uint _id;

    [Range(MIN_ID_NUMBER, MAX_ID_NUMBER, ErrorMessage = "ID must be greater than zero.")]
    public uint Id 
    {
        get => _id;
        set => ValidatorHelper.SetValueWithValidation(this, ref _id, nameof(Id), value); // Validation and assignment
    }

    //! BRIGADES
    private List<Brigade> _brigades;

    [MinLength(1, ErrorMessage = "Brigades must contain at least one brigade.")]
    public List<Brigade> Brigades
    {
        get => _brigades;
        set => ValidatorHelper.SetValueWithValidation(this, ref _brigades, nameof(Brigades), value); // Validation and assignment
    }

    //! SHIFTS
    private List<Shift> _shifts;

    [MinLength(1, ErrorMessage = "Shifts must contain at least one shift.")]
    public List<Shift> Shifts
    {
        get => _shifts;
        set => ValidatorHelper.SetValueWithValidation(this, ref _shifts, nameof(Shifts), value); // Validation and assignment
    }

    //! SCHEDULE
    private List<ScheduleElement> _schedule;

    [MinLength(1, ErrorMessage = "Schedule must contain at least one schedule element.")]
    public List<ScheduleElement> Schedule
    {
        get => _schedule;
        set => ValidatorHelper.SetValueWithValidation(this, ref _schedule, nameof(Schedule), value); // Validation and assignment
    }
    public int X { get; set; }
    public int Y { get; set; }
    public int ImageWidth { get; set; }
    public int ImageHeight { get; set; }
    public IMovementFunction MovementFunction { get; set; }

    // To initialize an empty object
    protected Workshop() : base() 
    { 
        _id = 0;
        _brigades = new List<Brigade>();
        _shifts = new List<Shift>();
        _schedule = new List<ScheduleElement>();
        X = 0;
        Y = 0;
        MovementFunction = new EmptyMovement();
    }

    public static Workshop CreateEmpty() => new Workshop(); // Allowed "empty" version
    
    public Workshop(string name, 
                    string manager, 
                    uint workerCount, 
                    List<string> productList, 
                    uint id,  
                    List<Brigade> brigades,
                    List<Shift> shifts,
                    List<ScheduleElement> schedule,
                    int? x = null,
                    int? y = null,
                    IMovementFunction? movementFunction = null,
                    int? imageWidth = null,
                    int? imageHeight = null)
        : base(name, manager, workerCount, productList)
    {
        _id = id;
        _brigades = brigades;
        _shifts = shifts;
        _schedule = schedule;

        X = x ?? 0;
        Y = y ?? 0;
        MovementFunction = movementFunction ?? new EmptyMovement();
        ImageWidth = imageWidth ?? 30;
        ImageHeight = imageHeight ?? 30;

        ValidatorHelper.ValidateObject(this);
    }

    public Workshop(Workshop other) : base(other)
    {
        _id = other._id;
        _brigades = new List<Brigade>(other._brigades);
        _shifts = new List<Shift>(other._shifts);
        _schedule = new List<ScheduleElement>(other._schedule);

        X = other.X;
        Y = other.Y;

        MovementFunction = other.MovementFunction;
    }

    public void Move(double timeElapsed, int boundaryX, int boundaryY)
    {
        // Checking for acceptable form sizes
        if (boundaryX < ImageWidth || boundaryY < ImageHeight)
        {
            throw new ArgumentException("The size of the shape must be larger than the size of the object!");
        }

        // Calculating the shift along the axes using a function of time
        var (dx, dy) = MovementFunction.Shift(timeElapsed);

        // Temporarily calculate the new positions
        int newX = X + dx;
        int newY = Y + dy;

        // Use Math.Clamp to ensure the new position is within the bounds
        X = Math.Clamp(newX, 0, boundaryX - ImageWidth);
        Y = Math.Clamp(newY, 0, boundaryY - ImageHeight);
    }

    /// <summary>
    /// Generates a string representation of the workshop information.
    /// </summary>
    /// <returns>A formatted string containing the production information, workshop ID, brigades, shifts, and schedule.</returns>
    /// <remarks>
    /// This method uses <see cref="GetProductionInfo"/> to include production details and <see cref="FormatList{T}"/> to format lists of brigades, shifts, and schedule.
    /// </remarks>
    public string GetWorkshopInfo()
    {
        var sb = new StringBuilder();
     
        sb.AppendLine($"Workshop №{Id}");

        // Formatting information about brigades
        sb.Append(FormatList(Brigades, "Brigades:", brigade => $" - Brigade {brigade.Id}: {brigade.Name}"));
        sb.AppendLine();

        // Formatting information about shifts
        sb.Append(FormatList(Shifts, "Shifts:", shift => $" - {shift}"));
        sb.AppendLine();

        // Formatting the schedule information
        sb.Append(FormatList(Schedule, "Schedule:", scheduleElement => $" - {scheduleElement}"));

        sb.AppendLine($"Position: X = {X}, Y = {Y}");
        sb.AppendLine($"Image Size: {ImageWidth} x {ImageHeight}");
        sb.AppendLine($"Movement Function: {MovementFunction.GetType().Name}");

        return sb.ToString();
    }

    public string GetShortWorkshopInfo()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Workshop №{Id}");
        return sb.ToString();
    }

    /// <summary>
    /// Displays the workshop information using the provided output method.
    /// </summary>
    /// <param name="output">A delegate that defines how the information should be output (e.g., to the console, a file, etc.).</param>
    /// <remarks>
    /// This method calls <see cref="GetWorkshopInfo"/> to generate the information string and then passes it to the <paramref name="output"/> delegate.
    /// </remarks>
    public override void ShowInfo(Action<string> output)
    {
        base.ShowInfo(output);
        output("\n" + GetWorkshopInfo());
    }

    public override void ShowShortInfo(Action<string> output)
    {
        base.ShowShortInfo(output);
        output("\n" + GetShortWorkshopInfo());
    }

    public override string ToString()
    {
        string productionPart = GetProductionInfo();
        string workshopPart = GetWorkshopInfo();
        return productionPart + "\n" + workshopPart;
    }

    /// <summary>
    /// Prints the schedule in a tabular format to the console.
    /// </summary>
    public void ShowSchedule(Action<string> output)
    {
        const int brigadeCellWidth = 14;
        const int daysCellWidth = 4;
        string dividingLine = new string('=', (daysCellWidth + 3) + (Shifts.Count * (brigadeCellWidth + 3) + 1));

        int cycleLength = CalculateScheduleLength(); // Length of the schedule cycle

        var brigadeMap = GenerateSchedule(); // Schedule generation

        if (brigadeMap == null)
        {
            output("Failed to generate schedule.");
            return;
        }

        // Output of the dividing line
        output(dividingLine + "\n");

        // Table header output
        output($"| {"days", -daysCellWidth} | ");
        foreach (var shift in Shifts)
        {
            output($"{shift, -brigadeCellWidth} | ");
        }

        // Output of the dividing line
        output("\n" + dividingLine + "\n");

        // Output of table rows
        for (int day = 0; day < cycleLength; ++day)
        {
            output($"| {day + 1, -daysCellWidth} | ");
            foreach (var shift in Shifts)
            {
                var key = (day, shift);

                // Checking whether a team has been assigned to a shift
                if (brigadeMap.TryGetValue(key, out var foundBrigade))
                {
                    output($"{foundBrigade.Name,-brigadeCellWidth} | ");
                }
                else
                {
                    output($"{"Empty",-brigadeCellWidth} | ");
                }
            }
            output("\n");
        }

        // Output of the dividing line
        output(dividingLine + "\n");
    }

    /// <summary>
    /// Generates a schedule mapping days and shifts to brigades.
    /// </summary>
    /// <returns>A dictionary where the key is a tuple of (day, shift) and the value is the assigned brigade.</returns>
    private Dictionary<(int, Shift), Brigade>? GenerateSchedule()
    {
        // Checking for null and empty collections
        if (Brigades?.Any() != true || Schedule?.Any() != true)
        {
            return null;
        }

        int cycleLength = CalculateScheduleLength();

        if (cycleLength < 0)
        {
            return null;
        }

        var brigadeMap = new Dictionary<(int, Shift), Brigade>();

        uint currentDay = 0;
        uint dayShift = Schedule[0].WorkDays;

        // Sorting through teams and schedule elements
        foreach (var brigade in Brigades)
        {
            foreach (var scheduleElement in Schedule)
            {
                // Assignment of working days
                foreach (var day in Enumerable.Range((int)currentDay, (int)scheduleElement.WorkDays))
                {
                    int keyDay = day % cycleLength;
                    brigadeMap[(keyDay, scheduleElement.Shift)] = brigade;
                }
                currentDay += (scheduleElement.WorkDays + scheduleElement.RelaxDays);
            }
            currentDay += dayShift;
        }
        return brigadeMap;
    }

    /// <summary>
    /// Calculates the length of the schedule cycle.
    /// </summary>
    /// <returns>The total length of the schedule cycle in days.</returns>
    private int CalculateScheduleLength()
    {
        return Schedule?.Sum(element => (int)(element.WorkDays + element.RelaxDays)) ?? 0;
    }
}