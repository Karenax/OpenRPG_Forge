"""Seal agent inspection of all six sheets and 32 real captured variant views."""
from pathlib import Path
import re
parent = Path(__file__).with_name('seal_pirate_lod_batch03_visual_review_v371.py')
code = parent.read_text('utf-8').replace('pirate-lod-batch03-combined-review-v371', 'pirate-lod-batch04-combined-review-v497').replace('pirate-lod-thumbnail-batch03-review-v370', 'pirate-lod-thumbnail-batch04-review-v494')
observation = 'All 32 variant views inspected across six original-size sheets. Two wicker fence lengths retain posts and crossing rails; simplified source LOD2 geometry remains identifiable. Six floor variants retain plank grain, uneven/broken edges, declared gaps and openings, and underfloor supports where present. Both views show the complete object within the frame. Neutral presentation is subdued but surfaces and geometry remain legible; no pink shader failure, uniform image or framing cut-off observed. Each LOD1/2 mesh identity is distinct from its LOD0 control. These Unity presentation captures do not qualify converted materials, automatic LOD, Forge rendering or the Studio GUI.'
code, count = re.subn(r"visual_observations='[^']*'", 'visual_observations=' + repr(observation), code)
assert count == 1
code = code.replace("r['input_pins'][str(p)]=sha(p);", "r['input_pins'][str(parent)]=sha(parent);r['input_pins'][str(p)]=sha(p);")
exec(compile(code, str(parent) + '#actual-32-view-review', 'exec'), globals())
