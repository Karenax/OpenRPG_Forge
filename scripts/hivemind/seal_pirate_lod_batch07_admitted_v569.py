"""Seal the actual four-sheet review of ten rock/roof LOD variants."""
from pathlib import Path
parent=Path(__file__).with_name('seal_pirate_lod_batch06_admitted_v519.py')
code=parent.read_text('utf-8').replace('pirate-lod-batch06-admitted-review-v519','pirate-lod-batch07-admitted-review-v569').replace('pirate-lod-thumbnail-batch06-admitted-review-v517','pirate-lod-thumbnail-batch07-admitted-review-v568').replace('six_palm_refusals_retained','six_material_refusals_retained')
start=code.index("visual_observations='")+len("visual_observations='")
end=code.index("')\nO.mkdir()",start)
observation='All 20 admitted views inspected across four sheets in this turn. RockCliff04 retains distinct vertical and rounded rock silhouettes and textured surfaces. Roof4x6, Roof6x6, Roof6x8 and Roof8x8 retain planks, sloped roof outlines and gable framing; Roof6x6 retains its posts and dormer. Both views are framed and recognizable, with no pink shader failure, uniform image or cut-off observed. LOD1/2 identities differ from LOD0 controls. Six RockStoneFoliage/ShackFront variants remain refused for dedicated source material migration qualification. These Unity presentation captures do not qualify Forge rendering, runtime placement or Studio GUI.'
code=code[:start]+observation+code[end:]
code=code.replace("r['input_pins'][str(Path(__file__))]", "r['input_pins'][str(parent)]=sha(parent)\nr['input_pins'][str(Path(__file__))]")
exec(compile(code,str(parent)+'#actual-rock-and-roof-sheet-review','exec'),globals())
