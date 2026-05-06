using ZISK.Data.Entities;

namespace ZISK.Helpers;

public static class AttendanceCalculator
{
    // Two variants exist because the return types are not interchangeable without precision loss:
    // double is required by MudBlazor chart components, decimal is used for EF/DTO aggregations.
    public static double CalculatePercentage(int present, int total)
        => total > 0 ? Math.Round((double)present / total * 100, 1) : 0;

    public static decimal CalculatePercentageDecimal(int present, int total)
        => total > 0 ? Math.Round((decimal)present / total * 100, 1) : 0;

    public static (int Present, int Absent, int Excused, int Total) CountByStatus(IEnumerable<AttendanceRecord> records)
    {
        var list = records as IList<AttendanceRecord> ?? records.ToList();
        return (
            Present: list.Count(r => r.Status == AttendanceStatus.Present),
            Absent: list.Count(r => r.Status == AttendanceStatus.Absent),
            Excused: list.Count(r => r.Status == AttendanceStatus.Excused),
            Total: list.Count
        );
    }
}
