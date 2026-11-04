export interface Account {
    accountNumber: string;
    owner: string;
    type: 'Savings' | 'FixedTerm' | 'Business';
    balance: number; // display only. Amounts that are sent to the server must never go through float.
    currency: string;
}

export interface AuditEntry {
    transactionId: string;
    type: 'Deposit' | 'Withdrawal' | 'Transfer';
    fromAccount: string | null;
    toAccount: string | null;
    amount: number; // display only
    currency: string;
    bookedAt: string;
    recordedAt: string;
}

export async function getAuditTrail(accountNumber: string, signal?: AbortSignal): Promise<AuditEntry[]> {
    const response = await fetch(`/api/accounts/${encodeURIComponent(accountNumber)}/audit`, { signal });
    if (!response.ok) {
        throw new Error(`Could not load audit trail (HTTP ${response.status}).`);
    }
    return response.json();
}

export async function getAccounts(signal?: AbortSignal): Promise<Account[]> {
    const response = await fetch('/api/accounts', { signal });
    if (!response.ok) {
        throw new Error(`Could not load accounts (HTTP ${response.status}).`);
    }
    return response.json();
}
