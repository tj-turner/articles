# Claude Code project memory — two real memories that outlived sessions

Both files below are real, copied from the UpAllNight project's memory
directory (`~/.claude/projects/<project>/memory/`). They have been loading
into every session in this project since the day they were written.

The pattern: every correction goes into memory *before* it goes back into
the code. The next session opens with the rule already known.

---

## 1. A feedback memory — a correction that became a rule

The first one captures the seed-idempotency rule from Part 1 of this
series. It was written after a duplicate-key failure at the post-deploy
stage taught me to never trust an unguarded `INSERT` in a seed file again.

```markdown
---
name: Seed scripts must be idempotent
description: Every SQL seed file in src/UpAllNight.Database/SeedData/
  must guard inserts so the post-deploy pipeline can re-run safely.
type: feedback
---
Every file under `src/UpAllNight.Database/SeedData/` MUST be idempotent —
curated defaults (`SeedGallery.sql`, `SeedCards.sql`) as well as
generated batch files. No exceptions.

- Per-row seeds: `IF NOT EXISTS (SELECT 1 FROM [<Table>]
  WHERE [<PrimaryKey>] = '<value>') INSERT …`.
- All-or-nothing reference tables (e.g. `Cards`): wrap the whole insert
  block in `IF NOT EXISTS (SELECT 1 FROM [<Table>]) BEGIN … END GO`.
- When authoring or reviewing a new seed file, re-read it and verify
  every `INSERT` is inside a guard before considering the task complete.

**Why:** The DACPAC post-deploy script runs on every deployment.
Unguarded inserts either duplicate rows or fail on unique constraints.
This bit us when re-deploying caused duplicate cards and avatars.

**How to apply:** Any time a seed file is created, edited, or reviewed
in this repo, confirm idempotency.
```

The `Why:` and `How to apply:` blocks are load-bearing. Without them, a
future session might apply the rule too narrowly ("oh, only for the seed
files I author this session") or too broadly ("guard every INSERT in the
project"). The reason and the scope are what make the rule survive.

---

## 2. A project memory — an *intentional* deferred decision

The second one is a different shape. It exists because I caught myself
arguing with myself, three sessions in a row, about whether to refactor
the book-strip layout for symmetry. Each time the answer was the same:
not now, here's why. The memory pins the decision so the conversation
doesn't restart.

```markdown
---
name: Book-strip symmetry follow-up option
description: Optional refactor to restore mirror symmetry of the
  team-book strips by making book-pile size container-relative.
type: project
---
The opposing-team and my-team book strips are intentionally asymmetric:
opp at `top:29%` (must clear draw/discard at `top:47.5%`) and mine at
`top:67%` (must clear raised hand cards along the bottom). They cannot
both be mirror-symmetric AND clear of their respective hazards because
the rendered slot height becomes a much larger % of table height at
small viewports.

**Available follow-up if symmetry is wanted later:** make `.book-pile`
size container-relative (e.g. `width: 4.83cqi; height: 6.83cqi` with
min-width / min-height floors). With a stable slot height the strips
can sit at mirror positions without overlap risk.

**How to apply:** only revisit if the user asks to "make books
symmetric" or "fix the book strip layout". Don't proactively refactor.
```

Notice the trigger — *don't proactively refactor.* The memory exists
specifically to prevent the decision from being re-litigated. It is the
opposite of a TODO. A TODO says "this is worth doing eventually." This
memory says "this is worth *not* doing, until a specific signal, here's
why."

---

## The MEMORY.md index

Both files are pointed at from `MEMORY.md`, which is the top-level index
that loads in every session:

```markdown
- [Seed scripts must be idempotent](feedback_seed_idempotency.md) —
  guard inserts with IF NOT EXISTS; learned from a duplicate-key
  failure on the second post-deploy run
- [Book-strip symmetry follow-up](project_book_strip_symmetry_option.md) —
  intentionally deferred refactor for container-relative book-pile sizing
```

One line per memory, with the hook that tells future-me whether this
memory is relevant to the current task. Most sessions don't need either
file's body — the index line is enough. The file is there for when the
session does the work the memory governs.
