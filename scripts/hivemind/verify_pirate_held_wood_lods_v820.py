"""Reopen all six exact LOD comparisons; retain refusal on any changed input."""
from pathlib import Path
parent = Path(__file__).with_name('verify_pirate_wood_lod_migration_v592.py')
code = parent.read_text('utf-8').replace('pirate-wood-lod-migration-v591', 'pirate-wood-lod-migration-v819').replace('pirate-wood-lod-migration-review-v592', 'pirate-held-wood-lod-review-v820')
replacements = {
    "len(prep['cases'])==144": "len(prep['cases'])==24",
    "len(run['comparisons'])==72": "len(run['comparisons'])==12",
    "range(36)": "range(6)",
    "range(0,36,3)": "range(0,6,3)",
    "groups_verified=12,lod_cases_verified=36,view_comparisons_verified=72": "groups_verified=2,lod_cases_verified=6,view_comparisons_verified=12",
    "twelve_wood_groups_three_exact_lods_native_stencil_comparisons_verified": "two_retained_refusal_wood_groups_three_exact_lods_native_stencil_comparisons_verified",
    "Path(__file__).with_name('run_pirate_wood_lod_migration_v591.py')": "Path(__file__).with_name('run_pirate_wood_held_lods_v819.py'),parent",
}
for before, after in replacements.items():
    assert before in code, before
    code = code.replace(before, after)
exec(compile(code, str(parent)+'#six-refused-lod-comparisons', 'exec'), globals())
