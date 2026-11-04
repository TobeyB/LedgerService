export type ParsedAmount = { ok: true; value: string } | { ok: false; error: string };

// Any Unicode space separator: space, no-break space (what Intl produces for sv-SE), narrow no-break space, thin space.
const GROUP_SEPARATORS = /\p{Zs}/gu;

/**
 * Parses an amount typed by a person, e.g. "1 500,50", "1500.5" or "12".
 * Returns the exact decimal text to send to the server ("1500.50"). The value is never converted
 * to a floating point number, so no digit can be changed on the way.
 *
 * Spaces group thousands. Comma or dot is the decimal separator, and only one is allowed:
 * "1,500" is rejected (three decimals), never silently read as 1500.
 */
export function parseAmount(input: string): ParsedAmount {
    const text = input.trim().replace(GROUP_SEPARATORS, '');

    if (text === '') return { ok: false, error: 'Enter an amount.' };
    if (text.startsWith('-')) return { ok: false, error: 'The amount must be greater than zero.' };

    const match = /^(\d+)(?:[.,](\d*))?$/.exec(text);
    if (!match) return { ok: false, error: 'Enter the amount as digits, for example 1 500,50.' };

    const whole = match[1].replace(/^0+(?=\d)/, '');
    const fraction = match[2] ?? '';

    if (fraction.length > 2) return { ok: false, error: 'An amount can have at most two decimals.' };
    if (whole.length > 15) return { ok: false, error: 'The amount is too large.' };

    const value = `${whole}.${fraction.padEnd(2, '0')}`;
    if (/^0+\.00$/.test(value)) return { ok: false, error: 'The amount must be greater than zero.' };

    return { ok: true, value };
}
