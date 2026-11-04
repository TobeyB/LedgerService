import { useRef, useState } from 'react';
import { ApiError, deposit, withdraw, type Account } from './api';
import { parseAmount } from './amount';
import { money } from './format';

type Kind = 'deposit' | 'withdraw';
type Message = { kind: 'success' | 'error'; text: string };

const UNKNOWN_OUTCOME = 'Check the audit trail before trying again.';

function describeError(e: unknown): string {
    if (e instanceof ApiError) {
        if (e.status === 400) return e.message;
        if (e.status === 404) return 'The account does not exist.';
        if (e.status === 422) return 'Insufficient funds: the balance is too low for this withdrawal. Nothing was changed.';
        return `The server reported an error (HTTP ${e.status}). The operation may not have been booked. ${UNKNOWN_OUTCOME}`;
    }
    // fetch rejects when no answer arrived, so the server may or may not have booked the operation.
    return `No answer from the server. The operation may or may not have been booked. ${UNKNOWN_OUTCOME}`;
}

export default function AccountOperations({ account, onBooked }: {
    account: Account;
    onBooked: () => Promise<void>;
}) {
    const [text, setText] = useState('');
    const [busy, setBusy] = useState(false);
    const [message, setMessage] = useState<Message>();
    // State updates are asynchronous, so a fast double-click could pass a state check twice.
    // A ref changes immediately and guarantees at most one request in flight.
    const inFlight = useRef(false);

    async function submit(kind: Kind) {
        if (inFlight.current) return;

        const parsed = parseAmount(text);
        if (!parsed.ok) {
            setMessage({ kind: 'error', text: parsed.error });
            return;
        }

        inFlight.current = true;
        setBusy(true);
        setMessage(undefined);
        try {
            const booked = await (kind === 'deposit' ? deposit : withdraw)(account.accountNumber, parsed.value);
            const verb = kind === 'deposit' ? 'Deposited' : 'Withdrew';
            setText('');
            setMessage({ kind: 'success', text: `${verb} ${money.format(booked.amount)} ${booked.currency}.` });
            await onBooked();
        } catch (e) {
            setMessage({ kind: 'error', text: describeError(e) });
        } finally {
            inFlight.current = false;
            setBusy(false);
        }
    }

    return (
        <section aria-label={`Deposit or withdraw for ${account.accountNumber}`}>
            <h2>{account.accountNumber} – {account.owner}</h2>
            <p>Balance: <strong>{money.format(account.balance)} {account.currency}</strong></p>
            <form onSubmit={e => e.preventDefault()}>
                <label>
                    Amount ({account.currency}){' '}
                    <input
                        value={text}
                        onChange={e => setText(e.target.value)}
                        inputMode="decimal"
                        autoComplete="off"
                        placeholder="1 500,50"
                        disabled={busy} />
                </label>{' '}
                <button type="button" disabled={busy} onClick={() => submit('deposit')}>Deposit</button>{' '}
                <button type="button" disabled={busy} onClick={() => submit('withdraw')}>Withdraw</button>
            </form>
            {busy && <p role="status">Working…</p>}
            {message?.kind === 'success' && <p role="status">{message.text}</p>}
            {message?.kind === 'error' && <p role="alert">{message.text}</p>}
        </section>
    );
}
