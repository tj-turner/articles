# Figure kit

Shared visual language for the figures in this repo. `kit.css` holds the palette,
the depth treatment and seven figure archetypes; a figure is markup against it.

Open [`gallery.html`](gallery.html) in a browser to see all seven, drawn with real
content from the Articles 5–8 outlines.

## Why this exists

Readers said the series reads as a wall of text with one diagram in the middle.
Two separate causes, and this addresses the second one.

The first is cadence, and the pre-flight now gates on it — no stretch over 600
words without a break. The second is that **the only figure shape available was a
flowchart.** Mermaid draws nodes and arrows. That is the whole vocabulary. So an
argument about an interval, a threshold, a matrix or a comparison arrived as
nodes and arrows too, because that was the tool, and it read as filler because it
was answering a question the figure wasn't asking.

The other half of it is cost. Every figure that shipped was 200–425 lines of
bespoke CSS, which is a comfortable afternoon and exactly why each article has
one. Against the kit, the stat band in the gallery is fifteen lines of markup.

## Pick by the shape of the argument

| The claim is… | Archetype | Class |
|---|---|---|
| "here are the magnitudes" | **Stat band** — 3–4 numbers, full width | `.stats` / `.stat` |
| "this clears a limit and that doesn't" | **Budget bars** — one axis, threshold drawn through | `.bars` / `.bar-row` |
| "this is true for a bounded interval" | **Window strip** — time axis with a shaded window | `.strip` |
| "which of these catches which of those" | **Coverage matrix** — states per cell | `.matrix` / `.cell` |
| "the obvious design vs the shipped one" | **Split panel** — against / for | `.split` / `.panel` |
| "the argument is in these three lines" | **Annotated code** — callouts pinned to lines | `.code-card` |
| "things move through stages" | **Flow band** — nodes, rails, arrows | `.flow` / `.node` |

Only the last one is a flowchart. If the argument is genuinely a flow, use the
flow band rather than Mermaid, so the figure lands in the same visual key as
everything else in the piece.

Modifiers worth knowing: `.stat.hot|.warm|.ok` tint a number by verdict,
`.node.absent` draws a path deliberately *not* taken (dashed and grey — an
absence that isn't on the page reads as an omission), `.cell.yes|.no|.part|.na`
are the matrix states, and `body.narrow` gives a 980px sheet for a second,
smaller figure in the same article.

## Writing one

```html
<link rel="stylesheet" href="../../figure-kit/kit.css">
...
<div class="fig-title">What each path passed through</div>
<div class="fig-sub">One line saying what the reader is looking at.</div>
<div class="stats"> ... </div>
<div class="fig-note"><b>The finding</b>, in a sentence, under a hairline rule.</div>
```

Every figure gets a title, a subtitle and usually a `.fig-note`. The note is where
the finding goes, so a reader who scrolls past the prose still gets the point.

Literal identifiers wear a `.pill`. Verdict colors are fixed and mean the same
thing in every figure: green passed, amber partial, red failed or over budget.

## Rendering

Unchanged from before — headless Chrome at `deviceScaleFactor: 2`, then
ImageMagick to both formats:

```
cd <article>/support-files
node local/render-figure.js my-figure.html local/my-figure.png
magick local/my-figure.png -quality 92 -define webp:method=6 ../my-figure.webp
magick local/my-figure.png -strip -define png:compression-level=9 ../my-figure.png
```

**Both formats ship.** Medium rejects `.webp` on upload, so the `.png` is what
gets pasted into the post and the `.webp` is what the README and the draft embed.
Puppeteer installs into `support-files/local/`, which is gitignored, so the
dependency tree never ships.

The renderer screenshots `#canvas` if it finds one and `body` otherwise, so the
figure owns its own width and the viewport is only a bound.

## The rule that isn't negotiable

**Never hand a text-carrying figure to an image generator.** Asked to make the
Article 4 coverage figure "nicer", an image model garbled every label
(*"Nole. Ερarty"*) and redrew the two walls once per row with three separate
destination boxes — which asserts the opposite of what the figure meant. Image
models are for header art. Figures are authored, always.
