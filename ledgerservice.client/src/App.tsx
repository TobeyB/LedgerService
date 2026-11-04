import { useEffect, useState } from 'react';
import { getAccounts, type Account } from './api';
import AuditTrail from './AuditTrail';
import './App.css';

const money = new Intl.NumberFormat('sv-SE', { minimumFractionDigits: 2, maximumFractionDigits: 2 });

function App() {
    const [accounts, setAccounts] = useState<Account[]>();
    const [error, setError] = useState<string>();
    const [selected, setSelected] = useState<string>();

    useEffect(() => {
        const controller = new AbortController();
        getAccounts(controller.signal)
            .then(setAccounts)
            .catch((e: unknown) => {
                if (e instanceof DOMException && e.name === 'AbortError') return;
                setError(e instanceof Error ? e.message : 'Could not load accounts.');
            });
        return () => controller.abort();
    }, []);

    return (
        <div>
            <h1>Accounts</h1>
            {error ? <p role="alert">{error}</p>
                : accounts === undefined ? <p>Loading…</p>
                : (
                    <table aria-label="Accounts">
                        <thead>
                            <tr>
                                <th>Account</th>
                                <th>Owner</th>
                                <th>Type</th>
                                <th style={{ textAlign: 'right' }}>Balance</th>
                                <th></th>
                            </tr>
                        </thead>
                        <tbody>
                            {accounts.map(a => (
                                <tr key={a.accountNumber}>
                                    <td>{a.accountNumber}</td>
                                    <td>{a.owner}</td>
                                    <td>{a.type}</td>
                                    <td style={{ textAlign: 'right' }}>{money.format(a.balance)} {a.currency}</td>
                                    <td>
                                        <button
                                            type="button"
                                            aria-pressed={selected === a.accountNumber}
                                            onClick={() => setSelected(selected === a.accountNumber ? undefined : a.accountNumber)}>
                                            Audit trail
                                        </button>
                                    </td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                )}
            {selected && <AuditTrail accountNumber={selected} />}
        </div>
    );
}

export default App;
