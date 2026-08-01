namespace ZISK.Client.Layout;

/// <summary>
/// Palette colours as hex strings, for the CSS/inline-style contexts where a MudBlazor <c>Color</c> enum value
/// cannot be used (gradients, alpha suffixes, chart series).
///
/// Seven pages each declared their own <c>private static readonly string _cPrimary = (string)AppTheme...</c>
/// set — 31 declarations in total, and they had begun to disagree about which ones existed. Reading them from
/// one place keeps <see cref="AppTheme"/> the single source of truth.
///
/// Prefer the MudBlazor <c>Color.*</c> enum wherever a component accepts it; reach for these only when a raw
/// hex string is genuinely required.
/// </summary>
public static class ThemeColors
{
    private static readonly MudBlazor.Palette Palette = AppTheme.SportClubTheme.PaletteLight;

    public static readonly string Primary = (string)Palette.Primary;
    public static readonly string Secondary = (string)Palette.Secondary;
    public static readonly string Tertiary = (string)Palette.Tertiary;
    public static readonly string Success = (string)Palette.Success;
    public static readonly string Warning = (string)Palette.Warning;
    public static readonly string Error = (string)Palette.Error;
    public static readonly string Info = (string)Palette.Info;
    public static readonly string TextSecondary = (string)Palette.TextSecondary;
    public static readonly string Divider = (string)Palette.Divider;
    public static readonly string ActionDefault = (string)Palette.ActionDefault;

    /// <summary>Muted caption styling, repeated verbatim across several pages as <c>_styleMuted</c>.</summary>
    public static readonly string MutedTextStyle = $"color:{TextSecondary};";

    /// <summary>
    /// A palette colour at ~10% alpha, the tint used behind rounded icon tiles.
    /// Relies on 8-digit hex (#RRGGBBAA), which every browser the app targets supports.
    /// </summary>
    public static string Tint(string hex) => $"{hex}1A";
}
