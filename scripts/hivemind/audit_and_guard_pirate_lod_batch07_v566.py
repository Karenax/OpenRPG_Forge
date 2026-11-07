"""Apply unchanged source/material refusals after the next image capture completes."""
from pathlib import Path
recipe=Path(__file__).with_name('audit_and_guard_pirate_lod_batch06_v514.py')
code=recipe.read_text('utf-8').replace('pirate-lod-thumbnail-batch06-v513','pirate-lod-thumbnail-batch07-v565').replace('pirate-lod-thumbnail-batch06-guard-v514','pirate-lod-thumbnail-batch07-guard-v566')
code=code.replace('pirate-lod-thumbnail-batch04-v491/HELPER-OWNERSHIP.v1.json','pirate-palm-wind-native-reference-v558/HELPER-OWNERSHIP.v1.json')
exec(compile(code,str(recipe)+'#lot07-source-material-guard','exec'),globals())
