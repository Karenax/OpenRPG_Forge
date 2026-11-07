"""Reopen exact native LOD identities and generate sheets for visual inspection."""
from pathlib import Path
parent = Path(__file__).with_name('reopen_pirate_lod_thumbnail_batch03_v370.py')
code = parent.read_text('utf-8').replace('pirate-lod-thumbnail-batch03-v368', 'pirate-lod-thumbnail-batch04-v491').replace('pirate-lod-thumbnail-batch03-review-v370', 'pirate-lod-thumbnail-batch04-review-v494').replace('pirate-lod-thumbnail-batch03-guard-v369', 'pirate-lod-thumbnail-batch04-guard-v493')
code = code.replace("for p in [Path(__file__),guardpath", "for p in [parent,Path(__file__),guardpath")
exec(compile(code, str(parent) + '#next-lod-native-reopen', 'exec'), globals())
