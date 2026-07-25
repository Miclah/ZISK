namespace ZISK.Data.Entities;

// [Flags] with powers of 2 lets the value be stored as a single int in the database using a bitmask.
// Weekdays=31 (Mon–Fri) and Weekend=96 (Sat+Sun) are pre-calculated combinations for convenience.
[Flags]
public enum Weekdays
{
    None = 0,
    Monday = 1,
    Tuesday = 2,
    Wednesday = 4,
    Thursday = 8,
    Friday = 16,
    Saturday = 32,
    Sunday = 64,
    Weekdays = 31,
    Weekend = 96
}
