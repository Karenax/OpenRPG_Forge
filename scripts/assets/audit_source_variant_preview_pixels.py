"""Compare pinned source-card thumbnails for otherwise matching pipeline pairs.

This is an inventory, not proof of equivalent materials or authorization to
merge cards. Each unique thumbnail is read once and checked against its pin.
"""
import argparse
import hashlib
import io
import json
from pathlib import Path

from PIL import Image
import vault_storage as storage
from stage_source_support_pair_consolidation import PROVENANCE_DIFFERENCES


def pixel_pin(raw):
    with Image.open(io.BytesIO(raw)) as image:
        image.load()
        rgba=image.convert('RGBA')
        return (rgba.size, hashlib.sha256(rgba.tobytes()).hexdigest())


def audit(vault, inventory_path):
    vault=vault.resolve(strict=True)
    inv_raw=inventory_path.read_bytes();inv=json.loads(inv_raw)
    pointer=storage.confined(vault,vault/'metadata/asset-source-support-registry.v1.json')
    pointer_raw=pointer.read_bytes();record=json.loads(pointer_raw)
    if storage.sha(pointer_raw)!=inv['source_registry_sha256']:raise ValueError('source registry changed')
    catalog=storage.confined(vault,vault/record['path']).read_bytes()
    if storage.sha(catalog)!=record['sha256']:raise ValueError('source catalog changed')
    cards={c['id']:c for c in json.loads(catalog)['assets']}
    candidates=[p for p in inv['pairs'] if p['declared_source_sha_equal'] and p['declared_meta_sha_equal']
                and set(p['different_fields'])-PROVENANCE_DIFFERENCES == {'preview_sha256'}]
    cache={};results=[]
    for pair in candidates:
        result={'hdrp_id':pair['hdrp_id'],'urp_id':pair['urp_id']}
        try:
            pins=[]
            for ident in (pair['hdrp_id'],pair['urp_id']):
                card=cards[ident];relative=card.get('preview');sha=card.get('preview_sha256')
                if not isinstance(relative,str) or not storage.valid_hex(sha,64) or Path(relative).is_absolute():
                    raise ValueError('missing or invalid thumbnail declaration')
                path=storage.confined(vault,vault/relative)
                if path not in cache:
                    raw=path.read_bytes()
                    cache[path]=(storage.sha(raw),pixel_pin(raw))
                if cache[path][0]!=sha:raise ValueError('thumbnail SHA differs')
                pins.append(cache[path][1])
            result['state']='equal_pixels' if pins[0]==pins[1] else 'different_pixels_or_size'
            result['dimensions']=[p[0] for p in pins]
        except (OSError,ValueError,KeyError,Image.DecompressionBombError) as error:
            result.update(state='review_required',reason=str(error))
        results.append(result)
    if pointer.read_bytes()!=pointer_raw or inventory_path.read_bytes()!=inv_raw:
        raise ValueError('inputs changed during audit')
    return {'state':'audit_only','source_registry_sha256':storage.sha(pointer_raw),
            'inventory_sha256':storage.sha(inv_raw),'thumbnails_read':len(cache),'results':results,
            'limitations':'Only thumbnail pixels are compared; materials, dependencies and runtime are not qualified.'}


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for name in ('vault','inventory','output'):p.add_argument('--'+name,type=Path,required=True)
    a=p.parse_args();result=audit(a.vault,a.inventory)
    with a.output.open('x',encoding='utf8') as f:json.dump(result,f,ensure_ascii=False,indent=2);f.write('\n')
    print(json.dumps({'pairs':len(result['results']),'thumbnails_read':result['thumbnails_read'],
        'equal_pixels':sum(x['state']=='equal_pixels' for x in result['results']),
        'review_required':sum(x['state']=='review_required' for x in result['results'])}))
