"""Compare source collision declarations in same-name Haunted Village pairs.

This is evidence only: local fields do not prove parent-space/world equivalence,
runtime behavior or permission to remove either source/model.
"""
import argparse
import collections
import json
import re
from pathlib import Path

import vault_storage as storage

COLLIDERS = {'64':'mesh', '65':'box', '135':'sphere', '136':'capsule'}
FIELDS = ('m_Enabled', 'm_IsTrigger', 'm_Radius', 'm_Height', 'm_Direction',
          'm_Center', 'm_Size', 'm_Convex', 'm_Mesh', 'm_Material')


def parse_colliders(raw):
    text = raw.decode('utf8', errors='strict')
    blocks = re.findall(r'(?ms)^--- !u!(\d+) &[^\r\n]*\r?\n(.*?)(?=^--- !u!|\Z)', text)
    counts = collections.Counter(kind for kind, _ in blocks)
    found = []
    for kind, body in blocks:
        if kind not in COLLIDERS:
            continue
        fields = {}
        for field in FIELDS:
            matches = re.findall(r'(?m)^\s*' + re.escape(field) + r':\s*([^\r\n]+)', body)
            if len(matches) > 1:
                raise ValueError('repeated collider field: ' + field)
            if matches:
                fields[field] = matches[0].strip()
        found.append({'type': COLLIDERS[kind], 'fields': fields})
    found.sort(key=lambda x: json.dumps(x, sort_keys=True))
    return {'colliders':found, 'component_counts':dict(counts)}


def audit(vault, evidence):
    vault = vault.resolve(strict=True)
    evidence_bytes = evidence.read_bytes()
    doc = json.loads(evidence_bytes)
    index = storage.confined(vault, vault/'metadata/asset-import-registry.v1.json')
    before = index.read_bytes()
    groups = [g for g in doc['groups'] if len(g['comparison']) == 1
              and all(g['comparison'][0][k] for k in ('mesh_payloads_exact','images_exact','materials_exact','transforms_exact'))
              and not g['comparison'][0]['source_components_same_counts']]
    ids = {x['id'] for g in groups for x in g['cards']}
    active = set()
    for entry in json.loads(before)['packs'].values():
        data = storage.confined(vault, vault/entry['path']/'details.v1.json').read_bytes()
        if storage.sha(data) != entry['files']['details.v1.json']: raise ValueError('details changed')
        active.update(x['id'] for x in json.loads(data)['assets'] if x['id'] in ids)
    if active != ids: raise ValueError('source evidence includes inactive identities')
    cache = {}
    rows = []
    for group in groups:
        cards = []
        for item in group['cards']:
            path = Path(item['source_path'])
            storage.confined(Path(path.anchor), path)
            if path not in cache:
                raw = path.read_bytes()
                cache[path] = (storage.sha(raw), parse_colliders(raw))
            sha, parsed = cache[path]
            if sha != item['source_sha256']: raise ValueError('source prefab changed')
            cards.append({'id':item['id'], 'source_sha256':sha, **parsed})
        rows.append({'pack':group['pack'],'source_filename':group['source_filename'],
                     'lod':group['lod'],'cards':cards,
                     'local_collider_fields_equal':cards[0]['colliders']==cards[1]['colliders']})
    if index.read_bytes()!=before or evidence.read_bytes()!=evidence_bytes:
        raise ValueError('inputs changed')
    return {'state':'audit_only','registry_sha256':storage.sha(before),'groups':rows,
            'unique_sources_read':len(cache),
            'limitations':'Local collider fields alone omit parent transforms and do not establish runtime equivalence.'}


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for name in ('vault','evidence','output'):p.add_argument('--'+name,type=Path,required=True)
    a=p.parse_args()
    if a.output.exists():p.error('output exists')
    result=audit(a.vault,a.evidence)
    with a.output.open('x',encoding='utf8',newline='\n') as f:json.dump(result,f,ensure_ascii=False,indent=2);f.write('\n')
    print(json.dumps({'groups':len(result['groups']),'matching_local_fields':sum(x['local_collider_fields_equal'] for x in result['groups']),
                      'unique_sources_read':result['unique_sources_read']}))
