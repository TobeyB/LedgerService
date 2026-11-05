import { ApiError } from './api';

export interface PendingKey {
    key: string;
    /** What the key was made for. A key must never be reused for a different transfer. */
    signature: string;
}

/** A new random key. Falls back when `crypto.randomUUID` is unavailable (it needs a secure context). */
export function newKey(): string {
    if (typeof crypto.randomUUID === 'function') return crypto.randomUUID();
    return Array.from(crypto.getRandomValues(new Uint8Array(16)), b => b.toString(16).padStart(2, '0')).join('');
}

/**
 * One key per transfer the person wants to make. Pressing the button again for the very same transfer
 * (after no answer arrived) reuses the key, so the server cannot book it twice. Anything different gets a new key.
 */
export function keyFor(pending: PendingKey | undefined, signature: string, make: () => string = newKey): PendingKey {
    return pending?.signature === signature ? pending : { key: make(), signature };
}

/**
 * True when the server gave a final answer to this key (booked, or refused for good).
 * The key must then be dropped: the server would replay the same answer, even if things have since changed.
 * False when the outcome is unknown or temporary (no answer, 5xx, 409 still running, 503 screening down):
 * keep the key and the next press is a safe retry of the same transfer.
 */
export function isFinalAnswer(error: unknown): boolean {
    return error instanceof ApiError && [400, 404, 422].includes(error.status);
}
