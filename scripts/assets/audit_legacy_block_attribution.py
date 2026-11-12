"""Attribute indexed shared blocks to active models, legacy aliases and other index rows.

Metadata/index accounting only. It does not scan all Vault consumers, prove
reclaimable disk space, modify the index, or authorize pack deletion.
"""
import argparse
import collections
import json
import sqlite3
from pathlib import Path

import vault_storage as storage

ACTIVE, HISTORICAL, OTHER, SNAPSHOT = 1, 2, 4, 8


def add_descriptor(blocks, descriptor, mask):
    storage.validate_descriptor(descriptor, storage.MAX_MODEL)
    for segment in descriptor['segments']:
        location=(segment['pack'],segment['offset'])
        content=(segment['stored_bytes'],segment['bytes'],segment['sha256'],segment['codec'])
        if location in blocks and blocks[location][0] != content:
            raise ValueError('conflicting records for physical block')
        previous=blocks.get(location,(content,0))
        blocks[location]=(content,previous[1] | mask)


def summarize(blocks):
    buckets=collections.defaultdict(lambda:{'blocks':0,'stored_bytes':0,'logical_block_bytes':0})
    for (stored, logical, _, _),mask in blocks.values():
        row=buckets[mask];row['blocks']+=1;row['stored_bytes']+=stored;row['logical_block_bytes']+=logical
    return {str(k):v for k,v in sorted(buckets.items())}


def index_mask(key, active, historical, snapshots):
    mask=(ACTIVE if key in active else 0) | (SNAPSHOT if key in snapshots else 0)
    if key not in active and key not in historical and key not in snapshots:mask|=OTHER
    return mask


def audit(vault, include_snapshots=False):
    vault=vault.resolve(strict=True)
    index=storage.confined(vault,vault/'metadata/asset-import-registry.v1.json')
    before=index.read_bytes();reg=json.loads(before)
    active={};active_paths={};historic=set();snapshot_paths=set();blocks={};schemas=collections.Counter();counts=collections.Counter()
    def key(path,sha):
        p=Path(path)
        if not p.is_absolute() or not p.is_relative_to(vault) or '..' in p.parts:
            raise ValueError('model path outside Vault')
        return (p.relative_to(vault).as_posix().casefold(),sha)
    for entry in reg['packs'].values():
        path=storage.confined(vault,vault/entry['path']/'details.v1.json');data=path.read_bytes()
        if storage.sha(data)!=entry['files']['details.v1.json']:raise ValueError('catalog SHA changed')
        for card in json.loads(data)['assets']:
            k=key(card['model_path'],card['model_sha256'])
            active[k]=active.get(k,0)+1;active_paths[k]=Path(card['model_path']);counts['active_cards']+=1
            for alias in card.get('consolidated_variants',[]):
                recipe=alias.get('legacy_recipe') or {};schema=recipe.get('schema','missing');schemas[schema]+=1
                if schema=='openrpg.legacy-vault-descriptor.v1':
                    d=recipe['descriptor']
                    if d['sha256']!=alias['retired_model_sha256'] or d['sha256']!=recipe['logical_sha256']:
                        raise ValueError('historical descriptor hash mismatch')
                    add_descriptor(blocks,d,HISTORICAL)
                    counts['historical_logical_bytes']+=d['bytes']
                    original=alias.get('original_detail') or {}
                    if original.get('model_path') and original.get('model_sha256')==d['sha256']:
                        historic.add(key(original['model_path'],d['sha256']))
    if include_snapshots:
        snapshots=[p for entry in reg['packs'].values() for p in entry.get('history',[])]
        seen=set()
        for number,pointer in enumerate(snapshots,1):
            path=storage.confined(vault,vault/pointer['path']/'details.v1.json')
            if path in seen:raise ValueError('duplicate snapshot path')
            seen.add(path);data=path.read_bytes()
            if storage.sha(data)!=pointer['files']['details.v1.json']:raise ValueError('snapshot details SHA changed')
            for card in json.loads(data)['assets']:
                snapshot_paths.add(key(card['model_path'],card['model_sha256']))
                for alias in card.get('consolidated_variants',[]):
                    recipe=alias.get('legacy_recipe') or {}
                    if recipe.get('schema')=='openrpg.legacy-vault-descriptor.v1':
                        d=recipe['descriptor']
                        if d['sha256']!=alias['retired_model_sha256'] or d['sha256']!=recipe['logical_sha256']:
                            raise ValueError('snapshot historical descriptor SHA mismatch')
                        add_descriptor(blocks,d,SNAPSHOT)
            if number%50==0 or number==len(snapshots):print(f'Inspected {number}/{len(snapshots)} snapshots',flush=True)
        counts['snapshots_verified']=len(snapshots)
    if (vault/'.forge-storage/WRITER.lock').exists():raise ValueError('active or unresolved storage writer lock')
    db_path=storage.confined(vault,vault/'.forge-storage/index.sqlite')
    db=sqlite3.connect('file:'+db_path.as_posix()+'?mode=ro',uri=True)
    seen_active=set()
    try:
        db.execute('PRAGMA query_only=ON');db.execute('BEGIN')
        for number,(path,sha,raw,descriptor_sha) in enumerate(db.execute('SELECT path,sha256,descriptor,descriptor_sha256 FROM models'),1):
            if not isinstance(raw,bytes) or storage.sha(raw)!=descriptor_sha or not raw.startswith(storage.MAGIC):
                raise ValueError('invalid indexed descriptor')
            doc=storage.decode_json(raw[len(storage.MAGIC):])
            if doc['sha256']!=sha:raise ValueError('indexed descriptor logical SHA differs')
            k=(path.replace('\\','/').casefold(),sha)
            mask=index_mask(k,active,historic,snapshot_paths)
            if k in historic:counts['historical_index_rows_matched']+=1
            if mask: add_descriptor(blocks,doc,mask)
            if k in active:seen_active.add(k)
            counts['indexed_models']+=1
            if number%5000==0:print(f'Inspected {number} indexed models',flush=True)
        db.rollback()
    finally:db.close()
    missing_active=[]
    for k in set(active)-seen_active:
        path=active_paths[k]
        try:
            descriptor=storage.read_descriptor(vault,path)
            if descriptor is None:
                counts['active_unindexed_raw_paths']+=1
            else:
                if descriptor['sha256']!=k[1]:raise ValueError('unindexed active descriptor SHA differs')
                add_descriptor(blocks,descriptor,ACTIVE)
                counts['active_unindexed_descriptor_paths']+=1
        except FileNotFoundError:
            missing_active.append(str(path))
    if index.read_bytes()!=before or (vault/'.forge-storage/WRITER.lock').exists():
        raise ValueError('registry changed or storage writer active')
    counts['active_unindexed_paths']=len(set(active)-seen_active)
    counts['active_missing_paths']=len(missing_active)
    counts['historical_paths_in_alias_metadata']=len(historic)
    labels={str(mask):' + '.join(name for bit,name in ((ACTIVE,'active'),(HISTORICAL,'historical'),
             (OTHER,'other indexed'),(SNAPSHOT,'snapshot')) if mask&bit) for mask in range(1,16)}
    return {'state':'audit_only','registry_sha256':storage.sha(before),'counts':dict(counts),
            'snapshots_included':include_snapshots,
            'historical_recipe_schemas':dict(schemas),'active_missing_paths':missing_active,'block_masks':labels,'buckets':summarize(blocks),
            'limitations':'Metadata and index snapshot only: unindexed assets, historical snapshots, external consumers, SQLite allocation and pack overhead are not fully attributed. Historical-only stored bytes are not a deletion or disk-gain estimate.'}


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--vault',type=Path,required=True);p.add_argument('--output',type=Path,required=True)
    p.add_argument('--include-snapshots',action='store_true')
    a=p.parse_args()
    if a.output.exists():p.error('output exists')
    result=audit(a.vault,a.include_snapshots)
    with a.output.open('x',encoding='utf8',newline='\n') as f:json.dump(result,f,ensure_ascii=False,indent=2);f.write('\n')
    print(json.dumps({'state':result['state'],'counts':result['counts'],'buckets':result['buckets']}))
