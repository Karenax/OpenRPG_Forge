"""Compare pinned Unity text sources and identify differences for review.

No equivalence, conversion, runtime behavior, or deletion is implied.
"""
import argparse
import collections
import difflib
import hashlib
import json
import re
from pathlib import Path

import vault_storage as storage
from stage_source_support_relinks import norm

GUID = re.compile(r'\bguid:\s*([0-9a-f]{32})\b',re.I)
HEADER = re.compile(r'^---\s+!u!(\d+)\s+&(-?\d+)')
FIELD = re.compile(r'^\s*(?:-\s*)?([A-Za-z_][A-Za-z_0-9.]*)\s*:')
LOD_THRESHOLD = re.compile(r'^(\s*(?:-\s*)?screenRelativeHeight:\s*)[-+0-9.eE]+(\s*)$')


def summarize_diff(left,right):
    a=left.splitlines();b=right.splitlines()
    fields=collections.Counter();lines=0
    matcher=difflib.SequenceMatcher(None,a,b,autojunk=False)
    for op,i,j,k,l in matcher.get_opcodes():
        if op=='equal':continue
        for line in a[i:j]+b[k:l]:
            lines+=1
            m=FIELD.match(line)
            if m:fields[m.group(1)]+=1
    def classes(value):
        return collections.Counter(m.group(1) for line in value if (m:=HEADER.match(line)))
    def guids(value):return set(GUID.findall(value))
    first,second=classes(a),classes(b);ga,gb=guids(left),guids(right)
    def mask_thresholds(lines):
        return [LOD_THRESHOLD.sub(r'\g<1><LOD_THRESHOLD>\g<2>',line) for line in lines]
    return {'left_lines':len(a),'right_lines':len(b),'changed_lines':lines,
            'different_fields':dict(fields.most_common()),'hdrp_classes':dict(first),'urp_classes':dict(second),
            'hdrp_only_guids':sorted(ga-gb),'urp_only_guids':sorted(gb-ga),
            'both_guids':len(ga&gb),'normalized_lines_equal':a==b,
            'equal_except_lod_thresholds':mask_thresholds(a)==mask_thresholds(b)}


def audit(vault,inventory,max_bytes):
    vault=vault.resolve(strict=True);inventory_raw=inventory.read_bytes();data=json.loads(inventory_raw)
    ptr=storage.confined(vault,vault/'metadata/asset-source-support-registry.v1.json');before=ptr.read_bytes()
    if storage.sha(before)!=data['source_registry_sha256']:raise ValueError('source inventory stale')
    entry=json.loads(before);raw=storage.confined(vault,vault/entry['path']).read_bytes()
    if storage.sha(raw)!=entry['sha256']:raise ValueError('catalog SHA differs')
    cards={x['id']:x for x in json.loads(raw)['assets']};results=[];cache={};read_bytes=0
    def read(card):
        nonlocal read_bytes
        rel=card.get('source_file');pin=card.get('source_sha256');norm(rel)
        if not storage.valid_hex(pin,64):raise ValueError('missing declared source SHA')
        path=storage.confined(vault,vault/rel)
        if path not in cache:
            size=path.stat().st_size
            if size>max_bytes:return None,size
            with storage.locked_source(path,share_delete=False) as f:content=f.read(max_bytes+1)
            if len(content)!=size or hashlib.sha256(content).hexdigest()!=pin:raise ValueError('source SHA differs')
            if b'\0' in content:raise ValueError('binary source')
            try:text=content.decode('utf-8-sig')
            except UnicodeDecodeError:raise ValueError('non UTF-8 source')
            cache[path]=(pin,text,size);read_bytes+=size
        saved,text,size=cache[path]
        if saved!=pin:raise ValueError('conflicting source SHA')
        return text,size
    for pair in data['pairs']:
        if pair['declared_source_sha_equal']:continue
        item={'hdrp_id':pair['hdrp_id'],'urp_id':pair['urp_id'],'source_path':pair['source_path']}
        try:
            left,ls=read(cards[pair['hdrp_id']]);right,rs=read(cards[pair['urp_id']])
            item.update(hdrp_bytes=ls,urp_bytes=rs)
            if left is None or right is None or left.count('\n')>10000 or right.count('\n')>10000:item.update(state='size_limit')
            else:item.update(state='compared',comparison=summarize_diff(left,right))
        except (ValueError,KeyError,OSError) as exc:item.update(state='review_required',reason=str(exc))
        results.append(item)
    if ptr.read_bytes()!=before or inventory.read_bytes()!=inventory_raw:raise ValueError('inputs changed')
    return {'state':'audit_only','source_registry_sha256':storage.sha(before),'inventory_sha256':storage.sha(inventory_raw),
            'max_bytes':max_bytes,'files_hashed':len(cache),'bytes_hashed':read_bytes,'results':results,
            'limitations':'Text comparison only; no equivalence, dependency completeness, rendering or deletion qualification.'}


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for n in ('vault','inventory','output'):p.add_argument('--'+n,type=Path,required=True)
    p.add_argument('--max-bytes',type=int,default=2*1024*1024);a=p.parse_args()
    result=audit(a.vault,a.inventory,a.max_bytes)
    with a.output.open('x',encoding='utf8') as f:json.dump(result,f,ensure_ascii=False,indent=2);f.write('\n')
    print(json.dumps({'pairs':len(result['results']),'states':dict(collections.Counter(x['state'] for x in result['results'])),
                      'files_hashed':result['files_hashed'],'bytes_hashed':result['bytes_hashed']}))
