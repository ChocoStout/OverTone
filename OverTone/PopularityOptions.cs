namespace OverTone;

/// <summary>
/// Tuning options for the Popularity extractor. Injectable via DI or passed to the constructor.
/// </summary>
/// <param name="BitsPerChannel">Bits of precision kept per channel when building the histogram (1–8).</param>
public record PopularityOptions(int BitsPerChannel = 5);
