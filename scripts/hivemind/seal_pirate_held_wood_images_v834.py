"""Seal review of the eight actual source views, scoped to measured SDK migration."""
from pathlib import Path
import re
parent = Path(__file__).with_name('seal_pirate_lod_batch03_visual_review_v371.py')
code = parent.read_text('utf-8').replace('pirate-lod-batch03-combined-review-v371', 'pirate-held-wood-images-seal-v834').replace('pirate-lod-thumbnail-batch03-review-v370', 'pirate-held-wood-images-review-v830')
code = code.replace("len(r['rows'])==16", "len(r['rows'])==4").replace("for a in r['rows'])==32", "for a in r['rows'])==8").replace('cards_verified=16,images_verified=32', 'cards_verified=4,images_verified=8').replace('sixteen_actual_lod_variants_agent_reviewed', 'four_exact_wood_lod_variants_agent_reviewed')
observation = 'Eight views inspected across two sheets. ShackFront4m01 retains the open door, window, posts and faded blue plank paint on both sides. ShackFront4m02 retains its closed wall and diagonal brace on both sides. LOD1 and LOD2 are fully framed, textured and recognizable; no pink shader failure, uniform capture or cut-off observed. Native selected LOD mesh identities differ from LOD0, and decoded PNGs show no clipped pixels. Admission is limited to thumbnails in the current SDK under the measured stencil/default migration receipt; original SDK pipeline, Forge material/geometry conversion, placement, runtime and editor GUI remain unqualified.'
code, count = re.subn(r"visual_observations='[^']*'", 'visual_observations='+repr(observation), code)
assert count == 1
code = code.replace("r['input_pins'][str(p)]=sha(p);", "r['input_pins'][str(parent)]=sha(parent);r['input_pins'][str(p)]=sha(p);")
code = code.replace('Sixteen actual LOD variants and thirty-two images reviewed', 'Four exact wood LOD variants and eight actual views reviewed')
exec(compile(code, str(parent)+'#eight-actually-inspected-source-views', 'exec'), globals())
