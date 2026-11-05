export interface Account {
    accountNumber: string;
    owner: string;
    type: 'Savings' | 'FixedTerm' | 'Business';
    balance: number; // display only. Amounts that are sent to the server must never go through float.
    currency: string;
}


export async function getTransactions(accountNumber: string, signal?: AbortSignal): Promise<BookedTransaction[]> {
    const response = await fetch(`/api/accounts/${encodeURIComponent(accountNumber)}/transactions`, { signal });
    if (!response.ok) {
        throw new Error(`Could not load transactions (HTTP ${response.status}).`);
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

/**
 * The idempotency key makes a retry safe: if the first request did go through, the server answers
 * with the first result instead of moving the money again.
 * The amount is spliced into the JSON as text so it is never converted to a floating point number.
 */
export const transfer = (fromAccount: string, toAccount: string, amountText: string, idempotencyKey: string) =>
    post(
        '/api/transfers',
        `{"fromAccount":${JSON.stringify(fromAccount)},"toAccount":${JSON.stringify(toAccount)},"amount":${amountText}}`,
        { 'Idempotency-Key': idempotencyKey });
