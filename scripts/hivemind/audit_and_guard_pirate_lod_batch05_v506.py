"""Reopen the next native image epoch using the unchanged source/material guards."""
from pathlib import Path
parent = Path(__file__).with_name('audit_and_guard_pirate_lod_batch04_v493.py')
code = parent.read_text('utf-8').replace('pirate-lod-thumbnail-batch04-v491', 'pirate-lod-thumbnail-batch05-v502').replace('pirate-lod-thumbnail-batch04-guard-v493', 'pirate-lod-thumbnail-batch05-guard-v506')
exec(compile(code, str(parent) + '#lod05-source-guard', 'exec'), globals())
