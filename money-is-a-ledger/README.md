# Money Is a Ledger, Not a Balance

Companion code samples for the Medium article (Part 3 of *Lessons from Building UpAllNight*).

**[Read the full article on Medium →](https://levelup.gitconnected.com/money-is-a-ledger-not-a-balance-a5e4eb221d90)**

## TL;DR

The in-game economy was almost a single `ChipBalance` column you increment
and decrement. It isn't, because that's the column that loses player trust
the first time someone asks where their chips went.

What shipped instead: every economic event writes an append-only ledger row
carrying the delta, a reason code, and `BalanceAfter` — the running balance
frozen at that step. The wallet column is a cache of the ledger's tail, not
the source of truth. Concurrent writers serialize on a `UPDLOCK, ROWLOCK` on
the wallet row. Lifetime counters fall out of the same `UPDATE` via a `CASE`
on the reason code.

And the three real-money storefronts on the other side — Stripe, Apple IAP,
Google Play Billing — go through a single `IPaymentProvider` interface even
though they disagree on what *"purchase complete"* even means (Stripe: a
server-side webhook; Apple/Google: a client-submitted receipt). Idempotency
on `(Provider, ProviderTransactionId)` is what keeps re-delivered webhooks
from minting money — the same lesson as Part 1's idempotent seed scripts,
with the stakes turned up.

## Files

| File | What it is |
|---|---|
| [`code-samples/01-wallet-ledger.sql`](code-samples/01-wallet-ledger.sql) | The wallet adjust SP. Lock + ledger row + balance cache + lifetime rollups, all in one transaction. |
| [`code-samples/02-bad-balance-column.sql`](code-samples/02-bad-balance-column.sql) | The version we *didn't* ship, annotated with what's wrong with it. Side-by-side counter-example to `01`. |
| [`code-samples/03-ipaymentprovider.cs`](code-samples/03-ipaymentprovider.cs) | The single payment-provider interface + resolver. Admits both webhook and receipt completion shapes. |
| [`code-samples/04-idempotent-grant.sql`](code-samples/04-idempotent-grant.sql) | The grant SP and the unique index that lets it run twice without granting twice. |
| [`code-samples/05-begin-purchase.cs`](code-samples/05-begin-purchase.cs) | The Stripe provider's `BeginPurchaseAsync` — pending row written **before** the Checkout URL is handed to the client. |

## The two lessons to take away

**Treat money as an append-only ledger from the first credit, not a column
you increment.** Snapshot `BalanceAfter` on every row, lock the wallet row,
update lifetime counters in the same `UPDATE`. Compare `01-wallet-ledger.sql`
against `02-bad-balance-column.sql` for the contrast.

**Make every grant idempotent on `(Provider, ProviderTransactionId)`.**
Webhooks re-deliver and stores re-notify; a double-delivered event must
settle the existing row, never mint a second grant. `04-idempotent-grant.sql`
shows the status-code dance the SP uses to no-op on re-delivery, and
`05-begin-purchase.cs` shows the small but load-bearing ordering — pending
row first, Checkout URL second — that gives the grant something to settle
against.

## License

MIT — copy, adapt, ship.
