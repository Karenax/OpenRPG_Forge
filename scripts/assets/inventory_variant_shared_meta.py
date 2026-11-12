"""Identify byte-identical meta files for remaining HDRP/URP card pairs."""
import argparse
import collections
import csv
import json
from pathlib import Path

import vault_storage as storage
from stage_source_support_relinks import digest_file, norm


def inventory(vault, pairs_file, output):
    vault = vault.resolve(strict=True)
    inventory_raw = pairs_file.read_bytes(); data = json.loads(inventory_raw)
    pointer = storage.confined(vault,vault/'metadata/asset-source-support-registry.v1.json')
    raw = pointer.read_bytes()
    if storage.sha(raw) != data['source_registry_sha256']: raise ValueError('stale inventory')
    pin = json.loads(raw)
    catalog = storage.confined(vault,vault/pin['path']).read_bytes()
    if storage.sha(catalog) != pin['sha256']: raise ValueError('catalog SHA mismatch')
    cards = {c['id']:c for c in json.loads(catalog)['assets']}
    mappings = collections.defaultdict(set); pins = {}; groups = collections.Counter()
    for pair in data['pairs']:
        if not pair['declared_meta_sha_equal']: continue
        old, retained = (cards[pair[k]] for k in ('hdrp_id','urp_id'))
        a, b, sha = old.get('source_meta_file'),retained.get('source_meta_file'),old.get('source_meta_sha256')
        if not isinstance(a,str) or not isinstance(b,str) or norm(a)==norm(b): continue
        if not (norm(a).startswith('originals/unity-purchased/hivemind/') and norm(b).startswith('originals/unity-purchased/hivemind/')): continue
        if ('/source/' in norm(a) and '/assets/' in '/'+norm(a).split('/source/',1)[1]) or not storage.valid_hex(sha,64): continue
        key = norm(a); mappings[key].add(b); pins[key] = (a,sha)
        groups[pair['base_id'].split('.')[1]] += 1
    rows = []; cache = {}; failures = []
    for key, retained_set in sorted(mappings.items()):
        old, sha = pins[key]
        if len(retained_set)!=1:
            failures.append({'old':old,'reason':'ambiguous retained path'}); continue
        kept = next(iter(retained_set))
        if norm(kept) in mappings:
            failures.append({'old':old,'reason':'overlapping candidate'}); continue
        try:
            for rel in (old, kept):
                p = storage.confined(vault,vault/rel)
                if p not in cache: cache[p] = digest_file(p)
                if cache[p] != sha: raise ValueError('file SHA differs')
            rows.append({'candidate':old,'retained_hdrp':json.dumps([kept]),'sha256':sha})
        except (OSError,ValueError) as exc:
            failures.append({'old':old,'reason':str(exc)})
    if pointer.read_bytes()!=raw or pairs_file.read_bytes()!=inventory_raw: raise ValueError('inputs changed')
    with output.open('x',encoding='utf8',newline='') as stream:
        writer=csv.DictWriter(stream,fieldnames=('candidate','retained_hdrp','sha256'))
        writer.writeheader(); writer.writerows(rows)
    return {'state':'inventory_only','candidates':len(rows),'unique_files_hashed':len(cache),
            'failures':failures,'card_pairs_by_pack':dict(groups),
            'limitations':'Active consumers and native reader not yet checked; no deletion approved.'}


if __name__ == '__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for n in ('vault','pairs','output','report'):p.add_argument('--'+n,type=Path,required=True)
    a=p.parse_args();result=inventory(a.vault,a.pairs,a.output)
    with a.report.open('x',encoding='utf8') as stream:json.dump(result,stream,indent=2);stream.write('\n')
    print(json.dumps(result))
