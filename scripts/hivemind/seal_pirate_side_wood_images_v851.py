"""Seal the eight actually inspected side-panel views without render qualification."""
from pathlib import Path
import re

recipe_path = Path(__file__).with_name('seal_pirate_held_wood_images_v834.py')
recipe = recipe_path.read_text('utf-8')
recipe = recipe.replace('pirate-held-wood-images-seal-v834', 'pirate-side-wood-images-seal-v851')
recipe = recipe.replace('pirate-held-wood-images-review-v830', 'pirate-side-wood-images-review-v849')
observation = ('Eight actual views inspected across the two generated sheets. '
               'ShackSide6m03 has a rectangular window, vertical supports and a diagonal brace, '
               'with faded blue paint and visible horizontal planks on both sides. '
               'ShackSide8m03 is a longer closed plank wall with vertical supports and diagonal bracing '
               'on both sides. LOD1 and LOD2 remain fully framed and recognizable. '
               'No pink shader failure, uniform image or cut-off was observed. '
               'Native mesh identities distinguish these LODs from the LOD0 controls; '
               'all PNGs decoded with zero clipped pixels. Review is scoped to source thumbnails '
               'in the current SDK after the measured Wood01 default/stencil migration. '
               'Original SDK parity, Forge geometry/material conversion, placement, runtime '
               'and editor GUI remain unqualified; this is agent review, not human review.')
recipe, count = re.subn(r'(?m)^observation = .*$', lambda _: 'observation = '+repr(observation), recipe)
assert count == 1
exec(compile(recipe, str(recipe_path)+'#eight-inspected-side-panel-views', 'exec'), globals())
