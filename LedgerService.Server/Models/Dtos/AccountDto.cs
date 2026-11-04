using LedgerService.Server.Models.Domain;

namespace LedgerService.Server.Models.Dtos;

public record AccountDto(
    string AccountNumber,
    string Owner,
    AccountType Type,
    decimal Balance,
    string Currency);
