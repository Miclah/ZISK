using MudBlazor;
using ZISK.Shared.Enums;

namespace ZISK.Client.Display;

/// <summary>
/// Slovak display text and MudBlazor colours for domain enums.
///
/// These mappings were previously re-declared inside each page's @code block. That was not merely repetition —
/// the copies had diverged: <c>AnnouncementPriority.High</c> rendered as "Vysoká" on the admin dashboard but
/// "Dôležité" on every other page, and <c>Low</c> appeared as "Nízka", "Info" and "Informačné" depending on
/// where you looked. Centralising them makes the label for a given value the same everywhere.
///
/// Note that attendance status is deliberately *not* fully covered here: CoachTrainingDetail derives its label
/// from a <c>TrainingAttendanceDto</c> using excuse state and how long ago the training started, which is a
/// different function that happens to share a name — see that page.
/// </summary>
public static class DomainLabels
{
    // ---- Roles -------------------------------------------------------------------------------------------

    public static string Role(string role) => role switch
    {
        "Admin" => "Admin",
        "Coach" => "Tréner",
        "Parent" => "Rodič",
        "Athlete" => "Športovec",
        "Child" => "Dieťa",
        _ => role
    };

    public static Color RoleColor(string role) => role switch
    {
        "Admin" => Color.Error,
        "Coach" => Color.Primary,
        "Parent" => Color.Secondary,
        "Athlete" => Color.Info,
        "Child" => Color.Success,
        _ => Color.Default
    };

    public static string RoleBorderColor(string role) => role switch
    {
        "Admin" => "var(--mud-palette-error)",
        "Coach" => "var(--mud-palette-primary)",
        "Parent" => "var(--mud-palette-secondary)",
        "Athlete" => "var(--mud-palette-info)",
        "Child" => "var(--mud-palette-success)",
        _ => "var(--mud-palette-default)"
    };

    // ---- Attendance --------------------------------------------------------------------------------------

    public static string AttendanceStatusText(AttendanceStatus status) => status switch
    {
        AttendanceStatus.Present => "Prítomný",
        AttendanceStatus.Absent => "Neprítomný",
        AttendanceStatus.Excused => "Ospravedlnený",
        _ => "Neznámy"
    };

    /// <summary>Success = present, Error = absent, Warning = excused — the convention used in every attendance view.</summary>
    public static Color AttendanceStatusColor(AttendanceStatus status) => status switch
    {
        AttendanceStatus.Present => Color.Success,
        AttendanceStatus.Absent => Color.Error,
        AttendanceStatus.Excused => Color.Warning,
        _ => Color.Default
    };

    /// <summary>CSS class for the attendance chip, styled in app.css.</summary>
    public static string AttendanceStatusChipClass(AttendanceStatus status) => status switch
    {
        AttendanceStatus.Present => "attendance-chip-present",
        AttendanceStatus.Absent => "attendance-chip-absent",
        AttendanceStatus.Excused => "attendance-chip-excused",
        _ => string.Empty
    };

    // ---- Excuse status -----------------------------------------------------------------------------------

    public static string ExcuseStatusText(ExcuseStatus status) => status switch
    {
        ExcuseStatus.Received => "Prijaté",
        _ => "Neznámy"
    };

    public static Color ExcuseStatusColor(ExcuseStatus status) => status switch
    {
        ExcuseStatus.Received => Color.Info,
        _ => Color.Default
    };

    // ---- Announcement priority ---------------------------------------------------------------------------

    /// <summary>
    /// Standardised on the wording the announcement pages and both dashboards already used ("Dôležité" /
    /// "Stredná" / "Info"). The admin dashboard previously said "Vysoká" / "Nízka" for the same values.
    /// </summary>
    public static string PriorityText(AnnouncementPriority priority) => priority switch
    {
        AnnouncementPriority.High => "Dôležité",
        AnnouncementPriority.Medium => "Stredná",
        _ => "Info"
    };

    public static Color PriorityColor(AnnouncementPriority priority) => priority switch
    {
        AnnouncementPriority.High => Color.Error,
        AnnouncementPriority.Medium => Color.Warning,
        _ => Color.Info
    };

    /// <summary>CSS variable, not a hex literal — priority borders are themed via MudBlazor palette vars.</summary>
    public static string PriorityBorderColor(AnnouncementPriority priority) => priority switch
    {
        AnnouncementPriority.High => "var(--mud-palette-error)",
        AnnouncementPriority.Medium => "var(--mud-palette-warning)",
        _ => "var(--mud-palette-info)"
    };

    // ---- Training type -----------------------------------------------------------------------------------

    public static string TrainingTypeText(TrainingType type) => type switch
    {
        TrainingType.Conditioning => "Kondičný",
        TrainingType.Technical => "Technický",
        TrainingType.Match => "Herný",
        TrainingType.Recovery => "Regeneračný",
        _ => "Iný"
    };

    public static Color TrainingTypeColor(TrainingType type) => type switch
    {
        TrainingType.Conditioning => Color.Primary,
        TrainingType.Technical => Color.Secondary,
        TrainingType.Match => Color.Tertiary,
        TrainingType.Recovery => Color.Info,
        _ => Color.Default
    };
}
