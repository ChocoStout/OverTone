namespace OverTone.Theming;

/// <summary>
/// A semantic slot in a UI color scheme. Each role carries a color and a matching "on" color
/// (text/icons that sit on top of it) — see <see cref="RoleColor"/>.
/// </summary>
public enum ColorRole
{
    /// <summary>The dominant brand color (buttons, links, active states).</summary>
    Primary,

    /// <summary>A complementary accent that supports the primary.</summary>
    Secondary,

    /// <summary>A third accent for extra contrast/variety.</summary>
    Tertiary,

    /// <summary>The default body text/foreground color (high contrast against the background).</summary>
    Neutral,

    /// <summary>The base page background.</summary>
    Background,

    /// <summary>A raised container surface (cards, sheets).</summary>
    Surface,

    /// <summary>A subtler surface tint for secondary containers.</summary>
    SurfaceVariant,

    /// <summary>Borders, dividers, and disabled outlines.</summary>
    Outline,

    /// <summary>Positive/confirmation status (green).</summary>
    Success,

    /// <summary>Caution status (amber).</summary>
    Warning,

    /// <summary>Error/destructive status (red).</summary>
    Error,

    /// <summary>Informational status (blue).</summary>
    Info,
}
