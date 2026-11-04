namespace LedgerService.Server.Models.Dtos;

/// <summary>Request body for deposit and withdrawal. Amount is a JSON number, bound exactly as decimal.</summary>
public record AmountRequest(decimal Amount);
