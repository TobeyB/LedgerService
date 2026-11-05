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

async function post(url: string, body: string, headers: Record<string, string> = {}): Promise<BookedTransaction> {
    const response = await fetch(url, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', ...headers },
        body,
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

// Amounts are spliced into the JSON as text so they are never converted to a floating point number.
const amountBody = (amountText: string) => `{"amount":${amountText}}`;

export const deposit = (accountNumber: string, amountText: string) =>
    post(`${accountUrl(accountNumber)}/deposits`, amountBody(amountText));

export const withdraw = (accountNumber: string, amountText: string) =>
    post(`${accountUrl(accountNumber)}/withdrawals`, amountBody(amountText));

/**
 * The idempotency key makes a retry safe: if the first request did go through, the server answers
 * with the first result instead of moving the money again.
 */
export const transfer = (fromAccount: string, toAccount: string, amountText: string, idempotencyKey: string) =>
    post(
        '/api/transfers',
        `{"fromAccount":${JSON.stringify(fromAccount)},"toAccount":${JSON.stringify(toAccount)},"amount":${amountText}}`,
        { 'Idempotency-Key': idempotencyKey });
