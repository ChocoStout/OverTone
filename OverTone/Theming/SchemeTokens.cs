using System.Text;

namespace OverTone.Theming;

/// <summary>How a <see cref="ThemePair"/> emits its dark-mode overrides. Combinable.</summary>
[Flags]
public enum DarkSelector
{
    /// <summary>Emit nothing for dark mode.</summary>
    None = 0,

    /// <summary>OS-driven: <c>@media (prefers-color-scheme: dark)</c>.</summary>
    Media = 1,

    /// <summary>Manual toggle: <c>[data-theme="dark"]</c> (framework-agnostic; Radix/shadcn style).</summary>
    DataTheme = 2,

    /// <summary>Manual toggle: a <c>.dark</c> class (Tailwind's default dark variant).</summary>
    ClassDark = 4,
}

/// <summary>
/// Serializes a <see cref="ColorScheme"/> / <see cref="ThemePair"/> to web design tokens. Roles become
/// CSS custom properties named <c>--{prefix}-{role}</c> and <c>--{prefix}-on-{role}</c>.
/// </summary>
public static class SchemeTokens
{
    // Fixed emit order + kebab names; HasOn marks roles that carry a paired text color.
    private static readonly (ColorRole Role, string Name, bool HasOn)[] Order =
    [
        (ColorRole.Primary, "primary", true),
        (ColorRole.Secondary, "secondary", true),
        (ColorRole.Tertiary, "tertiary", true),
        (ColorRole.Neutral, "neutral", false),
        (ColorRole.Background, "background", true),
        (ColorRole.Surface, "surface", true),
        (ColorRole.SurfaceVariant, "surface-variant", true),
        (ColorRole.Outline, "outline", false),
        (ColorRole.Success, "success", true),
        (ColorRole.Warning, "warning", true),
        (ColorRole.Error, "error", true),
        (ColorRole.Info, "info", true),
    ];

    /// <summary>Emits a single scheme as CSS custom properties inside a <c>:root</c> block.</summary>
    public static string AsCss(this ColorScheme scheme, string prefix = "color")
    {
        ArgumentNullException.ThrowIfNull(scheme);
        var sb = new StringBuilder();
        sb.AppendLine(":root {");
        AppendVars(sb, scheme, prefix, "    ");
        sb.AppendLine("}");
        return sb.ToString();
    }

    /// <summary>
    /// Emits light defaults in <c>:root</c> plus dark overrides, per <paramref name="darkSelector"/>
    /// (defaults to both an OS media query and a <c>[data-theme="dark"]</c> manual override).
    /// </summary>
    public static string AsCss(this ThemePair pair, string prefix = "color",
        DarkSelector darkSelector = DarkSelector.Media | DarkSelector.DataTheme)
    {
        ArgumentNullException.ThrowIfNull(pair);
        var sb = new StringBuilder();

        sb.AppendLine("/* Light (default) */");
        sb.AppendLine(":root {");
        AppendVars(sb, pair.Light, prefix, "    ");
        sb.AppendLine("}");

        if (darkSelector.HasFlag(DarkSelector.Media))
        {
            sb.AppendLine();
            sb.AppendLine("@media (prefers-color-scheme: dark) {");
            sb.AppendLine("    :root {");
            AppendVars(sb, pair.Dark, prefix, "        ");
            sb.AppendLine("    }");
            sb.AppendLine("}");
        }

        if (darkSelector.HasFlag(DarkSelector.DataTheme))
        {
            sb.AppendLine();
            sb.AppendLine("[data-theme=\"dark\"] {");
            AppendVars(sb, pair.Dark, prefix, "    ");
            sb.AppendLine("}");
        }

        if (darkSelector.HasFlag(DarkSelector.ClassDark))
        {
            sb.AppendLine();
            sb.AppendLine(".dark {");
            AppendVars(sb, pair.Dark, prefix, "    ");
            sb.AppendLine("}");
        }

        return sb.ToString();
    }

    private static void AppendVars(StringBuilder sb, ColorScheme scheme, string prefix, string indent)
    {
        foreach (var (role, name, hasOn) in Order)
        {
            if (!scheme.TryGet(role, out var rc)) continue;
            sb.AppendLine($"{indent}--{prefix}-{name}: {rc.Color.Hex};");
            if (hasOn)
                sb.AppendLine($"{indent}--{prefix}-on-{name}: {rc.On.Hex};");
            if (rc.Ramp is { } ramp)
                foreach (var shade in ramp)
                    sb.AppendLine($"{indent}--{prefix}-{name}-{shade.Step}: {shade.Color.Hex};");
        }
    }
}
