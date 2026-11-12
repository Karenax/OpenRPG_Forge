"""Find text-serialized Unity GUID consumers of identical texture pairs.

Scope is limited to the specified extracted Unity trees. Binary and external
consumers are not covered. No files are modified.
"""
import argparse
import collections
import json
import re
from pathlib import Path
import vault_storage as storage
from inventory_pipeline_tree_copies import enumerate_tree
from stage_source_support_relinks import digest_file

GUID=re.compile(rb'(?im)^guid:\s*([0-9a-f]{32})\s*$')
# Unity serializers include YAML `guid: ...` and JSON shader graph GUIDs.
# Match every standalone 32-digit hex token as a conservative candidate;
# matching unrelated IDs is possible and must not authorize a deletion.
GUID_REF=re.compile(rb'(?i)(?<![0-9a-f])([0-9a-f]{32})(?![0-9a-f])')
TEXT={'.mat','.prefab','.asset','.shadergraph','.shadersubgraph','.unity','.controller',
      '.overridecontroller','.anim','.playable','.rendertexture','.terrainlayer','.vfx'}


def meta_guid(raw):
    matches=GUID.findall(raw)
    if len(matches)!=1:raise ValueError('meta must contain exactly one root GUID')
    return matches[0].decode('ascii').lower()


def audit(vault,comparison,roots,retired=False):
    vault=vault.resolve(strict=True);raw=comparison.read_bytes();data=json.loads(raw)
    root_raw=roots.read_bytes();root_groups=json.loads(root_raw)
    ptrs=[storage.confined(vault,vault/'metadata/asset-source-support-registry.v1.json'),
          storage.confined(vault,vault/'metadata/asset-import-registry.v1.json')]
    before=[p.read_bytes() for p in ptrs]
    if storage.sha(before[0])!=data['source_registry_sha256']:raise ValueError('source registry changed')
    lookup={};images=[]
    for row in data['results']:
        if row['state']!='identical_dependencies':continue
        for item in row['dependencies']:
            metas=[]
            for side in (('urp',) if retired else ('hdrp','urp')):
                path=storage.confined(vault,vault/(item[side]+'.meta'));content=path.read_bytes()
                if storage.sha(content)!=item[side+'_info']['meta_sha256']:raise ValueError('meta SHA changed')
                metas.append(meta_guid(content))
            if not retired and metas[0]!=metas[1]:raise ValueError('matching meta GUIDs differ')
            key=metas[0]
            if key in lookup and lookup[key]!=item['hdrp']:raise ValueError('duplicate GUID maps to different textures')
            lookup[key]=item['hdrp']
            images.append({'hdrp':item['hdrp'],'urp':item['urp'],'guid':key,'references':[]})
    index={x['guid']:x for x in images};read=0;files_pins={}
    for group,hrel,urel in root_groups:
        for side,rel in (('hdrp',hrel),('urp',urel)):
            root=storage.confined(vault,vault/rel);files,skipped=enumerate_tree(root)
            if skipped:raise ValueError('tree contains skipped reparse points')
            for path,_ in files.values():
                if path.suffix.lower() not in TEXT:continue
                content=path.read_bytes();files_pins[path]=storage.sha(content);read+=1
                for guid in set(x.decode('ascii').lower() for x in GUID_REF.findall(content)):
                    if guid in index:index[guid]['references'].append({'group':group,'side':side,
                        'path':path.relative_to(vault).as_posix()})
    for path,pin in files_pins.items():
        if digest_file(path)!=pin:raise ValueError('serialized source changed during scan')
    if any(p.read_bytes()!=b for p,b in zip(ptrs,before)) or comparison.read_bytes()!=raw or roots.read_bytes()!=root_raw:
        raise ValueError('audit inputs changed')
    return {'state':'audit_only','comparison_sha256':storage.sha(raw),'source_registry_sha256':storage.sha(before[0]),
        'model_registry_sha256':storage.sha(before[1]),'text_files_read':read,'images':images,
        'limitations':'Known text-serialized Unity files within specified trees only; binary and external references unverified.'}


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for name in ('vault','comparison','roots','output'):p.add_argument('--'+name,type=Path,required=True)
    p.add_argument('--retired',action='store_true')
    a=p.parse_args();result=audit(a.vault,a.comparison,a.roots,a.retired)
    with a.output.open('x',encoding='utf8') as f:json.dump(result,f,ensure_ascii=False,indent=2);f.write('\n')
    print(json.dumps({'images':len(result['images']),'text_files_read':result['text_files_read'],
        'images_with_references':sum(bool(x['references']) for x in result['images'])}))
