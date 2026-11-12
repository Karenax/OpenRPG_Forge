"""Find active source pairs differing only in LOD values and collider labels."""
import argparse
import json
from pathlib import Path
import vault_storage as storage
from compare_unity_lod_collider_variants import compare
from stage_source_support_relinks import norm


def audit(vault,inventory):
    vault=vault.resolve(strict=True);raw_inventory=inventory.read_bytes();inv=json.loads(raw_inventory)
    pointer=storage.confined(vault,vault/'metadata/asset-source-support-registry.v1.json');before=pointer.read_bytes()
    if storage.sha(before)!=inv['source_registry_sha256']:raise ValueError('stale inventory')
    record=json.loads(before);raw=storage.confined(vault,vault/record['path']).read_bytes()
    if storage.sha(raw)!=record['sha256']:raise ValueError('catalog SHA differs')
    cards={c['id']:c for c in json.loads(raw)['assets']};cache={};rows=[]
    def load(card):
        rel=card['source_file'];norm(rel);path=storage.confined(vault,vault/rel);pin=card['source_sha256']
        if not storage.valid_hex(pin,64):raise ValueError('invalid source pin')
        if path not in cache:
            if path.stat().st_size>2*1024*1024:raise ValueError('source exceeds comparison budget')
            with storage.locked_source(path,share_delete=False) as stream:data=stream.read(2*1024*1024+1)
            if storage.sha(data)!=pin:raise ValueError('source SHA differs')
            cache[path]=(pin,data.decode('utf-8-sig'))
        if cache[path][0]!=pin:raise ValueError('conflicting source pin')
        return cache[path][1]
    for p in inv['pairs']:
        if p['declared_source_sha_equal'] or not p['declared_meta_sha_equal']:continue
        row={'hdrp_id':p['hdrp_id'],'urp_id':p['urp_id']}
        try:
            a,b=(load(cards[p[x]]) for x in ('hdrp_id','urp_id'))
            row['state']='lod_collider_only' if compare(a,b) else 'different'
        except (ValueError,OSError,KeyError,UnicodeError) as e:row.update(state='review_required',reason=str(e))
        rows.append(row)
    if pointer.read_bytes()!=before or inventory.read_bytes()!=raw_inventory:raise ValueError('inputs changed')
    return {'state':'audit_only','source_registry_sha256':storage.sha(before),'inventory_sha256':storage.sha(raw_inventory),
            'files_hashed':len(cache),'results':rows,'limitations':'Exact source comparison apart from LOD values and known collider names; rendering and runtime unqualified.'}


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for n in ('vault','inventory','output'):p.add_argument('--'+n,type=Path,required=True)
    a=p.parse_args();r=audit(a.vault,a.inventory)
    with a.output.open('x',encoding='utf8') as f:json.dump(r,f,indent=2);f.write('\n')
    print(json.dumps({'pairs':len(r['results']),'eligible':sum(x['state']=='lod_collider_only' for x in r['results']),
                      'review_required':sum(x['state']=='review_required' for x in r['results'])}))
