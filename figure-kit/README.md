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

## Headers

**Headers are authored now too.** [`header-template.html`](header-template.html)
is the house style as of 2026-08-28: 2:1, dark ground, no image model.

The generated headers did have a motif — dark navy, blue grid floor, chrome, an
amber flare, no text. The trouble is that it is the same motif as every other AI
article on Medium, and at feed-thumbnail size one glowing blob is every glowing
blob. The template puts real typography there instead, keeps a constant series
mark, and drops a **miniature of the article's own key figure** into the
right-hand card — so each header is unique, about the piece it sits on, and free,
because the figure already exists.

The ground is dark because all three candidates were compared at **330px**, which
is what Medium shows in a feed card. Dark keeps the presence the published
articles already have against Medium's white page. A light-ground variant was
built and rejected — on a white page it read as no image at all. Don't
reintroduce one without redoing that comparison.

Four things never change, and they are the part that does the recognizing: the
kicker pill, the monogram, the title treatment, and the four-color bar across the
bottom. Everything else varies per article.

No part number in the kicker. The cross-linking decision says every article reads
standalone, and a "05" on the header breaks that before the first word.

Change per article: `.hdr-title`, `.hdr-sub`, and the figure inside `.hdr-card`.
Leave the rest alone.

## Photographs

Photographs are allowed as of 2026-08-28, with a test: **write the caption
first.** If it says something the prose doesn't, the photo earned its place. If
it restates the heading, it's decoration — cut it and quote six lines from
`code-samples/` instead. Subject is the work (hands mid-edit, a whiteboard
mid-argument, real code on a screen), never a team posed around a laptop. Full
rules in `.claude/article-style.md` sec 13.

A stock photo arrives with a stranger's color grading, which is how the article
ends up in two visual languages — the same mistake the generated headers made.
Duotone it onto the series navy:

```powershell
& $magick photo.jpg -resize 1600x -gravity center -crop 1600x600+0+0 +repage `
  -colorspace Gray -auto-level `
  "(" -size 1x256 "gradient:#0b1f34-#f2f7fb" ")" -clut `
  -fill "#c08a1e" -colorize 6% -quality 86 photo-banner.jpg
```

`.jpg`, not `.webp` — Medium rejects `.webp` on upload, and photographs compress
better as JPEG than PNG. The crop is a banner: wide and short breaks the page, a
square eats a screen and reads as padding.

**Duotone is for texture, not for people.** Hands, screens, whiteboards, cables,
paper — yes. A photo whose whole point is a human face — no, it flattens skin
tones into something cold and corporate, which throws away the reason the photo
was worth including. Keep those in color and let the crop do the work.

**Processing costs the automatic credit.** An unmodified photo pulled through
Medium's built-in Unsplash search attributes itself. Crop or tone it and you are
uploading by hand, so write the attribution into the caption by hand too.

## The rule that isn't negotiable

**Never hand a text-carrying figure to an image generator.** Asked to make the
Article 4 coverage figure "nicer", an image model garbled every label
(*"Nole. Ερarty"*) and redrew the two walls once per row with three separate
destination boxes — which asserts the opposite of what the figure meant. Image
models are for header art. Figures are authored, always.
