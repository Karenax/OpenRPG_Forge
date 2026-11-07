"""Reopen native LOD identities and prepare batch06 sheets for actual inspection."""
from pathlib import Path
parent = Path(__file__).with_name('reopen_pirate_lod_thumbnail_batch04_v494.py')
code = parent.read_text('utf-8').replace('pirate-lod-thumbnail-batch04-v491', 'pirate-lod-thumbnail-batch06-v513').replace('pirate-lod-thumbnail-batch04-review-v494', 'pirate-lod-thumbnail-batch06-review-v515').replace('pirate-lod-thumbnail-batch04-guard-v493', 'pirate-lod-thumbnail-batch06-guard-v514')
exec(compile(code, str(parent)+'#lod06-native-reopen', 'exec'), globals())
