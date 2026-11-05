import { useEffect, useState } from 'react';
import { getTransactions, type BookedTransaction } from './api';
import { money, when } from './format';

/** Positive when money came into the account, negative when it left. */
function signedAmount(t: BookedTransaction, accountNumber: string): string {
    const incoming = t.toAccount === accountNumber;
    return `${incoming ? '+' : '−'}${money.format(t.amount)} ${t.currency}`;
}

export default function TransactionList({ accountNumber, version }: { accountNumber: string; version: number }) {
    // Results are tagged with the account they belong to, so a slow response for a previously
    // selected account is never shown under the current one.
    const [loaded, setLoaded] = useState<{ account: string; items?: BookedTransaction[]; error?: string }>();

    useEffect(() => {
        const controller = new AbortController();
        getTransactions(accountNumber, controller.signal)
            .then(items => setLoaded({ account: accountNumber, items }))
            .catch((e: unknown) => {
                if (e instanceof DOMException && e.name === 'AbortError') return;
                setLoaded({ account: accountNumber, error: e instanceof Error ? e.message : 'Could not load transactions.' });
            });
        return () => controller.abort();
    }, [accountNumber, version]);

    const current = loaded?.account === accountNumber ? loaded : undefined;

    return (
        <section className="card" aria-label={`Transactions for ${accountNumber}`}>
            <h2>Transactions <span className="muted">– {accountNumber}</span></h2>
            {current?.error ? <p role="alert" className="message error">{current.error}</p>
                : current?.items === undefined ? <p className="muted">Loading…</p>
                : current.items.length === 0 ? <p className="muted">No transactions yet.</p>
                : (
                    <div className="table-wrap">
                        <table>
                            <thead>
                                <tr>
                                    <th>Booked</th>
                                    <th>Type</th>
                                    <th>From</th>
                                    <th>To</th>
                                    <th className="num">Amount</th>
                                </tr>
                            </thead>
                            <tbody>
                                {current.items.map(t => (
                                    <tr key={t.id}>
                                        <td>{when.format(new Date(t.bookedAt))}</td>
                                        <td>{t.type}</td>
                                        <td>{t.fromAccount ?? '–'}</td>
                                        <td>{t.toAccount ?? '–'}</td>
                                        <td className="num">{signedAmount(t, accountNumber)}</td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    </div>
                )}
        </section>
    );
}
