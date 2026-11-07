"""Record actual inspection of all forty-eight third-lot source views."""
from pathlib import Path
import re

recipe_path = Path(__file__).with_name('seal_pirate_upper_lod_review_v954.py')
code = recipe_path.read_text('utf-8')
for old, new in {
    'pirate-upper-lod-images-reviewed-v954': 'pirate-upper-lod-images-reviewed-v987',
    'pirate-upper-lod-images-review-v953': 'pirate-upper-lod-images-review-v986',
    "len(r['rows']) == 22 and len(r['refusals']) == 2": "len(r['rows']) == 24 and len(r['refusals']) == 0",
    "for row in r['rows']) == 44": "for row in r['rows']) == 48",
    'twenty_two_upper_lod_source_cards_agent_reviewed': 'twenty_four_upper_lod_source_cards_agent_reviewed',
    'cards_verified=22, images_verified=44': 'cards_verified=24, images_verified=48',
    "print('22 source cards / 44 views reviewed; 2 native refusals retained')":
        "print('24 source cards / 48 views reviewed; no native refusals')",
    "r['input_pins'][str(Path(__file__).resolve())]":
        "r['input_pins'][str(recipe_path)] = sha(recipe_path)\nr['input_pins'][str(Path(__file__).resolve())]",
}.items():
    assert old in code, old
    code = code.replace(old, new)
observation = ('All 48 views inspected across eight contact sheets. The lookout tower retains its roof, '
               'deck, legs and braces; rails and ropes simplify visibly across LOD3/4/5. The gibbet post '
               'retains its beam, brace and rope bands. Three cliff variants retain distinct rock contours '
               'and textured surfaces. Roof sections retain planks, side boards and their openings or raised '
               'sections. Shack fronts retain their door openings; balcony and shed variants retain roofs, '
               'legs, braces, walls and floor boards as declared by their source LODs. All objects are fully '
               'framed; no uniform image, pink shader failure or clipped silhouette was observed. Every '
               'requested mesh selection differs from its independent LOD0 control. This qualifies only '
               'source thumbnails; complete conversion, OpenRPG rendering, placement, runtime and GUI '
               'verification of these twenty-four entries remain unqualified.')
code, count = re.subn(r"visual_observations='[^']*'", 'visual_observations='+repr(observation), code)
assert count == 1
exec(compile(code, str(recipe_path)+'#actual-third-lot-visual-review', 'exec'), globals())
