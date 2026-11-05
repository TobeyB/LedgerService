# Ledger back office

React + TypeScript page for the Ledger service: lists accounts, deposit, withdraw, transfer and the audit trail.

```bash
npm install
npm run dev     # dev server, proxies /api to the ASP.NET server
npm test        # unit tests (Vitest): amount parsing and idempotency-key logic
npm run lint
npm run build   # type-check + production build
```

Amounts are typed the Swedish way (`1 500,50`), parsed to exact decimal text and sent without ever becoming a floating point number.
