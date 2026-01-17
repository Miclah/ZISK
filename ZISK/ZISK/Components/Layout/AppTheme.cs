using MudBlazor;

namespace ZISK.Components.Layout;

public static class AppTheme
{
    public static MudTheme SportClubTheme => new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#1E3A5F",
            PrimaryContrastText = "#FFFFFF",
            Secondary = "#FF6B35",
            SecondaryContrastText = "#FFFFFF",
            Tertiary = "#4ECDC4",
            TertiaryContrastText = "#FFFFFF",
            Success = "#2ECC71",
            Warning = "#F39C12",
            Error = "#E74C3C",
            Info = "#3498DB",
            Background = "#F8FAFC",
            Surface = "#FFFFFF",
            AppbarBackground = "#1E3A5F",
            AppbarText = "#FFFFFF",
            DrawerBackground = "#FFFFFF",
            DrawerText = "#334155",
            DrawerIcon = "#64748B",
            TextPrimary = "#1E293B",
            TextSecondary = "#64748B",
            TextDisabled = "#94A3B8",
            ActionDefault = "#64748B",
            ActionDisabled = "#CBD5E1",
            ActionDisabledBackground = "#F1F5F9",
            Divider = "#E2E8F0",
            DividerLight = "#F1F5F9",
            TableLines = "#E2E8F0",
            TableStriped = "#F8FAFC",
            TableHover = "#F1F5F9",
            OverlayDark = "rgba(30, 41, 59, 0.5)",
            OverlayLight = "rgba(255, 255, 255, 0.5)"
        },
        Typography = new Typography
        {
            Default = new DefaultTypography
            {
                FontFamily = ["Inter", "Segoe UI", "Roboto", "sans-serif"],
                FontSize = "0.9375rem",
                FontWeight = "400",
                LineHeight = "1.6"
            },
            Button = new ButtonTypography { FontSize = "0.875rem", FontWeight = "600", TextTransform = "none" }
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "12px"
        }
    };
}