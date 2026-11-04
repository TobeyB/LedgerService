import { useEffect, useState } from 'react';
import { getAuditTrail, type AuditEntry } from './api';
import { money, when } from './format';


export default function AuditTrail({ accountNumber, version }: { accountNumber: string; version: number }) {
    // Results are tagged with the account they belong to, so a slow response for a previously
    // selected account is never shown under the current one.
    const [loaded, setLoaded] = useState<{ account: string; entries?: AuditEntry[]; error?: string }>();

    useEffect(() => {
        const controller = new AbortController();
        getAuditTrail(accountNumber, controller.signal)
            .then(entries => setLoaded({ account: accountNumber, entries }))
            .catch((e: unknown) => {
                if (e instanceof DOMException && e.name === 'AbortError') return;
                setLoaded({ account: accountNumber, error: e instanceof Error ? e.message : 'Could not load audit trail.' });
            });
        return () => controller.abort();
    }, [accountNumber, version]);

    const current = loaded?.account === accountNumber ? loaded : undefined;

    return (
        <section aria-label={`Audit trail for ${accountNumber}`}>
            <h2>Audit trail – {accountNumber}</h2>
            {current?.error ? <p role="alert">{current.error}</p>
                : current?.entries === undefined ? <p>Loading…</p>
                : current.entries.length === 0 ? <p>No bookings yet.</p>
                : (
                    <table>
                        <thead>
                            <tr>
                                <th>Booked</th>
                                <th>Type</th>
                                <th>From</th>
                                <th>To</th>
                                <th style={{ textAlign: 'right' }}>Amount</th>
                            </tr>
                        </thead>
                        <tbody>
                            {current.entries.map(e => (
                                <tr key={e.transactionId}>
                                    <td>{when.format(new Date(e.bookedAt))}</td>
                                    <td>{e.type}</td>
                                    <td>{e.fromAccount ?? '–'}</td>
                                    <td>{e.toAccount ?? '–'}</td>
                                    <td style={{ textAlign: 'right' }}>{money.format(e.amount)} {e.currency}</td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                )}
        </section>
    );
}
