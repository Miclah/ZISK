using MudBlazor;

namespace ZISK.Client.Layout;

// AI Generated Theme
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
        PaletteDark = new PaletteDark
        {
            Primary = "#60A5FA",
            PrimaryContrastText = "#0F172A",
            Secondary = "#FF8C5A",
            SecondaryContrastText = "#0F172A",
            Tertiary = "#5EEAD4",
            Success = "#4ADE80",
            Warning = "#FBBF24",
            Error = "#F87171",
            Info = "#38BDF8",
            Background = "#0F172A",
            Surface = "#1E293B",
            AppbarBackground = "#1E293B",
            AppbarText = "#F8FAFC",
            DrawerBackground = "#1E293B",
            DrawerText = "#E2E8F0",
            DrawerIcon = "#94A3B8",
            TextPrimary = "#F8FAFC",
            TextSecondary = "#94A3B8",
            TextDisabled = "#475569",
            ActionDefault = "#94A3B8",
            ActionDisabled = "#475569",
            Divider = "#334155",
            TableLines = "#334155",
            TableStriped = "#1E293B",
            TableHover = "#334155"
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
            H1 = new H1Typography { FontSize = "2rem", FontWeight = "700", LineHeight = "1.2" },
            H2 = new H2Typography { FontSize = "1.5rem", FontWeight = "600", LineHeight = "1.3" },
            H3 = new H3Typography { FontSize = "1.25rem", FontWeight = "600", LineHeight = "1.4" },
            H4 = new H4Typography { FontSize = "1.125rem", FontWeight = "600", LineHeight = "1.4" },
            H5 = new H5Typography { FontSize = "1rem", FontWeight = "600", LineHeight = "1.5" },
            H6 = new H6Typography { FontSize = "0.875rem", FontWeight = "600", LineHeight = "1.5" },
            Body1 = new Body1Typography { FontSize = "0.9375rem", LineHeight = "1.6" },
            Body2 = new Body2Typography { FontSize = "0.875rem", LineHeight = "1.5" },
            Button = new ButtonTypography { FontSize = "0.875rem", FontWeight = "600", TextTransform = "none" },
            Caption = new CaptionTypography { FontSize = "0.75rem", LineHeight = "1.5" },
            Subtitle1 = new Subtitle1Typography { FontSize = "1rem", FontWeight = "500" },
            Subtitle2 = new Subtitle2Typography { FontSize = "0.875rem", FontWeight = "500" }
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "12px",
            DrawerWidthLeft = "280px",
            DrawerMiniWidthLeft = "72px",
            AppbarHeight = "64px"
        }
    };
}