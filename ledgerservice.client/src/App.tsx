import { useCallback, useEffect, useState } from 'react';
import { getAccounts, type Account } from './api';
import AccountOperations from './AccountOperations';
import AuditTrail from './AuditTrail';
import { money } from './format';
import './App.css';

function App() {
    const [accounts, setAccounts] = useState<Account[]>();
    const [error, setError] = useState<string>();
    const [selected, setSelected] = useState<string>();
    const [auditVersion, setAuditVersion] = useState(0);

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

    // After a booking: show the new balances and the new audit entry.
    const onBooked = useCallback(async () => {
        try {
            setAccounts(await getAccounts());
            setError(undefined);
        } catch (e) {
            setError(e instanceof Error ? e.message : 'Could not refresh the accounts.');
        }
        setAuditVersion(v => v + 1);
    }, []);

    const selectedAccount = accounts?.find(a => a.accountNumber === selected);

    return (
        <div>
            <h1>Accounts</h1>
            {error && <p role="alert">{error}</p>}
            {accounts === undefined
                ? (error ? null : <p>Loading…</p>)
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
                                            Open
                                        </button>
                                    </td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                )}
            {selectedAccount && (
                <>
                    <AccountOperations key={selectedAccount.accountNumber} account={selectedAccount} onBooked={onBooked} />
                    <AuditTrail accountNumber={selectedAccount.accountNumber} version={auditVersion} />
                </>
            )}
        </div>
    );
}

export default App;
