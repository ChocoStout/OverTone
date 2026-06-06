namespace OverTone.Theming;

/// <summary>One step of a tonal ramp (e.g. step 500), keyed Tailwind-style 50…950.</summary>
public readonly record struct Shade(int Step, Rgb Color);

/// <summary>
/// A resolved scheme role: the role's color, the matching <see cref="On"/> color (text/icons placed on
/// it, chosen to meet the contrast target), and an optional tonal <see cref="Ramp"/> (50…950).
/// </summary>
public readonly record struct RoleColor(Rgb Color, Rgb On, IReadOnlyList<Shade>? Ramp = null);
