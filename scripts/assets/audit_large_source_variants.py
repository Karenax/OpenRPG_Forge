"""Compare large Unity text variants by streaming, preserving source bytes."""
import argparse
import hashlib
import json
import re
from pathlib import Path

import vault_storage as storage
from stage_source_support_relinks import norm

LOD=re.compile(rb'^(\s*(?:-\s*)?screenRelativeHeight:\s*)[-+0-9.eE]+(\s*)$')
MAX_LINE=4*1024*1024


def signatures(stream):
    original=hashlib.sha256();normalized=hashlib.sha256();lod=hashlib.sha256();count=size=0
    while True:
        line=stream.readline(MAX_LINE+1)
        if not line:break
        if len(line)>MAX_LINE:raise ValueError('line exceeds bounded comparison')
        if b'\x00' in line:raise ValueError('binary source')
        original.update(line);size+=len(line);count+=1
        if count==1 and line.startswith(b'\xef\xbb\xbf'):line=line[3:]
        line=line.rstrip(b'\r\n')
        normalized.update(line+b'\n')
        lod.update(LOD.sub(rb'\g<1><LOD_THRESHOLD>\g<2>',line)+b'\n')
    return {'sha256':original.hexdigest(),'normalized_sha256':normalized.hexdigest(),
            'lod_normalized_sha256':lod.hexdigest(),'bytes':size,'lines':count}


def audit(vault,inventory,prior,prior_inventory):
    vault=vault.resolve(strict=True);inv_raw=inventory.read_bytes();prior_raw=prior.read_bytes();prior_inv_raw=prior_inventory.read_bytes()
    inv=json.loads(inv_raw);old=json.loads(prior_raw);prior_inv=json.loads(prior_inv_raw)
    if old['inventory_sha256']!=storage.sha(prior_inv_raw) or old['source_registry_sha256']!=prior_inv['source_registry_sha256']:
        raise ValueError('prior audit differs from its inventory')
    ptr=storage.confined(vault,vault/'metadata/asset-source-support-registry.v1.json');before=ptr.read_bytes()
    if storage.sha(before)!=inv['source_registry_sha256']:raise ValueError('inventory registry changed')
    pointer=json.loads(before);raw=storage.confined(vault,vault/pointer['path']).read_bytes()
    if storage.sha(raw)!=pointer['sha256']:raise ValueError('catalog SHA changed')
    cards={x['id']:x for x in json.loads(raw)['assets']};previous_wanted={x['hdrp_id'] for x in old['results'] if x['state']=='size_limit'}
    wanted=previous_wanted & {x['hdrp_id'] for x in inv['pairs']}
    cache={};rows=[]
    def inspect(card):
        path=card.get('source_file');norm(path);pin=card.get('source_sha256')
        if not storage.valid_hex(pin,64):raise ValueError('missing source SHA')
        path=storage.confined(vault,vault/path)
        if path not in cache:
            with storage.locked_source(path,share_delete=False) as stream:cache[path]=signatures(stream)
        if cache[path]['sha256']!=pin:raise ValueError('source SHA differs')
        return cache[path]
    for pair in inv['pairs']:
        if pair['hdrp_id'] not in wanted:continue
        row={'hdrp_id':pair['hdrp_id'],'urp_id':pair['urp_id'],'source_path':pair['source_path']}
        try:
            a,b=inspect(cards[pair['hdrp_id']]),inspect(cards[pair['urp_id']])
            row.update(state='compared',hdrp=a,urp=b,normalized_equal=a['normalized_sha256']==b['normalized_sha256'],
                       lod_only=a['lod_normalized_sha256']==b['lod_normalized_sha256'] and a['normalized_sha256']!=b['normalized_sha256'])
        except (OSError,ValueError,KeyError) as exc:row.update(state='review_required',reason=str(exc))
        rows.append(row)
    if len(rows)!=len(wanted) or ptr.read_bytes()!=before or inventory.read_bytes()!=inv_raw or prior.read_bytes()!=prior_raw or prior_inventory.read_bytes()!=prior_inv_raw:
        raise ValueError('coverage or inputs changed')
    return {'state':'audit_only','source_registry_sha256':storage.sha(before),'inventory_sha256':storage.sha(inv_raw),
            'prior_audit_sha256':storage.sha(prior_raw),'prior_inventory_sha256':storage.sha(prior_inv_raw),
            'prior_candidates_no_longer_active':len(previous_wanted-wanted),'unique_files_hashed':len(cache),'results':rows,
            'limitations':'Streaming text comparison only; no render, Unity dependency or deletion qualification.'}


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for name in ('vault','inventory','prior','prior-inventory','output'):p.add_argument('--'+name,type=Path,required=True)
    a=p.parse_args();result=audit(a.vault,a.inventory,a.prior,a.prior_inventory)
    with a.output.open('x',encoding='utf8') as f:json.dump(result,f,indent=2);f.write('\n')
    print(json.dumps({'pairs':len(result['results']),'compared':sum(x['state']=='compared' for x in result['results']),
                      'review_required':sum(x['state']!='compared' for x in result['results']),
                      'lod_only':sum(x.get('lod_only',False) for x in result['results']),
                      'normalized_equal':sum(x.get('normalized_equal',False) for x in result['results'])}))
