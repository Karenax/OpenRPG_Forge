"""Retain the existing material/source refusals for the next thumbnail lot."""
from pathlib import Path
parent = Path(__file__).with_name('audit_and_guard_pirate_lod_batch03_v369.py')
code = parent.read_text('utf-8').replace('pirate-lod-thumbnail-batch03-v368', 'pirate-lod-thumbnail-batch04-v491').replace('pirate-lod-thumbnail-batch03-guard-v369', 'pirate-lod-thumbnail-batch04-guard-v493')
needle = 'assert not O.exists()'
audit = "audit=read(CAP/'HELPER-OWNERSHIP.v1.json')\nassert audit['status']=='private_helpers_match_repository_or_retained_bench_sources' and not audit['asset_dll_found']\n"
audit += "for helper in audit['helpers']:\n    for key in ['private_path','controlled_source']:\n        assert sha(helper[key])==helper['sha256'],helper[key]\n        pins[helper[key]]=helper['sha256']\n"
audit += "pins[str(parent)]=sha(parent)\n" + needle
assert code.count(needle) == 1
code = code.replace(needle, audit)
exec(compile(code, str(parent) + '#next-lod-source-guard', 'exec'), globals())
