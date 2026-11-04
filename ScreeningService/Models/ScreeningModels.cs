using System.ComponentModel.DataAnnotations;

namespace ScreeningService.Models;

public enum Decision
{
    Approve,
    Reject
}

public class ScreeningRequest
{
    public string? RequestId { get; init; }

    [Required] public string? FromAccount { get; init; }

    [Required] public string? ToAccount { get; init; }

    [Range(typeof(decimal), "0.01", "79228162514264337593543950335")]
    public decimal Amount { get; init; }

    [Required] public string? Currency { get; init; }
}

public record ScreeningResponse(string? RequestId, Decision Decision);
