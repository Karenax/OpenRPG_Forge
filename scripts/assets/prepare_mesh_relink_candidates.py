"""Prepare a CSV of equal HDRP mesh copies with URP retained; no mutation."""
import argparse
import csv
import io
import json
from pathlib import Path
import vault_storage as storage


def prepare(comparison_path,refs_path):
    comparison_raw=comparison_path.read_bytes();refs_raw=refs_path.read_bytes()
    comparison,refs=json.loads(comparison_raw),json.loads(refs_raw)
    if (refs['source_sha256']!=storage.sha(comparison_raw)
            or refs['source_registry_sha256']!=comparison['source_registry_sha256']):
        raise ValueError('reference audit does not match comparison')
    mapping={}
    for row in comparison['results']:
        if row['state']!='identical_dependencies':continue
        for item in row['dependencies']:
            if not item['same_mesh'] or not item['same_meta']:raise ValueError('nonidentical mesh pair')
            old,keep=item['hdrp'],item['urp']
            record=(keep,item['hdrp_info']['sha256'],item['hdrp_info']['bytes'])
            if old in mapping and mapping[old]!=record:raise ValueError('ambiguous retained mesh')
            mapping[old]=record
    if not mapping:raise ValueError('no mesh mappings')
    if {x.casefold() for x in mapping}&{x[0].casefold() for x in mapping.values()}:
        raise ValueError('retained path overlaps old')
    for old in mapping:
        entries=refs['paths'].get(old)
        if not entries or any(('/source_mesh_files/' not in x['field'] and not x['field'].endswith('/source_mesh_file')) or
                              not x['document'].startswith('metadata/asset-source-support-revisions/') for x in entries):
            raise ValueError('unsupported active reference: '+old)
        meta=refs['paths'].get(old+'.meta')
        if meta is None or meta:raise ValueError('meta has active reference or is absent from audit')
    out=io.StringIO(newline='');writer=csv.DictWriter(out,fieldnames=['candidate','retained_hdrp','sha256'])
    writer.writeheader()
    for old,(keep,pin,_) in sorted(mapping.items()):
        writer.writerow({'candidate':old,'retained_hdrp':json.dumps([keep]),'sha256':pin})
    return out.getvalue(),len(mapping),sum(x[2] for x in mapping.values())


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for name in ('comparison','refs','output'):p.add_argument('--'+name,type=Path,required=True)
    a=p.parse_args();raw,count,size=prepare(a.comparison,a.refs)
    with a.output.open('x',encoding='utf8',newline='') as f:f.write(raw)
    print(json.dumps({'state':'candidate_only','meshes':count,'logical_bytes':size}))
