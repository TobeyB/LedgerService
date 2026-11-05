import { useCallback, useEffect, useState } from 'react';
import { getAccounts, type Account } from './api';
import AccountOperations from './AccountOperations';
import AuditTrail from './AuditTrail';
import TransferForm from './TransferForm';
import { money } from './format';

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
        <>
            <header className="page-header">
                <h1>Ledger back office</h1>
                <p>Accounts, deposits, withdrawals and transfers.</p>
            </header>

            <div className="stack">
                <section className="card" aria-label="Accounts">
                    <h2>Accounts</h2>
                    {error && <p role="alert" className="message error">{error}</p>}
                    {accounts === undefined
                        ? (error ? null : <p className="muted">Loading…</p>)
                        : (
                            <div className="table-wrap">
                                <table>
                                    <thead>
                                        <tr>
                                            <th>Account</th>
                                            <th>Owner</th>
                                            <th>Type</th>
                                            <th className="num">Balance</th>
                                            <th></th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        {accounts.map(a => (
                                            <tr key={a.accountNumber} className={selected === a.accountNumber ? 'selected' : undefined}>
                                                <td>{a.accountNumber}</td>
                                                <td>{a.owner}</td>
                                                <td>{a.type}</td>
                                                <td className="num">{money.format(a.balance)} {a.currency}</td>
                                                <td className="num">
                                                    <button
                                                        type="button"
                                                        className="secondary small"
                                                        aria-pressed={selected === a.accountNumber}
                                                        onClick={() => setSelected(selected === a.accountNumber ? undefined : a.accountNumber)}>
                                                        Open
                                                    </button>
                                                </td>
                                            </tr>
                                        ))}
                                    </tbody>
                                </table>
                            </div>
                        )}
                </section>

                {accounts && accounts.length >= 2 && <TransferForm accounts={accounts} onBooked={onBooked} />}

                {selectedAccount && (
                    <>
                        <AccountOperations key={selectedAccount.accountNumber} account={selectedAccount} onBooked={onBooked} />
                        <AuditTrail accountNumber={selectedAccount.accountNumber} version={auditVersion} />
                    </>
                )}
            </div>
        </>
    );
}

export default App;
