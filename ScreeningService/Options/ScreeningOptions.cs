namespace ScreeningService.Options;

public class ScreeningOptions
{
    public const string SectionName = "Screening";

    public int MinDelayMs { get; set; } = 50;

    public int MaxDelayMs { get; set; } = 1500;

    /// <summary>Share of calls (0 to 1) that answer 429 or 503.</summary>
    public double FailureRate { get; set; } = 0.2;

    /// <summary>Screenings involving this account are always rejected.</summary>
    public string RejectedAccount { get; set; } = "NB-2001";
}
