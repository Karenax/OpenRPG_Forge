"""Refresh the hierarchy proof after the third image lot, preserving refusals."""
from pathlib import Path

recipe_path = Path(__file__).with_name('audit_pirate_source_lod_hierarchy_v959.py')
code = recipe_path.read_text('utf-8')
for old, new in {
    'pirate-source-lod-hierarchy-v959': 'pirate-source-lod-hierarchy-v994',
    'assert len(missing) == 159': 'assert len(missing) == 111',
    'source_cards_examined=159': 'source_cards_examined=len(missing)',
    'pins[str(Path(__file__).resolve())] = sha(Path(__file__))':
        'pins[str(Path(__file__).resolve())] = sha(Path(__file__))\npins[str(recipe_path)] = sha(recipe_path)',
}.items():
    assert code.count(old) == 1, old
    code = code.replace(old, new)
exec(compile(code, str(recipe_path)+'#fourth-lot-source-hierarchy', 'exec'), globals())
