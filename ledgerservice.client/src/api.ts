export interface Account {
    accountNumber: string;
    owner: string;
    type: 'Savings' | 'FixedTerm' | 'Business';
    balance: number; // display only. Amounts that are sent to the server must never go through float.
    currency: string;
}

export async function getAccounts(signal?: AbortSignal): Promise<Account[]> {
    const response = await fetch('/api/accounts', { signal });
    if (!response.ok) {
        throw new Error(`Could not load accounts (HTTP ${response.status}).`);
    }
    return response.json();
}
