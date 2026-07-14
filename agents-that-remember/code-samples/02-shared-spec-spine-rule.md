<!-- Excerpt from .claude/agents/shared/kdp-spec.md §2.1–§2.2 — the shared spec
     all three agents Read before building a cover. The law of the memory loop:
     the rejection promoted into the shared spec, with the failure it guards
     against recorded inline. -->

### 2.1 Cover PDF — FULL WRAP (back + spine + front), page-count-dependent

**Spine width formula (white paper, B&W interior):**
```
spine_width = interior_page_count * 0.002252   # inches
```
(Cream paper uses 0.0025". Use white for this project.)

### 2.2 Regions of the wrap cover (left-to-right as printed)

- **Spine (spine_width wide):** the spine panel. Content depends on spine width:
  - **spine < 0.25":** leave blank (no text). Two reasons combine: (a) even 6pt type is ~0.083" tall and can drift into front/back panels when trimming; (b) KDP's automated cover review **rejects** spine text without ≥ 0.375" clearance from head and foot trim edges, and on thin spines the rejection risk is high — earth-day was rejected at 0.142" with the message *"please move all spine text at least 0.375 inches away from both the top and bottom edges of the cover"*. Record the decision in kdp-info.txt as `Spine text: blank (spine < 0.25")`.
  - **spine ≥ 0.25":** include the book title and author, rotated 90° clockwise (reading top-to-bottom when book is shelved). Title in bold serif, author in lighter weight. **Head/foot clearance (required by KDP automated review):** position the spine text centered at `wrap_h/2` and confirm the text's total rotated length is at most `wrap_h − 2×0.375" − 0.25"`. On 11.25" wraps this means total rotated text length ≤ 10.125".
