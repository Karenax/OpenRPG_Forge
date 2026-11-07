"""Review the ten guard-admitted variants while retaining all six material refusals."""
from pathlib import Path
parent=Path(__file__).with_name('reopen_pirate_lod_batch06_admitted_v517.py')
code=parent.read_text('utf-8').replace('pirate-lod-thumbnail-batch06-v513','pirate-lod-thumbnail-batch07-v565').replace('pirate-lod-thumbnail-batch06-admitted-review-v517','pirate-lod-thumbnail-batch07-admitted-review-v568').replace('pirate-lod-thumbnail-batch06-guard-v514','pirate-lod-thumbnail-batch07-guard-v566').replace('six_palm_variants_refused','six_material_variants_refused')
exec(compile(code,str(parent)+'#lot07-admitted-only','exec'),globals())
