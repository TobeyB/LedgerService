import { describe, expect, it } from 'vitest';
import { parseAmount } from './amount';

const ok = (input: string) => {
    const r = parseAmount(input);
    if (!r.ok) throw new Error(`"${input}" was rejected: ${r.error}`);
    return r.value;
};

describe('parseAmount', () => {
    it.each([
        ['1 500,50', '1500.50'],
        ['1 500,50', '1500.50'], // no-break space, as produced by Intl for sv-SE
        ['1 500,50', '1500.50'], // narrow no-break space
        ['1500.50', '1500.50'],
        ['1500,5', '1500.50'],
        ['12', '12.00'],
        ['12,', '12.00'],
        ['0,01', '0.01'],
        ['  250  ', '250.00'],
        ['007,10', '7.10'],
        ['12 345 678,99', '12345678.99'],
    ])('reads %j as %s', (input, expected) => {
        expect(ok(input)).toBe(expected);
    });

    it('keeps every digit exactly, even where floating point would not', () => {
        expect(ok('900719925474099,01')).toBe('900719925474099.01');
        expect(ok('0,10')).toBe('0.10');
    });


    it.each([
        '', '   ', '0', '0,00', '000', '-5', '-0,50',
        '1,505', '10.001', '1,500', // a comma is a decimal separator, never thousands
        '1,2,3', '1.234,56', '1,234.56',
        'abc', '12 kr', '1e3', '+5', ',50', '.5',
        '1 000 000 000 000 000 000',
    ])('rejects %j', input => {
        expect(parseAmount(input).ok).toBe(false);
    });
});
