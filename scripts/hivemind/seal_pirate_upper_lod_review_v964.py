"""Record actual inspection of all eight sheets of the second upper-LOD lot."""
from pathlib import Path
import re

parent = Path(__file__).with_name('seal_pirate_upper_lod_review_v954.py')
code = parent.read_text('utf-8')
for before, after in {
    'pirate-upper-lod-images-reviewed-v954': 'pirate-upper-lod-images-reviewed-v964',
    'pirate-upper-lod-images-review-v953': 'pirate-upper-lod-images-review-v963',
    "len(r['rows']) == 22 and len(r['refusals']) == 2": "len(r['rows']) == 24 and len(r['refusals']) == 0",
    "for row in r['rows']) == 44": "for row in r['rows']) == 48",
    'twenty_two_upper_lod_source_cards_agent_reviewed': 'twenty_four_upper_lod_source_cards_agent_reviewed',
    'cards_verified=22, images_verified=44': 'cards_verified=24, images_verified=48',
    "print('22 source cards / 44 views reviewed; 2 native refusals retained')": "print('24 source cards / 48 views reviewed; no native refusals')",
    "r['input_pins'][str(Path(__file__).resolve())]": "r['input_pins'][str(parent)] = sha(parent)\nr['input_pins'][str(Path(__file__).resolve())]",
}.items():
    assert before in code, before
    code = code.replace(before, after)
observation = ('All 48 views inspected across eight sheets. Three wicker fences retain posts and sparse rails. '
               'The seven floor variants retain plank surfaces, gaps, supports and stair openings; source LOD simplification is visible. '
               'Hay pile and storage retain dense straw textures and the storage frame. Two small boats retain hulls, seats and planks with distinct paint colors. '
               'Short and tall ladders retain rails and rungs; rope details reduce at upper source LODs. '
               'All objects are completely framed with no uniform image, clipped silhouette or pink shader failure. '
               'Every requested LOD3/4/5 has independent mesh identities from its LOD0 control. '
               'This review qualifies source thumbnails only; conversion, OpenRPG rendering, placement, runtime and actual Studio GUI remain unverified.')
code, count = re.subn(r"visual_observations='[^']*'", 'visual_observations='+repr(observation), code)
assert count == 1
exec(compile(code, str(parent)+'#actual-second-lot-inspection', 'exec'), globals())
