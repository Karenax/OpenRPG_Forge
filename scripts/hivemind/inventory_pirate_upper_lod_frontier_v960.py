"""Refresh upper-LOD routing with explicit source hierarchy constraints."""
from pathlib import Path

parent = Path(__file__).with_name('inventory_pirate_upper_lod_frontier_v950.py')
code = parent.read_text('utf-8').replace('pirate-upper-lod-frontier-v950', 'pirate-upper-lod-frontier-v960')
anchor = "blocked = set(prior['blocked_material_guids'])"
assert code.count(anchor) == 1
code = code.replace(anchor, anchor+"\nhierarchy = read(W/'pirate-source-lod-hierarchy-v959/RESULT.v1.json')\n"
                    "assert hierarchy['catalog_revision'] == pointer['revision']\n"
                    "hierarchy_by_id = {c['id']:c for c in hierarchy['cards']}")
anchor = "    affected = materials & blocked"
assert code.count(anchor) == 1
code = code.replace(anchor, "    if any(not hierarchy_by_id[c['id']]['uniform_requested_lod_declared'] for c in selected):\n"
                    "        reasons.append('requested_lod_not_declared_in_all_source_groups')\n"+anchor)
code = code.replace("pins[str(Path(__file__).resolve())] = sha(Path(__file__))",
                    "pins[str(Path(__file__).resolve())] = sha(Path(__file__))\npins[str(parent)] = sha(parent)")
exec(compile(code, str(parent)+'#source-hierarchy-aware-routing', 'exec'), globals())
