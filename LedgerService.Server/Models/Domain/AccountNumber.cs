namespace LedgerService.Server.Models.Domain;

public static class AccountNumber
{
    /// <summary>
    /// The one canonical form of an account number: trimmed and upper-case (" nb-1001 " becomes "NB-1001").
    /// Applied to every account number that comes in, so what is compared, stored and returned never depends on
    /// how it was typed, or on the case sensitivity of the database.
    /// </summary>
    public static string Normalize(string value) => value.Trim().ToUpperInvariant();
}
