using System.ComponentModel.DataAnnotations;

namespace LedgerService.Server.Services.Screening;

public class ScreeningClientOptions
{
    public const string SectionName = "Screening";

    [Required]
    public string BaseUrl { get; set; } = "";

    /// <summary>Longest a single call to the screening service may take. The fake answers within 1.5 s.</summary>
    public TimeSpan AttemptTimeout { get; set; } = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Longest the whole screening may take, all attempts and waits together. A transfer waits at most this long
    /// and is then refused. Must be longer than <see cref="AttemptTimeout"/>.
    /// </summary>
    public TimeSpan TotalRequestTimeout { get; set; } = TimeSpan.FromSeconds(8);

    [Range(0, 10)]
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>Base wait between retries (growing and jittered). The library default of 2 s would use up the whole budget.</summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMilliseconds(500);
}
