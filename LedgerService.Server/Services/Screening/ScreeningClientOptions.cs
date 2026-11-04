using System.ComponentModel.DataAnnotations;

namespace LedgerService.Server.Services.Screening;

public class ScreeningClientOptions
{
    public const string SectionName = "Screening";

    [Required]
    public string BaseUrl { get; set; } = "";
}
