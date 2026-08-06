using MudBlazor;
using ZISK.Shared.Enums;
using ZISK.Shared.Localization;

namespace ZISK.Client.Display;

/// <summary>
/// Display text and MudBlazor colours for domain enums, in whichever language the visitor has
/// selected (see <see cref="Current"/>).
///
/// Every page reads its enum labels from here rather than declaring its own. An enum value that
/// renders as one word on the dashboard and a different word on the detail page reads like two
/// different things to a user, and per-page copies drift towards exactly that.
///
/// The text itself lives in <see cref="Translations"/>, which keeps both languages in one place.
/// See <see cref="Current"/> for how the ambient language gets set.
///
/// Note that attendance status is deliberately *not* fully covered here: CoachTrainingDetail derives its label
/// from a <c>TrainingAttendanceDto</c> using excuse state and how long ago the training started, which is a
/// different function that happens to share a name — see that page.
/// </summary>
public static class DomainLabels
{
    /// <summary>
    /// The visitor's current language. A plain static field rather than something threaded
    /// through every one of the ~40 call sites across the app - Blazor WASM runs single-threaded
    /// per browser tab, so this is safe, and it's what lets every existing
    /// <c>DomainLabels.XxxText(value)</c> call site stay untouched while still becoming
    /// reactive. LanguageService updates this on init and on every language switch.
    /// </summary>
    public static Lang Current { get; set; } = Lang.Sk;

    // ---- Roles -------------------------------------------------------------------------------------------

    public static string Role(string role) => role switch
    {
        "Admin" => Translations.Get(Current, "role.admin"),
        "Coach" => Translations.Get(Current, "role.coach"),
        "Parent" => Translations.Get(Current, "role.parent"),
        "Athlete" => Translations.Get(Current, "role.athlete"),
        "Child" => Translations.Get(Current, "role.child"),
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
        AttendanceStatus.Present => Translations.Get(Current, "domain.attendance.present"),
        AttendanceStatus.Absent => Translations.Get(Current, "domain.attendance.absent"),
        AttendanceStatus.Excused => Translations.Get(Current, "domain.attendance.excused"),
        _ => Translations.Get(Current, "domain.attendance.unknown")
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
        ExcuseStatus.Received => Translations.Get(Current, "domain.excuse.received"),
        _ => Translations.Get(Current, "domain.excuse.unknown")
    };

    public static Color ExcuseStatusColor(ExcuseStatus status) => status switch
    {
        ExcuseStatus.Received => Color.Info,
        _ => Color.Default
    };

    // ---- Announcement priority ---------------------------------------------------------------------------

    /// <summary>
    /// Rendered as "Dôležité" / "Stredná" / "Info" rather than a literal high/medium/low, which is
    /// how the club talks about announcements.
    /// </summary>
    public static string PriorityText(AnnouncementPriority priority) => priority switch
    {
        AnnouncementPriority.High => Translations.Get(Current, "domain.priority.high"),
        AnnouncementPriority.Medium => Translations.Get(Current, "domain.priority.medium"),
        _ => Translations.Get(Current, "domain.priority.low")
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
        TrainingType.Conditioning => Translations.Get(Current, "domain.training.conditioning"),
        TrainingType.Technical => Translations.Get(Current, "domain.training.technical"),
        TrainingType.Match => Translations.Get(Current, "domain.training.match"),
        TrainingType.Recovery => Translations.Get(Current, "domain.training.recovery"),
        _ => Translations.Get(Current, "domain.training.other")
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
