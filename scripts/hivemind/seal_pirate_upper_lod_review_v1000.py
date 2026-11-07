"""Seal actual inspection of seven contact sheets and 38 source views."""
from pathlib import Path
import re

recipe_path = Path(__file__).with_name('seal_pirate_upper_lod_review_v954.py')
code = recipe_path.read_text('utf-8')
for old, new in {
    'pirate-upper-lod-images-reviewed-v954': 'pirate-upper-lod-images-reviewed-v1000',
    'pirate-upper-lod-images-review-v953': 'pirate-upper-lod-images-review-v999',
    "len(r['rows']) == 22 and len(r['refusals']) == 2": "len(r['rows']) == 19 and len(r['refusals']) == 0",
    "len(r['contact_sheets']) == 8": "len(r['contact_sheets']) == 7",
    "for row in r['rows']) == 44": "for row in r['rows']) == 38",
    'twenty_two_upper_lod_source_cards_agent_reviewed': 'nineteen_upper_lod_source_cards_agent_reviewed',
    'cards_verified=22, images_verified=44': 'cards_verified=19, images_verified=38',
    "print('22 source cards / 44 views reviewed; 2 native refusals retained')":
        "print('19 source cards / 38 views reviewed; no native refusals')",
    "r['input_pins'][str(Path(__file__).resolve())]":
        "r['input_pins'][str(recipe_path)] = sha(recipe_path)\nr['input_pins'][str(Path(__file__).resolve())]",
}.items():
    assert old in code, old
    code = code.replace(old, new)
observations = ('All 38 views inspected across seven contact sheets. Three shed variants retain distinct '
                'walls, roof profiles, openings, braces and posts. Coarser LOD5 roofs and floors have visible '
                'gaps corresponding to the selected source geometry. Walkway end and platform retain boards '
                'and rails. Two ship wheels retain different spokes, handles and rope bindings. Stairs retain '
                'individual steps and supporting beams; stocks retain three holes, braces and feet. The tarp '
                'retains its irregular sheet and hanging sides. No uniform image, pink shader failure or '
                'clipped silhouette was observed. All selected mesh identities differ from independent LOD0 '
                'controls. This is agent review of source thumbnails only; conversion, OpenRPG rendering, '
                'placement, runtime, human review and actual Studio GUI remain unqualified.')
code, count = re.subn(r"visual_observations='[^']*'", 'visual_observations='+repr(observations), code)
assert count == 1
exec(compile(code, str(recipe_path)+'#actual-fourth-lot-visual-review', 'exec'), globals())
