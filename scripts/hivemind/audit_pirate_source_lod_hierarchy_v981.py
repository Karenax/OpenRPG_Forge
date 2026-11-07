"""Reopen only the remaining sources against the current catalogue revision."""
from pathlib import Path

recipe_path = Path(__file__).with_name('audit_pirate_source_lod_hierarchy_v959.py')
code = recipe_path.read_text('utf-8')
changes = {
    'pirate-source-lod-hierarchy-v959': 'pirate-source-lod-hierarchy-v981',
    'assert len(missing) == 159': 'assert len(missing) == 135',
    'source_cards_examined=159': 'source_cards_examined=len(missing)',
    'pins[str(Path(__file__).resolve())] = sha(Path(__file__))':
        'pins[str(Path(__file__).resolve())] = sha(Path(__file__))\npins[str(recipe_path)] = sha(recipe_path)',
}
for old, new in changes.items():
    assert code.count(old) == 1, old
    code = code.replace(old, new)
exec(compile(code, str(recipe_path)+'#current-remaining-sources', 'exec'), globals())
