# The Rule Lives in Three Places

Companion code samples for the Medium article (Part 5 of *Lessons from Building UpAllNight*).

**[Read the full article on Medium →](TODO-update-after-publish)**

## TL;DR

A tester surfaced a bug in the meld rule. The wrong rule was already in the
code, in the doc, and in the in-game rules modal — one bug, three places.
Fixing it was three commits. This repo has those three surfaces side by side:
same rule, three vocabularies, written for three different audiences (the
runtime, the next contributor, the player). When they disagree, the player
wins — they pick the most flattering one and conclude the other two are bugs.

## Files

| File | What it is |
|---|---|
| [`code-samples/01-meld-validate.cs`](code-samples/01-meld-validate.cs) | The relevant excerpt of `MeldService.ValidateMeld` — the rule in code form: rank match, wild-conversion guard, and the lay-off path that the old rule refused. The runtime's voice. |
| [`code-samples/02-gamerules.md`](code-samples/02-gamerules.md) | The "Rules" section of `gamerules.md` — the canonical written rule for the next contributor. Prose, not conditionals. |
| [`code-samples/03-rules-content.razor`](code-samples/03-rules-content.razor) | The "Books — Natural vs. Dirty" section of `RulesContent.razor` — the rules-modal HTML players read in-game. Strong tags, ten-second skim, the contract you made with the player. |

## Three surfaces, one rule

The article's central frame: a rule about gameplay necessarily lives in three
places, and the duplication is the point — not a smell.

- **The code (sample `01`).** What the game enforces. Validates moves in real
  time. Says yes or no with no commentary.
- **The doc (sample `02`).** What the next contributor learns. Describes the
  rule in prose so they don't have to reverse-engineer it from the
  conditionals.
- **The modal (sample `03`).** What the player reads. The same rule again,
  in HTML, written for someone who has thirty seconds before they tap "got
  it."

Each says the same rule in a different language for a different audience.
None of the three knows the other two exist. A rule change is a PR that
touches all three. If you've changed two of them, you've shipped a bug.

## License

MIT — copy, adapt, ship.
