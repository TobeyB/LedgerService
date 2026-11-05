import { describe, expect, it } from 'vitest';
import { ApiError } from './api';
import { isFinalAnswer, keyFor } from './idempotency';

describe('keyFor', () => {
    let n = 0;
    const make = () => `key-${++n}`;

    it('makes a new key for a new transfer', () => {
        expect(keyFor(undefined, 'A|B|10.00', make).key).toBe('key-1');
    });

    it('reuses the key when the very same transfer is sent again', () => {
        const first = keyFor(undefined, 'A|B|10.00', make);
        const again = keyFor(first, 'A|B|10.00', make);
        expect(again.key).toBe(first.key);
    });

    it.each(['B|A|10.00', 'A|C|10.00', 'A|B|10.01'])('makes a new key when the transfer changed (%s)', changed => {
        const first = keyFor(undefined, 'A|B|10.00', make);
        expect(keyFor(first, changed, make).key).not.toBe(first.key);
    });
});

describe('isFinalAnswer', () => {
    it.each([400, 404, 422])('HTTP %i is a final answer', status => {
        expect(isFinalAnswer(new ApiError(status, 'x'))).toBe(true);
    });

    it.each([409, 500, 502, 503])('HTTP %i keeps the key, because the outcome is temporary or unknown', status => {
        expect(isFinalAnswer(new ApiError(status, 'x'))).toBe(false);
    });

    it('treats "no answer at all" as unknown', () => {
        expect(isFinalAnswer(new TypeError('Failed to fetch'))).toBe(false);
    });
});
