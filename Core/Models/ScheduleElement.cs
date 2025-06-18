namespace CoreAPI.Core.Models;

using CoreAPI.Core.Helpers;
using System.ComponentModel.DataAnnotations;

/// <summary>
/// Represents a schedule element containing work days, relaxation days, and a shift.
/// </summary>
public class ScheduleElement
{
    //! WORK_DAYS
    private uint _workDays;

    [Range(1, uint.MaxValue , ErrorMessage = "WorkDays must be greater than 0.")]
    public uint WorkDays
    {
        get => _workDays;
        set => ValidatorHelper.SetValueWithValidation(this, ref _workDays, nameof(WorkDays), value); // Validation and assignment
    }

    //! RELAX_DAYS
    private uint _relaxDays;

    [Range(1, uint.MaxValue , ErrorMessage = "RelaxDays must be greater than 0.")]
    public uint RelaxDays
    {
        get => _relaxDays;
        set => ValidatorHelper.SetValueWithValidation(this, ref _relaxDays, nameof(RelaxDays), value); // Validation and assignment
    }

    //! SHIFT
    private readonly Shift _shift;
    public Shift Shift => _shift;

    /// <summary>
    /// Initializes a new instance of the <see cref="ScheduleElement"/> class.
    /// </summary>
    /// <param name="workDays">The number of work days.</param>
    /// <param name="relaxDays">The number of relaxation days.</param>
    /// <param name="shift">The shift associated with the work days.</param>
    public ScheduleElement(uint workDays, uint relaxDays, Shift shift)
    {
        _workDays = workDays;
        _relaxDays = relaxDays;
        _shift = shift;

        ValidatorHelper.ValidateObject(this);
    }

    /// <summary>
    /// Returns a string representation of the <see cref="ScheduleElement"/>.
    /// </summary>
    /// <returns>A string describing the work days, shift, and relaxation days.</returns>
    public override string ToString()
    {
        return $"{WorkDays} work days ({Shift}), followed by {RelaxDays} relaxation days";
    }
}