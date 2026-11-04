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

export interface BookedTransaction {
    id: string;
    type: 'Deposit' | 'Withdrawal' | 'Transfer';
    fromAccount: string | null;
    toAccount: string | null;
    amount: number; // display only
    currency: string;
    bookedAt: string;
}

/** The server answered with an error. `message` is safe to show to a person. */
export class ApiError extends Error {
    readonly status: number;

    constructor(status: number, message: string) {
        super(message);
        this.status = status;
    }
}

async function post(url: string, amountText: string): Promise<BookedTransaction> {
    // The amount is spliced in as text so it is never converted to a floating point number.
    const response = await fetch(url, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: `{"amount":${amountText}}`,
    });

    if (response.ok) {
        return response.json();
    }

    let detail: string | undefined;
    try {
        const problem = await response.json();
        detail = problem.detail ?? problem.title;
    } catch {
        // not a problem document
    }
    throw new ApiError(response.status, detail ?? `HTTP ${response.status}`);
}

const accountUrl = (accountNumber: string) => `/api/accounts/${encodeURIComponent(accountNumber)}`;

export const deposit = (accountNumber: string, amountText: string) =>
    post(`${accountUrl(accountNumber)}/deposits`, amountText);

export const withdraw = (accountNumber: string, amountText: string) =>
    post(`${accountUrl(accountNumber)}/withdrawals`, amountText);
