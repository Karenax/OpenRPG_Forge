"""Seal actual inspection of all thirty-two lot08 views."""
from pathlib import Path
import re
parent=Path(__file__).with_name('seal_pirate_lod_batch03_visual_review_v371.py')
code=parent.read_text('utf-8').replace('pirate-lod-batch03-combined-review-v371','pirate-lod-batch08-combined-review-v577').replace('pirate-lod-thumbnail-batch03-review-v370','pirate-lod-thumbnail-batch08-review-v576')
observation='All 32 views inspected across six sheets. ShackFront6m retains the doorway, plank sides and diagonal braces. The balcony retains its roof, posts, platform, railings and braces. The separate shed floor retains uneven planks. Three shed variants retain open fronts, plank walls, roofs and interior framing. WalkwayEnd retains its two upright posts and rails; Walkway retains the platform and staggered loose rails in both source views. All views are fully framed and recognizable, with no pink shader failure, uniform image or cut-off. Every LOD1/2 mesh identity differs from its LOD0 control. Source captures do not qualify converted geometry/materials, automatic LOD, runtime placement or actual Studio GUI.'
code,count=re.subn(r"visual_observations='[^']*'",'visual_observations='+repr(observation),code)
assert count==1
code=code.replace("r['input_pins'][str(p)]=sha(p);","r['input_pins'][str(parent)]=sha(parent);r['input_pins'][str(p)]=sha(p);")
exec(compile(code,str(parent)+'#actual-lot08-visual-review','exec'),globals())
