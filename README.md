# Ledger

A small account service that keeps customer accounts and moves money between them, built to avoid the failures of an earlier prototype: balances that drifted, negative balances, double charges on retry, transfers while a check was down, phantom history and personal data in logs.

Everything here is fictional.

| Part | What | Technology |
|---|---|---|
| `LedgerService.Server` | The service: accounts, deposit, withdraw, transfer, transaction list, audit trail | ASP.NET Core, EF Core, SQL Server |
| `ScreeningService` | A fake of the external screening service | ASP.NET Core |
| `ledgerservice.client` | Back-office page: list accounts, make a transfer, see an account's transactions | React, TypeScript, Vite |
| `LedgerService.Tests` | Tests against a real SQL Server (Testcontainers) | xUnit |

## Run everything

You need Docker. From the repository root:

```bash
docker compose up -d --build --wait
```

This starts four containers:

| Service | URL | Notes |
|---|---|---|
| Web page | http://localhost:3000 | Proxies `/api` to the server |
| Ledger server | http://localhost:5120 | Applies migrations and seeds demo accounts on startup (Development only) |
| Screening fake | http://localhost:5217 | `POST /v1/screenings` |
| SQL Server | `localhost,1433` | User `sa`, development password in `docker-compose.yml` |

The server needs a few seconds after "up" to migrate and seed. A request in that moment can get a 502 from the proxy; try again.

Only the database, if you want to run the .NET projects from your IDE:

```bash
docker compose up -d sqlserver
```

Wipe all data and start fresh:

```bash
docker compose down -v
```

### Seed data

| Account | Owner | Type | Balance (SEK) |
|---|---|---|---|
| NB-1001 | Anna Lindqvist | Savings | 50 000,00 |
| NB-1002 | Erik Johansson | Savings | 12 500,50 |
| NB-1003 | Maria Svensson | FixedTerm | 100 000,00 |
| NB-1004 | Nordic Widgets AB | Business | 250 000,00 |
| NB-2001 | Shadow Holdings AB | Business | 500 000,00 |

**NB-2001 is the company that screening always rejects**, whichever side of a transfer it is on (and only for transfers of 10 000 SEK or more, since smaller ones are not screened).

### Tests

```bash
cd LedgerService.Tests
dotnet test            # needs Docker running: starts a real SQL Server container

cd ledgerservice.client
npm install
npm test               # Vitest: Swedish amount parsing and idempotency-key logic
```

## Examples

Amounts are JSON numbers with at most two decimals. On Windows PowerShell use `curl.exe`.

**List accounts**

```bash
curl localhost:5120/api/accounts
```

**Deposit**

```bash
curl -X POST localhost:5120/api/accounts/NB-1002/deposits \
  -H "Content-Type: application/json" -d '{"amount": 100.50}'
```

**Withdraw**

```bash
curl -X POST localhost:5120/api/accounts/NB-1002/withdrawals \
  -H "Content-Type: application/json" -d '{"amount": 40.25}'
```

**Transfer** (add `Idempotency-Key` so a retry cannot move the money twice; use a new key for each new transfer)

```bash
curl -X POST localhost:5120/api/transfers \
  -H "Content-Type: application/json" -H "Idempotency-Key: demo-1" \
  -d '{"fromAccount":"NB-1001","toAccount":"NB-1002","amount":12000}'
```

Send the same request again: the answer is identical and the money moved once. A transfer of 10 000 SEK or more is screened first and can take a few seconds. Try `NB-2001` as the target to see a rejection.

**List the transactions of an account** (newest first)

```bash
curl localhost:5120/api/accounts/NB-1002/transactions
```

**List the audit trail of an account** (oldest first)

```bash
curl localhost:5120/api/accounts/NB-1002/audit
```

**Screening fake directly**

```bash
curl -X POST localhost:5217/v1/screenings -H "Content-Type: application/json" \
  -d '{"requestId":"r1","fromAccount":"NB-1001","toAccount":"NB-2001","amount":15000,"currency":"SEK"}'
```

### Errors

Errors are RFC 9457 problem documents.

| Status | When |
|---|---|
| 400 | Invalid amount (zero, negative, more than two decimals), same source and target, bad `Idempotency-Key` |
| 404 | Unknown account |
| 409 | A request with the same `Idempotency-Key` is still running; retry shortly |
| 422 | Insufficient funds; refused by screening; `Idempotency-Key` reused for a different request |
| 503 | Screening could not be reached, so the transfer was not made (`Retry-After` is set) |

## How it works

### Money and the balance rules

- Amounts are `decimal` and are validated by a small `Money` type: positive, at most two decimals. The currency is always SEK. Amounts are always written with two decimals so `12000` and `12000.00` are the same everywhere.
- The balance never goes negative: a withdrawal or the debit of a transfer is one SQL statement, `UPDATE … SET Balance = Balance - @amount WHERE … AND Balance >= @amount`. There is no read-modify-write, so concurrent requests cannot both spend the same money. A database `CHECK (Balance >= 0)` backs it up.
- A transfer updates both accounts, writes the transaction and its audit entry, and completes its idempotency record in **one database transaction**. Either all of it happens or none of it. Accounts are always updated in the same order (by account number), so transfers in opposite directions wait for each other instead of deadlocking.
- Money is never created or destroyed: a transfer debits and credits the same amount inside that one transaction.

### Idempotency

- A transfer sent with an `Idempotency-Key` happens at most once, and every retry gets the first answer (the same transaction, the same timestamp).
- The first request claims the key by inserting a row whose primary key is the key. Two concurrent first requests cannot both claim it.
- The result (the booked transaction, or a definitive refusal such as insufficient funds) is stored with the booking, in the same database transaction.
- The same key with a different request is refused (422). A request whose key is still being processed gets 409.
- A claim left behind by a crashed request is taken over after two minutes. The database also refuses a second booking with the same key, as a backstop.
- The web page makes one key per transfer, keeps it while the outcome is unknown (so pressing the button again is a safe retry) and drops it after a final answer.

### Screening

- Transfers of **10 000 SEK or more** must be approved before any money moves.
- It **fails closed**: only a clear "Approve" lets the transfer through. A rejection, a timeout, a 429/503 that does not clear, a connection error or an answer we do not understand all mean no transfer.
- The client uses the standard HTTP resilience handler: retries with `Retry-After` honoured, a 3 s limit per attempt and 8 s in total (configurable under `Screening` in `appsettings.json`).
- The idempotency key is sent as the screening `requestId`, so every retry gets the same decision and is not billed again. Without a key a random id is used.
- Screening is called **before** the database transaction opens, so no database locks are held during a slow call. A cheap check for unknown accounts and insufficient funds runs first so impossible transfers are never sent to (and billed by) the third party.
- A screening outage is not remembered under the key, so the client can retry once screening is back. A rejection is remembered.

### Audit trail

Every booked deposit, withdrawal and transfer leaves exactly one audit entry, written in the same database transaction as the booking. A transfer that is not booked leaves none. A unique index on the transaction id makes a second entry for the same booking impossible. Entries contain account numbers and amounts only, never owner names.

### Layout of the server

- `Controllers`: HTTP only; map results to status codes.
- `Services`: `LedgerService` (the rules), `AuditService`, and the screening client.
- `Models/Domain`: entities (`Account`, `Transaction`, `AuditEntry`, `IdempotencyRecord`, `Money`). `Models/Dtos`: what goes over HTTP. The service knows nothing about DTOs.
- `Data`: `AppDbContext`, migrations and the development seed.

## Logging

What belongs in a log, and what does not:

- **Never**: owner names or other personal data, request bodies, balances or amounts tied to a person, credentials.
- **Yes**: ids and outcomes that let an operator follow a request without identifying anyone. Today that is the screening request id, attempt number and HTTP status when screening misbehaves. ASP.NET request logging is kept at warning level, and EF Core logs SQL text but not parameter values (sensitive data logging is off).

## Assumptions and interpretations

The specification is incomplete on purpose. These are my readings:

1. **Account numbers** are trimmed and upper-cased as soon as a request is processed (`" nb-1001 "` becomes `NB-1001`), so they are compared, stored and returned in one canonical form regardless of how they were typed or of the database collation. A transfer to the same account in another spelling is refused.
2. **Currency** is always SEK and is not part of requests. Anything else is not representable.
3. **Amounts** are JSON numbers bound exactly to `decimal`. More than two decimals is refused, never rounded. The web page types amounts the Swedish way (`1 500,50`) and sends exact text, never a floating point number.
4. **The threshold** is inclusive: 10 000,00 SEK is screened, 9 999,99 is not.
5. **"If it cannot be reached, do not make the transfer"** covers every failure to get a clear approval, including slow answers, not just connection errors.
6. **Idempotency is for transfers**, as the specification says. Deposit and withdraw have no key: a retry is a new operation. The `Idempotency-Key` is optional; without it nothing is remembered.
7. **Which outcomes are remembered under a key**: success and definitive refusals (insufficient funds, unknown account, screening rejection) are replayed; "screening unavailable" is not, so a later retry can succeed.
8. **Concurrent retries** of the same key get 409 while the first request is still running, rather than waiting.
9. **Account types** (Savings, FixedTerm, Business) have no behavioural differences yet. The type is an enum with fixed numeric values so more can be added. FixedTerm would normally restrict withdrawals; that is out of scope.
10. **A transfer is one transaction record** with a source and a target, listed under both accounts. The audit trail is a separate record, one per transaction.
11. **Audit entries are stored in the main database**, written atomically with the booking. The separate store the specification describes is listed under "Not covered from the specification".
12. **Transactions list**: unknown account gives 404. **Audit trail**: unknown account gives an empty list.
13. **No authentication.** This is an internal back-office service; a real deployment needs it.
14. **Migrations and seed data run automatically only in Development.** In production, migrations are a deploy step.
15. **The fake screening service** rejects any screening involving NB-2001, approves everything else, takes 50 to 1500 ms, and answers 429 or 503 (with `Retry-After`) for about one call in five. The same `requestId` always gets the same decision. It does not model billing.
16. **The web page** lists accounts, makes transfers and shows an account's transactions. Deposit and withdraw are API-only.

## Not covered from the specification

- **A separate audit store.** The specification says transfers must keep working when the audit store is down, and asks for a test of that. Here the audit entry is written in the main database, in the same transaction as the booking, so there is no separate store that can be down and nothing that moves entries elsewhere. The rule "exactly one entry per booked operation, none for an unbooked one" holds and is tested; the audit-store-down behaviour and its test do not exist.
- **Running as two copies, demonstrated.** The service keeps all state in the database and nothing in memory, so two copies are designed to be safe. But the compose file starts one copy, and no test runs two instances against each other. Concurrency is tested with many parallel requests (each with its own database connection) against one database.
- **Billing in the screening fake.** The specification describes a fake that bills each call, bills a `requestId` only once, and bills every call without one. The fake keeps the "same `requestId`, same decision" rule but does not model billing, so "not billed twice" cannot be observed. The service sends the idempotency key as the `requestId` so that it would hold.
- **An outcome log.** Logging is limited to what is described under "Logging": the screening client writes warnings with ids and statuses, and nothing else is logged. There is no line per booked or refused transfer.

## Known limitations

- The transaction list is not paged.
- There are no tests at the HTTP layer (status codes, the `Idempotency-Key` header, JSON binding); the rules are tested through the service, and the endpoints were tried by hand.
- There is no health endpoint on the server, so compose cannot wait for it to be ready.
- The screening fake has no tests of its own.
- Development credentials are in `appsettings.json` and `docker-compose.yml`.

## How I used tools

I used an AI assistant (Claude Code) throughout: to propose the design, write code and tests, run them, and draft this README. I made the decisions that shaped the result: for example keeping the `Money` type, keeping audit entries in the main database, keeping the transfer as its own resource, simplifying the screening client to the standard resilience handler, and cutting the web page down to accounts and transfers. The tests were run against real SQL Server, not mocks, because the rules that matter (constraints, locking, concurrency) live in the database.

**What I would not send to a tool in a real bank:**

- Real customer data of any kind: names, personal identity numbers, account numbers, balances, transaction histories, and production database dumps or logs that contain them.
- Secrets: connection strings, passwords, API keys, certificates, tokens.
- Screening, fraud or AML rules, lists and thresholds, and anything about how the real third-party service decides.
- Details of internal architecture, network layout and unpatched vulnerabilities.
- Anything covered by confidentiality or regulatory duties, unless the tool is approved for that classification of data and runs where the bank allows it.

Used that way, a tool sees only fictional data and general code. Every line still has to be understood and reviewed by the person who hands it in.
