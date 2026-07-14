# Excerpt from a generated book's build.py (books/puzzle/earth-day-.../source/code/build.py).
# The enforcement of the memory loop: the rule as executing code, with the *reason*
# preserved in a comment so the next reader of the generated script understands
# why the spine is intentionally empty. This is the book that got rejected;
# this build.py is the corrected version the rule produced.

def write_cover(interior_page_count):
    spine_w = interior_page_count * 0.002252 * inch
    wrap_w  = 2 * BLEED + 2 * TRIM_W + spine_w
    wrap_h  = 2 * BLEED + TRIM_H

    c = canvas.Canvas(output_path, pagesize=(wrap_w, wrap_h))

    back_x0  = 0.0
    back_x1  = BLEED + TRIM_W
    spine_x0 = back_x1
    spine_x1 = spine_x0 + spine_w
    front_x0 = spine_x1

    # ... front-cover raster, back-cover copy, title bands drawn here ...

    # ---- SPINE: solid green band, no text -----------------------------------
    # Spine is ~0.142" wide; KDP requires 0.375" clearance from head/foot
    # edges, which leaves no usable run for legible text on a thin spine.
    # Leave the spine as a solid color band (matches the front-cover palette).
    c.setFillColor(deep_green)
    c.rect(spine_x0, 0, spine_w, wrap_h, fill=1, stroke=0)
