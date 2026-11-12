"""Find local promotion manifests and cards using candidate model paths.

This complements the active-registry audit and does not authorize deletion.
JSON and known text files under selected candidate packs are inspected; no links are followed.
"""
import argparse
import json
import os
from pathlib import Path

import vault_storage as storage

TEXT_EXTENSIONS = {'.meta', '.prefab', '.mat', '.shadergraph', '.wgsl', '.md', '.txt', '.yaml', '.yml'}


def strings(value, path=''):
    if isinstance(value, str):
        yield path, value.replace('\\', '/').casefold()
    elif isinstance(value, dict):
        for key, child in value.items():
            yield from strings(child, path + '/' + str(key))
    elif isinstance(value, list):
        for i, child in enumerate(value):
            yield from strings(child, path + '/' + str(i))


def find_references(vault, candidates):
    cache = {}
    pack_docs = {}
    rows = []
    for item in candidates:
        path = Path(item['retired_path'])
        if not path.is_absolute() or not path.is_relative_to(vault) or '..' in path.parts:
            raise ValueError('candidate outside Vault')
        parts = path.relative_to(vault).parts
        if not parts or parts[0].lower() != 'candidates':
            continue
        path = storage.confined(vault, path)
        if len(parts) != 5 or parts[:2] != ('candidates', 'hivemind') or parts[-2] != 'models' or path.suffix.lower() != '.glb':
            raise ValueError('candidate path outside expected scope')
        pack = path.parent.parent
        targets = {f'models/{path.name}'.casefold(), str(path).replace('\\', '/').casefold(), path.name.casefold()}
        refs = []
        if pack not in pack_docs:
            files = []
            for root, dirs, names in os.walk(pack, followlinks=False):
                for name in dirs:
                    entry = Path(root)/name
                    info = entry.lstat()
                    if entry.is_symlink() or getattr(info,'st_file_attributes',0)&0x400:
                        raise ValueError('candidate pack contains a directory link')
                for name in names:
                    if Path(name).suffix.lower() not in TEXT_EXTENSIONS | {'.json'}: continue
                    entry = Path(root)/name
                    info = entry.lstat()
                    if entry.is_symlink() or getattr(info,'st_file_attributes',0)&0x400:
                        raise ValueError('candidate pack contains a text or JSON link')
                    files.append(storage.confined(vault, entry))
            pack_docs[pack] = sorted(files)
        for doc_path in pack_docs[pack]:
            if doc_path not in cache:
                data = doc_path.read_bytes()
                if doc_path.suffix.lower() == '.json':
                    content = list(strings(json.loads(data)))
                    kind = 'json'
                else:
                    content = [(str(i), line.replace('\\','/').casefold()) for i, line in enumerate(data.decode('utf8',errors='strict').splitlines(),1)]
                    kind = 'text'
                cache[doc_path] = (storage.sha(data), content, kind)
            _, content, kind = cache[doc_path]
            refs.extend({'document': str(doc_path), 'location': key, 'format':kind} for key, value in content
                        if (value.removeprefix('./') in targets if kind == 'json' else any(target in value for target in targets)))
        rows.append({'id':item['retired_id'], 'path':str(path), 'references':refs})
    for path, (sha, _, _) in cache.items():
        if storage.sha(path.read_bytes()) != sha: raise ValueError('candidate metadata changed')
    return rows


def audit(vault, plan_path):
    vault = vault.resolve(strict=True)
    source = plan_path.read_bytes()
    plan = json.loads(source)
    registry = storage.confined(vault, vault/'metadata/asset-import-registry.v1.json')
    if storage.sha(registry.read_bytes()) != plan['registry_sha256']:
        raise ValueError('registry changed')
    rows = find_references(vault, plan['candidates'])
    if storage.sha(registry.read_bytes()) != plan['registry_sha256'] or plan_path.read_bytes() != source:
        raise ValueError('inputs changed')
    return {'state':'audit_only', 'registry_sha256':plan['registry_sha256'], 'rows':rows,
            'limitations':'JSON strings and known text files within selected candidate packs only; binary formats and external consumers may exist.'}


if __name__ == '__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for key in ('vault','plan','output'): p.add_argument('--'+key,type=Path,required=True)
    a=p.parse_args()
    if a.output.exists(): p.error('output exists')
    result=audit(a.vault,a.plan)
    with a.output.open('x',encoding='utf8',newline='\n') as f: json.dump(result,f,ensure_ascii=False,indent=2);f.write('\n')
    print(json.dumps({'candidates':len(result['rows']), 'with_local_references':sum(bool(x['references']) for x in result['rows'])}))
