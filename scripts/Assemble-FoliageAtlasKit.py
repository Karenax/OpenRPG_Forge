"""Assemble a separate, unpromoted kit from validated LOD derivatives."""
import argparse
import hashlib
import json
from pathlib import Path


def sha(data):
    return hashlib.sha256(data).hexdigest()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--source-kit',type=Path,required=True)
    parser.add_argument('--derivation',type=Path,action='append',required=True)
    parser.add_argument('--output-dir',type=Path,required=True)
    args = parser.parse_args()
    source, output = args.source_kit.resolve(), args.output_dir.resolve()
    if output.exists() or output.drive.lower() != 'e:' or output.is_relative_to(source):
        raise ValueError('fresh E: output outside source required')
    inputs = {name:(source/name).read_bytes() for name in ('build.json','preview.json','prototype-catalog.json')}
    build, preview, catalog = [json.loads(inputs[n]) for n in ('build.json','preview.json','prototype-catalog.json')]
    if not all(v['asset_kit_id']==build['asset_kit_id'] and v['asset_kit_revision']==build['asset_kit_revision']
               for v in (preview,catalog)):
        raise ValueError('source kit mismatch')
    authored = set(build.get('authored_source',{}).get('assets',[]))
    derivatives, proofs = {}, []
    for path in args.derivation:
        data = path.read_bytes()
        record = json.loads(data)
        asset_id = record['asset_id']
        if asset_id in derivatives or asset_id in authored or Path(record['glb']).name != record['glb']:
            raise ValueError('duplicate/authored/invalid derivative')
        glb = (path.parent/record['glb']).read_bytes()
        validation_bytes = (path.parent/'validation.khronos.json').read_bytes()
        validation = json.loads(validation_bytes)
        if sha(glb) != record['glb_sha256'] or validation['glb_sha256'] != sha(glb) or validation['report']['issues']['numErrors']:
            raise ValueError('derivative validation mismatch')
        derivatives[asset_id] = (record,glb)
        proofs.append({'asset_id':asset_id,'derivation_sha256':sha(data),'validation_sha256':sha(validation_bytes)})
    files, replaced = {}, set()
    for asset in build['assets']:
        relative = Path(asset['glb']['path'])
        path = (source/relative).resolve()
        if not path.is_relative_to(source) or path.is_symlink(): raise ValueError('source path escapes kit')
        data = path.read_bytes()
        if sha(data) != asset['glb']['sha256']: raise ValueError('source GLB hash mismatch')
        inputs[str(relative)] = data
        if asset['id'] in derivatives:
            record, derived = derivatives[asset['id']]
            if record['source_sha256'] != sha(data): raise ValueError('derivative belongs to another source')
            asset['glb']['sha256'] = sha(derived)
            for binding in catalog['bindings']:
                if binding['asset_id'] == asset['id']:
                    if binding['glb_sha256'] != sha(data): raise ValueError('binding source hash mismatch')
                    binding['glb_sha256'] = sha(derived)
            data = derived
            replaced.add(asset['id'])
        files[str(relative)] = data
    if replaced != set(derivatives): raise ValueError('derivative asset absent from kit')
    identity = sha(''.join(sorted(p['derivation_sha256'] for p in proofs)).encode())[:12]
    identifier = build['asset_kit_id']+'-cluster-atlas-lod1-'+identity
    for item in (build,preview,catalog):
        item['asset_kit_id'], item['asset_kit_revision'] = identifier, 1
    evidence = {'schema':'forge.foliage-atlas-kit-candidate','schema_version':1,
        'script_sha256':sha(Path(__file__).read_bytes()),'sources':{n:sha(b) for n,b in inputs.items()},
        'derivatives':proofs,'runtime_promoted':False,'human_accepted':False,
        'limits':['non-mipmapped sampler candidate','visual transitions and runtime performance unqualified']}
    encoded_evidence = (json.dumps(evidence,indent=2)+'\n').encode()
    build['candidate_evidence'] = {'base_report_sha256':sha(inputs['build.json']),
        'derivative_proof_sha256':sha(encoded_evidence),'scope':'Only tree LOD1 foliage; not promoted'}
    build['generator']['assembly_script_sha256'] = evidence['script_sha256']
    build['qualification'] = {'technical_export_validated':True,'human_art_review':'pending',
                              'publication_approved':False,'r1_063_complete':False}
    for name,data in inputs.items():
        if (source/name).read_bytes() != data: raise ValueError('source changed during assembly')
    output.mkdir(parents=True,exist_ok=False)
    for name,data in files.items():
        target = output/name
        target.parent.mkdir(parents=True,exist_ok=True)
        target.write_bytes(data)
    for name,item in (('build.json',build),('preview.json',preview),('prototype-catalog.json',catalog)):
        (output/name).write_text(json.dumps(item,indent=2)+'\n',encoding='utf-8')
    (output/'atlas-kit-proof.json').write_bytes(encoded_evidence)
    print(f'ATLAS_KIT_ASSEMBLED replaced={len(replaced)} preserved={len(files)-len(replaced)} unpromoted=true')


if __name__ == '__main__':
    main()
