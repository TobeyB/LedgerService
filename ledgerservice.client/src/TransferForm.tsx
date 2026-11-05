import { useRef, useState } from 'react';
import { ApiError, transfer, type Account } from './api';
import { parseAmount } from './amount';
import { money } from './format';
import { isFinalAnswer, keyFor, type PendingKey } from './idempotency';

type Message = { kind: 'success' | 'error'; text: string };

const NO_MONEY_MOVED = 'No money was moved.';

function describeError(e: unknown): string {
    if (e instanceof ApiError) {
        const detail = e.message.startsWith('HTTP') ? undefined : e.message;
        switch (e.status) {
            case 400:
                return `${detail ?? 'The transfer was not valid.'} ${NO_MONEY_MOVED}`;
            case 404:
                return `One of the accounts does not exist. ${NO_MONEY_MOVED}`;
            case 422:
                return detail?.includes('moved') ? detail : `${(detail ?? 'The transfer was refused').replace(/\.$/, '')}. ${NO_MONEY_MOVED}`;
            case 409:
                return 'The same transfer is still being processed. Wait a moment, then press Transfer again.';
            case 503:
                if (detail) return `${detail} Press Transfer again to retry.`;
        }
    }
    // No answer, or an error that does not say whether the transfer happened.
    return 'The transfer may or may not have been made, because no clear answer arrived. '
        + 'Pressing Transfer again is safe: it will not move the money twice. You can also check the transactions list.';
}

export default function TransferForm({ accounts, onBooked }: {
    accounts: Account[];
    onBooked: () => Promise<void>;
}) {
    const [from, setFrom] = useState(accounts[0]?.accountNumber ?? '');
    const [to, setTo] = useState(accounts[1]?.accountNumber ?? '');
    const [text, setText] = useState('');
    const [busy, setBusy] = useState(false);
    const [message, setMessage] = useState<Message>();
    // State updates are asynchronous, so a fast double-click could pass a state check twice.
    // A ref changes immediately and guarantees at most one request in flight.
    const inFlight = useRef(false);
    // The idempotency key of the transfer being attempted. Kept while the outcome is unknown,
    // so pressing the button again is a retry of the same transfer, never a second one.
    const pending = useRef<PendingKey>(undefined);

    async function submit() {
        if (inFlight.current) return;

        if (from === to) {
            setMessage({ kind: 'error', text: 'Choose two different accounts.' });
            return;
        }
        const parsed = parseAmount(text);
        if (!parsed.ok) {
            setMessage({ kind: 'error', text: parsed.error });
            return;
        }

        pending.current = keyFor(pending.current, `${from}|${to}|${parsed.value}`);

        inFlight.current = true;
        setBusy(true);
        setMessage(undefined);
        try {
            const booked = await transfer(from, to, parsed.value, pending.current.key);
            pending.current = undefined;
            setText('');
            setMessage({
                kind: 'success',
                text: `Transferred ${money.format(booked.amount)} ${booked.currency} from ${from} to ${to}.`,
            });
            await onBooked();
        } catch (e) {
            if (isFinalAnswer(e)) pending.current = undefined;
            setMessage({ kind: 'error', text: describeError(e) });
        } finally {
            inFlight.current = false;
            setBusy(false);
        }
    }

    const label = (a: Account) => `${a.accountNumber} – ${a.owner} (${money.format(a.balance)} ${a.currency})`;

    return (
        <section className="card" aria-label="Transfer">
            <h2>Transfer</h2>
            <form onSubmit={e => { e.preventDefault(); void submit(); }}>
                <div className="fields">
                    <label className="field">
                        From
                        <select value={from} onChange={e => setFrom(e.target.value)} disabled={busy}>
                            {accounts.map(a => <option key={a.accountNumber} value={a.accountNumber}>{label(a)}</option>)}
                        </select>
                    </label>
                    <label className="field">
                        To
                        <select value={to} onChange={e => setTo(e.target.value)} disabled={busy}>
                            {accounts.map(a => <option key={a.accountNumber} value={a.accountNumber}>{label(a)}</option>)}
                        </select>
                    </label>
                    <label className="field">
                        Amount (SEK)
                        <input
                            value={text}
                            onChange={e => setText(e.target.value)}
                            inputMode="decimal"
                            autoComplete="off"
                            placeholder="1 500,50"
                            disabled={busy} />
                    </label>
                </div>
                <div className="actions">
                    <button type="submit" disabled={busy}>Transfer</button>
                    <span className="muted">Transfers of 10 000 SEK or more are screened first.</span>
                </div>
            </form>
            {busy && <p role="status" className="message info">Working… screening can take a few seconds.</p>}
            {message?.kind === 'success' && <p role="status" className="message success">{message.text}</p>}
            {message?.kind === 'error' && <p role="alert" className="message error">{message.text}</p>}
        </section>
    );
}
