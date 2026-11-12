"""Audit whether a reconstructed Unity source root covers a converter queue."""
import argparse
import hashlib
import json
from pathlib import Path, PurePosixPath


def audit(queue_path, overlay_path, source_manifest_path=None):
    queue_raw=queue_path.read_bytes(); overlay_raw=overlay_path.read_bytes()
    queue=json.loads(queue_raw); overlay=json.loads(overlay_raw)
    if overlay.get('schema')!='openrpg.unity-shared-overlay.v1':raise ValueError('invalid overlay')
    rows={}
    for row in overlay['files']:
        key=row['logical'].replace('\\','/').casefold()
        if key in rows:raise ValueError('ambiguous overlay paths')
        rows[key]=row
    source_raw=source_manifest_path.read_bytes() if source_manifest_path else None
    source=json.loads(source_raw) if source_raw is not None else None
    if source and source['guid_paths']!=queue['guid_paths']:raise ValueError('queue and source GUID maps differ')
    asset_guids={x['guid'] for x in source['files'] if x['kind']=='asset'} if source else None
    results=[]
    for guid,path in sorted(queue['guid_paths'].items()):
        p=PurePosixPath(path.replace('\\','/'))
        if p.is_absolute() or '..' in p.parts or not p.parts:raise ValueError('invalid queue path')
        key=p.as_posix().casefold();present=key in rows;meta=key+'.meta' in rows
        results.append({'guid':guid,'source':p.as_posix(),'asset_present':present,'meta_present':meta,
                        'has_asset_record':guid in asset_guids if source else None})
    if queue_path.read_bytes()!=queue_raw or overlay_path.read_bytes()!=overlay_raw:raise ValueError('inputs changed')
    if source and source_manifest_path.read_bytes()!=source_raw:raise ValueError('source manifest changed')
    return {'state':'coverage_only','queue_sha256':hashlib.sha256(queue_raw).hexdigest(),
            'overlay_sha256':hashlib.sha256(overlay_raw).hexdigest(),'count':len(results),
            'source_manifest_sha256':hashlib.sha256(source_raw).hexdigest() if source else None,
            'asset_records':len(asset_guids) if source else None,
            'missing_asset_records':sum(r['has_asset_record'] and not r['asset_present'] for r in results) if source else None,
            'metadata_only_guids':sum(not r['has_asset_record'] for r in results) if source else None,
            'available_with_meta':sum(r['asset_present'] and r['meta_present'] for r in results),
            'missing_assets':sum(not r['asset_present'] for r in results),
            'missing_meta':sum(not r['meta_present'] for r in results),'results':results,
            'limitations':'Path coverage only; GUID contents, material behavior and converter execution not checked.'}


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    for name in ('queue','overlay','output'):p.add_argument('--'+name,type=Path,required=True)
    p.add_argument('--source-manifest',type=Path)
    a=p.parse_args();result=audit(a.queue,a.overlay,a.source_manifest)
    with a.output.open('x',encoding='utf8') as f:json.dump(result,f,ensure_ascii=False,indent=2);f.write('\n')
    print(json.dumps({k:result[k] for k in ('state','count','asset_records','missing_asset_records','metadata_only_guids','available_with_meta','missing_assets','missing_meta')}))
