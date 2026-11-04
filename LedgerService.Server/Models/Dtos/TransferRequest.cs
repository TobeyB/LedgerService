namespace LedgerService.Server.Models.Dtos;

/// <summary>Amount is a JSON number, bound exactly as decimal.</summary>
public record TransferRequest(string FromAccount, string ToAccount, decimal Amount);
