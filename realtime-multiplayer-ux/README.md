# Real-Time Multiplayer Is a UX Problem, Not a Transport Problem

Companion code samples for the Medium article (Part 2 of *Lessons from Building UpAllNight*).

**[Read the full article on Medium →](TODO-update-after-publish)**

## TL;DR

I expected the hard part of real-time multiplayer to be the wire. It wasn't —
SignalR was four group methods and one rule (the database is the source of
truth; the hub just pushes "something changed, refetch"). Every problem that
actually cost me time lived a layer up, in the gap between *the server knows the
right thing* and *the player can see it, reach it, and act on it*: a live game
nobody could rejoin, a book whose progress was unreadable, a rule violation
players never noticed. This repo has the artifacts behind those lessons.

## Files

| File | What it is |
|---|---|
| [`code-samples/01-gamehub-groups.cs`](code-samples/01-gamehub-groups.cs) | The entire real-time API: four group methods plus a personal per-user group. The "transport was the easy part" exhibit. |
| [`code-samples/02-resumable-games.sql`](code-samples/02-resumable-games.sql) | The "door back in" — the query that finds the games a reconnecting player is still seated at. |
| [`code-samples/03-stale-sweep.cs`](code-samples/03-stale-sweep.cs) | The reaper for the *other* orphan: Waiting lobbies nobody joined. Scoped to never touch InProgress games. |
| [`code-samples/04-error-modal-vs-toast.razor`](code-samples/04-error-modal-vs-toast.razor) | Escalating errors from a missed toast to a centered modal — one conditional, not a new component. |
| [`code-samples/05-progress-pips.razor`](code-samples/05-progress-pips.razor) | A 7-pip progress row that turned "5 / 7" into something newcomers could read at a glance. |

## Two orphans, opposite fixes

The bug at the heart of the article: there are two kinds of orphaned game in a
multiplayer app, and they need opposite treatment.

- **"Nobody joined."** A `Waiting` lobby that never filled. *Reap it* — that's
  `03-stale-sweep.cs`.
- **"Tab closed mid-game."** A live `InProgress` game the player lost their
  connection to. *Give them a way back in* — that's `02-resumable-games.sql`.

Reaping the second kind would delete a live table out from under a player who
just blinked. I'd built the reaper and assumed orphans were handled — the
resume case was a whole separate feature I hadn't written yet.

## License

MIT — copy, adapt, ship.
