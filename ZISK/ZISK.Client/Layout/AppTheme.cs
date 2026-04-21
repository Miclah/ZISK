using MudBlazor;

namespace ZISK.Client.Layout;

// AI Generated Theme
public static class AppTheme
{
    public static MudTheme SportClubTheme => new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#4F46E5",
            PrimaryContrastText = "#FFFFFF",
            Secondary = "#F97316",
            SecondaryContrastText = "#FFFFFF",
            Tertiary = "#06B6D4",
            TertiaryContrastText = "#FFFFFF",
            Success = "#16A34A",
            Warning = "#EAB308",
            Error = "#DC2626",
            Info = "#2563EB",
            Background = "#F8FAFC",
            Surface = "#FFFFFF",
            AppbarBackground = "#312E81",
            AppbarText = "#FFFFFF",
            DrawerBackground = "#FFFFFF",
            DrawerText = "#334155",
            DrawerIcon = "#64748B",
            TextPrimary = "#0F172A",
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
            OverlayDark = "rgba(15, 23, 42, 0.5)",
            OverlayLight = "rgba(255, 255, 255, 0.5)"
        },
        PaletteDark = new PaletteDark
        {
            Primary = "#818CF8",
            PrimaryContrastText = "#0F172A",
            Secondary = "#FB923C",
            SecondaryContrastText = "#0F172A",
            Tertiary = "#22D3EE",
            Success = "#4ADE80",
            Warning = "#FACC15",
            Error = "#F87171",
            Info = "#60A5FA",
            Background = "#0C0F1A",
            Surface = "#1A1F36",
            AppbarBackground = "#1A1F36",
            AppbarText = "#F8FAFC",
            DrawerBackground = "#1A1F36",
            DrawerText = "#E2E8F0",
            DrawerIcon = "#94A3B8",
            TextPrimary = "#F8FAFC",
            TextSecondary = "#94A3B8",
            TextDisabled = "#475569",
            ActionDefault = "#94A3B8",
            ActionDisabled = "#475569",
            Divider = "#252B43",
            TableLines = "#252B43",
            TableStriped = "#1A1F36",
            TableHover = "#252B43"
        },
        Typography = new Typography
        {
            Default = new DefaultTypography
            {
                FontFamily = ["Inter", "Segoe UI", "Roboto", "sans-serif"],
                FontSize = "0.9375rem",
                FontWeight = "400",
                LineHeight = "1.55"
            },
            H1 = new H1Typography { FontSize = "2rem", FontWeight = "700", LineHeight = "1.2", LetterSpacing = "-0.025em" },
            H2 = new H2Typography { FontSize = "1.5rem", FontWeight = "600", LineHeight = "1.3", LetterSpacing = "-0.02em" },
            H3 = new H3Typography { FontSize = "1.25rem", FontWeight = "600", LineHeight = "1.4", LetterSpacing = "-0.015em" },
            H4 = new H4Typography { FontSize = "1.125rem", FontWeight = "600", LineHeight = "1.4", LetterSpacing = "-0.01em" },
            H5 = new H5Typography { FontSize = "1rem", FontWeight = "600", LineHeight = "1.5" },
            H6 = new H6Typography { FontSize = "0.875rem", FontWeight = "600", LineHeight = "1.5" },
            Body1 = new Body1Typography { FontSize = "0.9375rem", LineHeight = "1.55" },
            Body2 = new Body2Typography { FontSize = "0.875rem", LineHeight = "1.5" },
            Button = new ButtonTypography { FontSize = "0.875rem", FontWeight = "600", TextTransform = "none", LetterSpacing = "0.01em" },
            Caption = new CaptionTypography { FontSize = "0.75rem", LineHeight = "1.5", LetterSpacing = "0.02em" },
            Subtitle1 = new Subtitle1Typography { FontSize = "1rem", FontWeight = "500", LetterSpacing = "-0.01em" },
            Subtitle2 = new Subtitle2Typography { FontSize = "0.875rem", FontWeight = "500" }
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "10px",
            DrawerWidthLeft = "280px",
            DrawerMiniWidthLeft = "72px",
            AppbarHeight = "64px"
        }
    };
}
