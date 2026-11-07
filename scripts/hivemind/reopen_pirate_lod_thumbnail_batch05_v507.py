"""Reopen next LOD identities and views before their agent visual review."""
from pathlib import Path
parent = Path(__file__).with_name('reopen_pirate_lod_thumbnail_batch04_v494.py')
code = parent.read_text('utf-8').replace('pirate-lod-thumbnail-batch04-v491', 'pirate-lod-thumbnail-batch05-v502').replace('pirate-lod-thumbnail-batch04-review-v494', 'pirate-lod-thumbnail-batch05-review-v507').replace('pirate-lod-thumbnail-batch04-guard-v493', 'pirate-lod-thumbnail-batch05-guard-v506')
exec(compile(code, str(parent) + '#lod05-native-reopen', 'exec'), globals())
