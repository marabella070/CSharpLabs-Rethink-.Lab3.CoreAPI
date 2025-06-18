namespace CoreAPI.Core.Models;

using CoreAPI.Core.Helpers;
using System.ComponentModel.DataAnnotations;

/// <summary>
/// Represents a work shift with a start time, end time, and duration.
/// </summary>
public class Shift
{
    //! START_TIME
    private readonly TimeSpan _startTime;

    [Range(typeof(TimeSpan), "00:00:00", "23:59:59", ErrorMessage = "Start time must be within the range of a single day.")]
    public TimeSpan StartTime => _startTime;

    //! END_TIME
    private readonly TimeSpan _endTime;

    [Range(typeof(TimeSpan), "00:00:00", "23:59:59", ErrorMessage = "End time must be within the range of a single day.")]
    public TimeSpan EndTime => _endTime;

    /// <summary>
    /// Gets the duration of the shift.
    /// </summary>
    public TimeSpan Duration => _endTime - _startTime;

    /// <summary>
    /// Initializes a new instance of the <see cref="Shift"/> class with specified start and end times.
    /// </summary>
    /// <param name="startTime">The start time of the shift.</param>
    /// <param name="endTime">The end time of the shift.</param>
    public Shift(TimeSpan startTime, TimeSpan endTime)
    {
        _startTime = startTime;
        _endTime = endTime;

        ValidatorHelper.ValidateObject(this);
    }

    /// <summary>
    /// Returns a string representation of the shift in the format "hh:mm - hh:mm".
    /// </summary>
    /// <returns>A string that represents the shift's start and end times.</returns>
    public override string ToString()
    {
        return $"{_startTime:hh\\:mm} - {_endTime:hh\\:mm}";
    }

    /// <summary>
    /// Determines whether the specified <see cref="Shift"/> is equal to the current one.
    /// </summary>
    /// <param name="obj">The <see cref="Shift"/> to compare with the current object.</param>
    /// <returns><c>true</c> if the specified <see cref="Shift"/> is equal to the current one; otherwise, <c>false</c>.</returns>
    public override bool Equals(object? obj)
    {
        if (obj == null || GetType() != obj.GetType())
        {
            return false;
        }

        Shift other = (Shift)obj;

        return _startTime == other._startTime && _endTime == other._endTime;
    }

    /// <summary>
    /// Serves as a hash function for a <see cref="Shift"/> object.
    /// </summary>
    /// <returns>A hash code for the current <see cref="Shift"/>.</returns>
    public override int GetHashCode()
    {
        return HashCode.Combine(_startTime, _endTime);
    }

    /// <summary>
    /// Compares two <see cref="Shift"/> objects for equality.
    /// </summary>
    /// <param name="left">The first <see cref="Shift"/> object to compare.</param>
    /// <param name="right">The second <see cref="Shift"/> object to compare.</param>
    /// <returns><c>true</c> if both objects are equal; otherwise, <c>false</c>.</returns>
    public static bool operator ==(Shift left, Shift right)
    {
        if (ReferenceEquals(left, null))
        {
            return ReferenceEquals(right, null);
        }
        return left.Equals(right);
    }

    /// <summary>
    /// Compares two <see cref="Shift"/> objects for inequality.
    /// </summary>
    /// <param name="left">The first <see cref="Shift"/> object to compare.</param>
    /// <param name="right">The second <see cref="Shift"/> object to compare.</param>
    /// <returns><c>true</c> if both objects are not equal; otherwise, <c>false</c>.</returns>
    public static bool operator !=(Shift left, Shift right)
    {
        return !(left == right);
    }
}