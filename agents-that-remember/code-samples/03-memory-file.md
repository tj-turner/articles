<!-- A memory file from the book generator's memory/ folder, reproduced verbatim
     (originSessionId trimmed). The diary of the memory loop: the durable note.
     Note the rigid shape — a rule, a *Why*, and a *How to apply*. The Why is
     what stops a future session from "optimizing away" a rule that looks
     inconvenient but exists for a hard external reason. -->

---
name: KDP rejects spine text without 0.375" head/foot clearance
description: spines under ~0.25" should be left blank — KDP automated cover review rejects spine text that doesn't have ≥ 0.375" margin from the top and bottom trim edges, even when math says it fits
type: feedback
---
KDP's automated cover review rejects spine text without ≥ 0.375" clearance from both the head and foot trim edges (the cover's top and bottom edges). On thin spines, leave the spine BLANK rather than risk a rejection that blocks publishing.

**Practical rule for this project:** spines under 0.25" → no spine text. Spines ≥ 0.25" → spine text is allowed, but verify total rotated text length ≤ `wrap_h − 2×0.375" − 0.125"` (10.125" on 11.25" wraps).

**Why:** Earth-day book (63 pp, 0.142" spine) was rejected at KDP upload with the message: *"Text on the spine is too close to the edges, which could lead to it being cut off during printing. To fix this, please move all spine text at least 0.375 inches (9.6 mm) away from both the top and bottom edges of the cover."* The text was rotated, centered at wrap_h/2, and well within margins mathematically — but KDP's checker still rejected it. Safest action is to drop spine text on narrow spines entirely.

**How to apply:** When generating a cover in any book agent (puzzle, coloring, journal), check the spine width during cover build. If `spine_w < 0.25"`, skip the rotated title/author text and emit only the solid background band. Record `Spine text: blank (spine < 0.25")` in `kdp-info.txt`. The kdp-spec.md §2.1 captures the project-wide rule.
