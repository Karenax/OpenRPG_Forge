"""Prepare immutable derived model catalogs with identical shared previews.

No Vault file is written and no model or image is deleted. Only candidates whose
two image paths share a directory are considered by this first pass.
"""
import argparse
import copy
import json
from pathlib import Path

import vault_storage as storage
from prepare_variant_metadata_transition import encoded
from stage_source_support_relinks import digest_file


def mapping_from_audit(audit):
    result = {}
    for group in audit['groups']:
        if not storage.valid_hex(group['sha256'], 64):
            raise ValueError('invalid preview SHA')
        by_dir = {}
        for row in group['paths']:
            if row.get('state') != 'verified':
                continue
            path = Path(row['path'])
            by_dir.setdefault(str(path.parent).casefold(), []).append(row)
        for rows in by_dir.values():
            rows.sort(key=lambda r: r['path'].casefold())
            for row in rows[1:]:
                key = row['path'].casefold()
                if key in result:
                    raise ValueError('duplicate preview candidate')
                result[key] = {'old': row['path'], 'retained': rows[0]['path'],
                               'sha256': group['sha256'], 'ids': row['ids']}
    return result


def asset_root(catalog, detail, item):
    model = Path(detail['model_path'])
    relative = Path(item['model'])
    preview = Path(item['preview'])
    if relative.is_absolute() or preview.is_absolute() or '..' in relative.parts or '..' in preview.parts:
        raise ValueError('catalog path escapes asset root')
    if tuple(x.casefold() for x in model.parts[-len(relative.parts):]) == tuple(x.casefold() for x in relative.parts):
        for _ in relative.parts:
            model = model.parent
        return model
    return catalog.parent


def change_preview(item, old_path, retained_path, root, expected):
    if item.get('preview_sha256') != expected:
        raise ValueError('preview SHA changed in catalog')
    rel = Path(item['preview'])
    if (root/rel).as_posix().casefold() != Path(old_path).as_posix().casefold():
        raise ValueError('preview path changed in catalog')
    retained = Path(retained_path)
    if retained.parent.as_posix().casefold() != Path(old_path).parent.as_posix().casefold():
        raise ValueError('retained preview must share a directory')
    item['preview'] = (rel.parent/retained.name).as_posix()


def prepare(vault, audit_file, stage):
    vault = vault.resolve(strict=True)
    audit_raw = audit_file.read_bytes()
    audit = json.loads(audit_raw)
    registry_path = storage.confined(vault, vault/'metadata/asset-import-registry.v1.json')
    support_path = storage.confined(vault, vault/'metadata/asset-source-support-registry.v1.json')
    before, support_before = registry_path.read_bytes(), support_path.read_bytes()
    if storage.sha(before) != audit['registry_sha256']:
        raise ValueError('model registry changed')
    mappings = mapping_from_audit(audit)
    if not mappings:
        raise ValueError('no same-directory preview pairs')
    wanted = {ident for row in mappings.values() for ident in row['ids']}
    if sum(len(x['ids']) for x in mappings.values()) != len(wanted):
        raise ValueError('model identity appears in multiple preview candidates')
    for row in mappings.values():
        for side in ('old','retained'):
            path = storage.confined(vault, Path(row[side]))
            if digest_file(path) != row['sha256']:
                raise ValueError('preview SHA changed')
    registry = json.loads(before)
    pack_docs = {}
    catalog_docs = {}
    affected = {}
    found = set()
    for pack, entry in sorted(registry['packs'].items()):
        docs = {}
        for key, name in (('details','details.v1.json'),('profiles','profiles.v1.json')):
            raw = storage.confined(vault, vault/entry['path']/name).read_bytes()
            if storage.sha(raw) != entry['files'][name]:
                raise ValueError('model snapshot SHA changed')
            docs[key] = json.loads(raw)
        for detail in docs['details']['assets']:
            ident = detail['id']
            if ident not in wanted:
                continue
            found.add(ident)
            catalog_path = storage.confined(vault, Path(detail['source_catalog']))
            if catalog_path not in catalog_docs:
                original = catalog_path.read_bytes()
                catalog_docs[catalog_path] = {'raw':original, 'doc':json.loads(original)}
            doc = catalog_docs[catalog_path]['doc']
            matches = [item for item in doc['assets'] if item['id'] == ident]
            if len(matches) != 1 or matches[0]['sha256'] != detail['model_sha256']:
                raise ValueError('catalog identity/model mismatch: '+ident)
            item = matches[0]
            root = asset_root(catalog_path, detail, item)
            path = str(root/Path(item['preview'])).casefold()
            if path not in mappings or ident not in mappings[path]['ids']:
                raise ValueError('preview mapping differs: '+ident)
            row = mappings[path]
            change_preview(item, row['old'], row['retained'], root, row['sha256'])
            affected[ident] = catalog_path
        if any(d['id'] in wanted for d in docs['details']['assets']):
            pack_docs[pack] = docs
    if found != wanted:
        raise ValueError('one or more candidate model identities missing')
    catalogs = {}
    for number, (path, entry) in enumerate(sorted(catalog_docs.items(), key=lambda x:str(x[0]).casefold()), 1):
        raw = encoded(entry['doc'])
        sha = storage.sha(raw)
        filename = 'openrpg-preview-'+sha+'.json'
        new_path = path.parent/filename
        catalogs[path] = {'old':str(path), 'old_sha256':storage.sha(entry['raw']),
                          'new':str(new_path), 'new_sha256':sha, 'stage_file':f'catalog-{number:04d}.json', 'raw':raw}
    for docs in pack_docs.values():
        for detail in docs['details']['assets']:
            if detail['id'] in affected:
                rec = catalogs[affected[detail['id']]]
                if 'source_catalog_preview_origin' in detail:
                    raise ValueError('existing preview-origin metadata requires explicit migration')
                detail['source_catalog_preview_origin'] = {'path':rec['old'], 'sha256':rec['old_sha256']}
                detail['source_catalog'] = rec['new']
    if registry_path.read_bytes() != before or support_path.read_bytes() != support_before or audit_file.read_bytes() != audit_raw:
        raise ValueError('inputs changed during preparation')
    for path, entry in catalog_docs.items():
        if path.read_bytes() != entry['raw']:
            raise ValueError('original catalog changed')
    stage.mkdir(exist_ok=False)
    cat_rows = []
    for rec in catalogs.values():
        raw = rec.pop('raw')
        (stage/rec['stage_file']).write_bytes(raw)
        cat_rows.append(rec)
    packs = {}
    for pack, docs in pack_docs.items():
        directory = stage/pack
        directory.mkdir()
        files = {}
        for key, name in (('details','details.v1.json'),('profiles','profiles.v1.json')):
            raw = encoded(docs[key])
            (directory/name).write_bytes(raw)
            files[name] = storage.sha(raw)
        files['revision'] = storage.sha(encoded({'profiles':docs['profiles'],'details':docs['details']}))
        packs[pack] = files
    manifest = {'schema':'openrpg.model-preview-relink-stage.v1','state':'prepared_only',
                'registry_sha256':storage.sha(before),'source_registry_sha256':storage.sha(support_before),
                'audit_sha256':storage.sha(audit_raw),'candidate_paths':list(mappings.values()),
                'changed_ids':sorted(wanted),'catalogs':cat_rows,'packs':packs,
                'limitations':'Derived catalogs and snapshots are not published; reader, references and retirement not qualified.'}
    (stage/'plan.json').write_bytes(encoded(manifest))
    return manifest


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--vault', type=Path, required=True)
    p.add_argument('--audit', type=Path, required=True)
    p.add_argument('--stage', type=Path, required=True)
    a = p.parse_args()
    root = Path(r'E:\OpenRPG_Forge\.build\asset-conversion').resolve(strict=True)
    stage = a.stage.resolve(strict=False)
    if not stage.is_relative_to(root) or stage.exists(): p.error('new stage must be under asset conversion workspace')
    result = prepare(a.vault, a.audit, stage)
    print(json.dumps({'state':result['state'],'paths':len(result['candidate_paths']),
                      'models':len(result['changed_ids']),'catalogs':len(result['catalogs']),'packs':len(result['packs'])}))
