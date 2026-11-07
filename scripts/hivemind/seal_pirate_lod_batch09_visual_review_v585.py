"""Seal actual inspection of all thirty-two lot09 views."""
from pathlib import Path
import re
parent=Path(__file__).with_name('seal_pirate_lod_batch03_visual_review_v371.py')
code=parent.read_text('utf-8').replace('pirate-lod-batch03-combined-review-v371','pirate-lod-batch09-combined-review-v585').replace('pirate-lod-thumbnail-batch03-review-v370','pirate-lod-thumbnail-batch09-review-v584')
observation='All 32 views inspected across six sheets. Two ship wheels retain wooden rims, spokes, handles and rope wraps in their source orientation. Stair planks retain slats and long side beams. Stocks retain the three openings, frame, legs, braces and side chain. The table retains its plank top and four legs. The tarp retains draped fabric and hanging sides. Both shutters retain vertical planks, metal strips, fasteners and diagonal braces with distinct front and back. All views are fully framed and recognizable, without pink shader failure, uniform image or cut-off. Every LOD1/2 mesh identity differs from LOD0. Source captures do not qualify converted geometry, materials, placement, runtime or actual Studio GUI.'
code,count=re.subn(r"visual_observations='[^']*'",'visual_observations='+repr(observation),code)
assert count==1
code=code.replace("r['input_pins'][str(p)]=sha(p);","r['input_pins'][str(parent)]=sha(parent);r['input_pins'][str(p)]=sha(p);")
exec(compile(code,str(parent)+'#actual-lot09-visual-review','exec'),globals())
