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
            Secondary = "#D97706",
            SecondaryContrastText = "#FFFFFF",
            Tertiary = "#2563EB",
            TertiaryContrastText = "#FFFFFF",
            Success = "#16A34A",
            Warning = "#D97706",
            Error = "#DC2626",
            Info = "#2563EB",
            Background = "#F1F5F9",
            Surface = "#FFFFFF",
            AppbarBackground = "#1E3A5F",
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
            OverlayDark = "rgba(15, 23, 42, 0.55)",
            OverlayLight = "rgba(255, 255, 255, 0.75)"
        },
        Typography = new Typography
        {
            Default = new DefaultTypography
            {
                FontFamily = ["Inter", "Segoe UI", "Roboto", "system-ui", "-apple-system", "sans-serif"],
                FontSize = "0.9375rem",
                FontWeight = "400",
                LineHeight = "1.55"
            },
            H1 = new H1Typography { FontSize = "2rem", FontWeight = "700", LineHeight = "1.2", LetterSpacing = "-0.025em" },
            H2 = new H2Typography { FontSize = "1.5rem", FontWeight = "700", LineHeight = "1.3", LetterSpacing = "-0.02em" },
            H3 = new H3Typography { FontSize = "1.25rem", FontWeight = "600", LineHeight = "1.4", LetterSpacing = "-0.015em" },
            H4 = new H4Typography { FontSize = "1.125rem", FontWeight = "600", LineHeight = "1.4", LetterSpacing = "-0.01em" },
            H5 = new H5Typography { FontSize = "1rem", FontWeight = "600", LineHeight = "1.5" },
            H6 = new H6Typography { FontSize = "0.875rem", FontWeight = "600", LineHeight = "1.5" },
            Subtitle1 = new Subtitle1Typography { FontSize = "1rem", FontWeight = "500", LineHeight = "1.5", LetterSpacing = "-0.01em" },
            Subtitle2 = new Subtitle2Typography { FontSize = "0.875rem", FontWeight = "500", LineHeight = "1.5" },
            Body1 = new Body1Typography { FontSize = "0.9375rem", FontWeight = "400", LineHeight = "1.55" },
            Body2 = new Body2Typography { FontSize = "0.875rem", FontWeight = "400", LineHeight = "1.5" },
            Button = new ButtonTypography { FontSize = "0.875rem", FontWeight = "500", LineHeight = "1.4", LetterSpacing = "0.01em", TextTransform = "none" },
            Caption = new CaptionTypography { FontSize = "0.75rem", FontWeight = "400", LineHeight = "1.5", LetterSpacing = "0.02em" },
            Overline = new OverlineTypography { FontSize = "0.6875rem", FontWeight = "600", LineHeight = "1.5", LetterSpacing = "0.08em", TextTransform = "uppercase" }
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "8px",
            DrawerWidthLeft = "280px",
            DrawerMiniWidthLeft = "72px",
            AppbarHeight = "64px"
        }
    };
}
