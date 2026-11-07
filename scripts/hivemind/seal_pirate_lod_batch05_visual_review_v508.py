"""Seal actual inspection of the six batch05 sheets, preserving prior evidence."""
from pathlib import Path
import re
parent = Path(__file__).with_name('seal_pirate_lod_batch03_visual_review_v371.py')
code = parent.read_text('utf-8').replace('pirate-lod-batch03-combined-review-v371', 'pirate-lod-batch05-combined-review-v508').replace('pirate-lod-thumbnail-batch03-review-v370', 'pirate-lod-thumbnail-batch05-review-v507')
observation = 'All 32 variant views inspected across six original-size sheets. Wooden flooring retains separate planks and uneven edges. The hanging cage retains its vertical bars and circular hoops. Hay bundles retain ties and fibrous silhouettes; the hay storage retains surrounding wooden rails. Two rowboat finishes retain hull planking, seating and interior supports. Short and tall ladders retain their rungs and rope ties. All objects are fully framed and recognizable in both views. Neutral subdued presentation, with no pink shader failure, uniform image or framing cut-off observed. Native LOD1 and LOD2 mesh identities were separately checked against LOD0 controls. These Unity presentation captures do not qualify Forge rendering, converted materials, runtime placement or the Studio GUI.'
code, count = re.subn(r"visual_observations='[^']*'", 'visual_observations=' + repr(observation), code)
assert count == 1
code = code.replace("r['input_pins'][str(p)]=sha(p);", "r['input_pins'][str(parent)]=sha(parent);r['input_pins'][str(p)]=sha(p);")
exec(compile(code, str(parent) + '#actual-batch05-inspection', 'exec'), globals())
