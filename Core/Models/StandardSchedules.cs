namespace CoreAPI.Core.Models;

/// <summary>
/// Provides standard shift schedules commonly used in Russia for continuous production facilities.
/// </summary>
public static class StandardSchedules
{
    /// <summary>
    /// Represents a three-shift, four-brigade schedule commonly used in Russia.
    /// </summary>
    /// <remarks>
    /// This schedule is optimal for continuous production facilities with a large number of workers.
    /// Workers are divided into four brigades, with three brigades working each day in their respective shifts,
    /// and one brigade resting. The typical shift distribution is as follows:
    /// <list type="bullet">
    ///   <item>Four days on the night shift (23:00–7:00), followed by two days off.</item>
    ///   <item>Four days on the day shift (7:00–15:00), followed by one day off.</item>
    ///   <item>Four days on the evening shift (15:00–23:00), followed by one day off.</item>
    /// </list>
    /// In this schedule, brigades work different hours per month (128, 178, 152, 184 hours),
    /// so the schedule is planned over several months to compensate for overtime in one month
    /// with undertime in others, ensuring compliance with the weekly working time norm on average.
    /// </remarks>
    public static readonly (List<Shift> Shifts, List<ScheduleElement> ScheduleElements) ThreeShiftFourBrigade = (
        new List<Shift>
        {
            new Shift(new TimeSpan(23, 0, 0), new TimeSpan(7, 0, 0)), /*Night shift*/
            new Shift(new TimeSpan(7, 0, 0), new TimeSpan(15, 0, 0)), /*Day shift*/
            new Shift(new TimeSpan(15, 0, 0), new TimeSpan(23, 0, 0)), /*Evening shift*/
        },
        new List<ScheduleElement>
        {
            new ScheduleElement(4, 2, new Shift(new TimeSpan(23, 0, 0), new TimeSpan(7, 0, 0))),    /* 4 days night shift, 2 days rest */
            new ScheduleElement(4, 1, new Shift(new TimeSpan(7, 0, 0), new TimeSpan(15, 0, 0))),    /* 4 days day shift, 1 day of rest */
            new ScheduleElement(4, 1, new Shift(new TimeSpan(15, 0, 0), new TimeSpan(23, 0, 0))),   /* 4 days evening shift, 1 day of rest */
        }
    );

    /// <summary>
    /// Represents a three-shift, five-brigade schedule (also known as the "72-hour schedule") commonly used in Russia.
    /// </summary>
    /// <remarks>
    /// Workers are divided into five brigades. The typical shift distribution is as follows:
    /// <list type="bullet">
    ///   <item>Two days on the day shift (8:00–16:00), followed by two days off (48 hours).</item>
    ///   <item>Two days on the evening shift (16:00–24:00), followed by one day off ("transitional" day, 24 hours).</item>
    ///   <item>Two days on the night shift (0:00–8:00), followed by three days off (one "recovery" day and two days off, totaling 72 hours).</item>
    /// </list>
    /// One cycle lasts 10 days. In a month, each brigade works between 136 and 152 hours.
    /// </remarks>
    public static readonly (List<Shift> Shifts, List<ScheduleElement> ScheduleElements) ThreeShiftFiveBrigade = (
        new List<Shift>
        {
            new Shift(new TimeSpan(0, 0, 0), new TimeSpan(8, 0, 0)),    /*Night shift*/
            new Shift(new TimeSpan(8, 0, 0), new TimeSpan(16, 0, 0)),   /*Day shift*/
            new Shift(new TimeSpan(16, 0, 0), new TimeSpan(0, 0, 0)),  /*Evening shift*/
        },
        new List<ScheduleElement>
        {
            new ScheduleElement(2, 3, new Shift(new TimeSpan(0, 0, 0), new TimeSpan(8, 0, 0))),    /* 2 days night shift, 3 days rest */
            new ScheduleElement(2, 2, new Shift(new TimeSpan(8, 0, 0), new TimeSpan(16, 0, 0))),   /* 2 days day shift, 2 days rest */
            new ScheduleElement(2, 1, new Shift(new TimeSpan(16, 0, 0), new TimeSpan(0, 0, 0))),  /* 2 days evening shift, 1 day of rest */
        }
    );
}