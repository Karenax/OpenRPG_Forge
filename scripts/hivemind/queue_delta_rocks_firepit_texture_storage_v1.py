"""Capture ten new original texture identities using the verified native exporter."""
from pathlib import Path
import datetime
import importlib.util
import json
import os
import re
import runpy
import subprocess
import sys

HERE = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location('storage', HERE/'queue_delta_foliage_texture_storage_v1.py')
common = importlib.util.module_from_spec(spec)
spec.loader.exec_module(common)
W = common.W
ROOT = W/'unity-delta-rocks-firepit-texture-storage-v1'
common.ROOT = ROOT
PROJECT = common.PROJECT
SOURCE = common.SOURCE
WANTED = {'abc00000000014678987794877247210', 'abc00000000009294800760372203426'}

def idle():
    assert common.ps('@(Get-Process -Name Unity -ErrorAction SilentlyContinue).Count')=='0'

def prepare():
    assert not ROOT.exists()
    idle()
    kernels_path=W/'nested-prefab-delta-material-wgsl-v1/RESULT.v1.json'
    source_manifest=SOURCE.parent/'source.v1.json'
    inventory=common.read(source_manifest)
    records={str((SOURCE.parent/r['stored_path']).resolve()):r for r in inventory['files']}
    prior=W/'unity-delta-foliage-texture-storage-v2'
    prior_prepared=common.read(prior/'PREPARED.v1.json')
    prior_result=common.read(prior/'RUN-RESULT.v1.json')
    assert prior_result['textures_verified']==18 and prior_result['unity_exit_code']==0
    already={r['expected_guid'] for r in common.read(prior/'manifest.json')['cases']}
    helper=Path(prior_prepared['helper'])
    verifier=common.OLD/'verify_texture_export.py'
    assert common.sha(helper)==prior_prepared['exporter_sha256']
    assert common.sha(verifier)==prior_prepared['verifier_sha256']
    pins={str(p):common.sha(p) for p in [Path(__file__),HERE/'queue_delta_foliage_texture_storage_v1.py',
        kernels_path,source_manifest,helper,verifier,prior/'PREPARED.v1.json',prior/'RUN-RESULT.v1.json']}
    absent_configuration_files=[]
    for relative in ['ProjectSettings/ProjectSettings.asset','ProjectSettings/GraphicsSettings.asset',
                     'ProjectSettings/QualitySettings.asset','Packages/manifest.json','Packages/packages-lock.json']:
        path=PROJECT/relative
        if path.is_file():
            pins[str(path)]=common.sha(path)
        else:
            assert relative=='Packages/packages-lock.json'
            absent_configuration_files.append(str(path))
    textures={}; bindings=[]
    for row in common.read(kernels_path)['rows']:
        if row['material_guid'] not in WANTED: continue
        assert row['actual_naga_exit_code']==0
        mat=SOURCE/inventory['guid_paths'][row['material_guid']]
        assert common.sha(mat)==records[str(mat.resolve())]['sha256']
        pins[str(mat)]=common.sha(mat)
        text=mat.read_text('utf-8-sig')
        for binding in row['source_texture_bindings']:
            guid=binding['guid']; assert guid not in already, 'Reuse verified capture instead of repeating it'
            image,meta=[Path(r['path']) for r in binding['source_files']]
            assert image.suffix.lower()=='.png' and meta==Path(str(image)+'.meta')
            assert image.resolve().is_relative_to(SOURCE.resolve())
            for entry,path in zip(binding['source_files'],[image,meta]):
                assert common.sha(path)==entry['sha256']==records[str(path.resolve())]['sha256']
                pins[str(path)]=entry['sha256']
            assert re.search(r'^guid:\s*'+guid+r'\s*$',meta.read_text('utf-8-sig'),re.M)
            ids=set(re.findall(r'm_Texture:\s*\{fileID:\s*(-?\d+),\s*guid:\s*'+guid+r',\s*type:\s*3\}',text))
            assert len(ids)==1
            item=dict(image=str(image),meta=str(meta),file_id=ids.pop())
            assert guid not in textures or textures[guid]==item
            textures[guid]=item
            bindings.append(dict(material_guid=row['material_guid'],texture_guid=guid,
                requires_normal_decode=binding['requires_normal_decode']))
    assert len(textures)==len(bindings)==10
    existing=set()
    for path in (PROJECT/'Assets').rglob('*.meta'):
        match=re.search(r'^guid:\s*([0-9a-f]{32})\s*$',path.read_text('utf-8-sig'),re.M)
        if match: existing.add(match[1])
    assert not existing.intersection(textures), 'Preserve existing project GUIDs'
    common.check(pins)
    ROOT.mkdir(); requests=[]; copies=[]
    for guid,row in sorted(textures.items()):
        image,meta=Path(row['image']),Path(row['meta'])
        relative=Path('Assets/DeltaRocksFirePitTextures')/guid/image.name
        target=PROJECT/relative
        assert not target.exists() and not Path(str(target)+'.meta').exists()
        target.parent.mkdir(parents=True)
        for original,dest in [(image,target),(meta,Path(str(target)+'.meta'))]:
            with dest.open('xb') as stream: stream.write(original.read_bytes())
            assert common.sha(dest)==pins[str(original)]
            pins[str(dest)]=common.sha(dest)
            copies.append(dict(original=str(original),copied=str(dest),sha256=common.sha(dest)))
        requests.append(dict(asset_id='hivemind.native-american-village.texture-'+guid,
            asset_path=relative.as_posix(),expected_guid=guid,expected_file_id=row['file_id'],expected_name=image.stem))
    common.save('manifest.json',dict(schema_version=1,output_root=str(ROOT/'export'),cases=requests))
    pins[str(ROOT/'manifest.json')]=common.sha(ROOT/'manifest.json')
    common.save('PREPARED.v1.json',dict(project=str(PROJECT),input_pins=pins,copies=copies,
        bindings=bindings,texture_cases=10,original_materials=2,helper=str(helper),verifier=str(verifier),
        absent_configuration_files=absent_configuration_files,full_import=False))
    with (ROOT/'controller.log').open('xb') as log:
        child=subprocess.Popen([sys.executable,'-B',str(Path(__file__).resolve()),'run'],cwd=ROOT,
            stdin=subprocess.DEVNULL,stdout=log,stderr=log,creationflags=subprocess.CREATE_NO_WINDOW)
    common.save('JOB.v1.json',dict(controller_pid=child.pid,created_utc=datetime.datetime.now(datetime.timezone.utc).isoformat()))
    print(json.dumps(dict(controller_pid=child.pid,new_textures=10,source_originals_preserved=True)))

def run():
    state=dict(status='capturing_ten_native_rocks_firepit_textures',textures_verified=0,
        gpu_sampling_qualified=False,normal_decode_qualified=False,studio_verified=False,full_import=False)
    common.save('RUN-RESULT.v1.json',state)
    try:
        prepared=common.read(ROOT/'PREPARED.v1.json')
        common.check(prepared['input_pins']); idle()
        assert all(not Path(p).exists() for p in prepared['absent_configuration_files'])
        env=os.environ.copy();env.update(TEMP=str(ROOT),TMP=str(ROOT),UPM_CACHE_ROOT=str(W/'unity-package-cache'))
        with (ROOT/'unity.stdout.log').open('xb') as out,(ROOT/'unity.stderr.log').open('xb') as err:
            child=subprocess.Popen(['F:/Unity/6000.5.8f1/Editor/Unity.exe','-batchmode','-force-d3d11','-quit',
                '-projectPath',prepared['project'],'-executeMethod','OpenRPGDeltaFoliageTexturesV1.TextureSourceSnapshot.Export',
                '--openrpg-manifest',str(ROOT/'manifest.json'),'-logFile',str(ROOT/'unity.log')],cwd=ROOT,env=env,
                stdin=subprocess.DEVNULL,stdout=out,stderr=err,creationflags=subprocess.CREATE_NO_WINDOW)
            state['unity_pid']=child.pid;common.save('RUN-RESULT.v1.json',state)
            state['unity_exit_code']=child.wait()
        assert state['unity_exit_code']==0, 'Native texture capture refused; retain logs and partial exports'
        native=common.read(ROOT/'export/RESULT.json');manifest=common.read(ROOT/'manifest.json')
        assert native['unity_version']=='6000.5.8f1' and native['color_space']=='Linear'
        assert native['graphics_device_type']=='Direct3D11'
        assert native['total']==native['matched']==len(native['cases'])==10 and native['failed']==0
        verifier=runpy.run_path(prepared['verifier'],run_name='trusted_texture_verifier')
        rows=[verifier['check_case'](request,row,ROOT/'export'/f'{index:03}')
              for index,(request,row) in enumerate(zip(manifest['cases'],native['cases']))]
        # Private meta readability changes are measured separately; original identities remain strict.
        private_meta={row['copied']:row for row in prepared['copies'] if row['copied'].endswith('.meta')}
        differences=[]
        for path,digest in prepared['input_pins'].items():
            observed=common.sha(path)
            if observed!=digest:
                assert path in private_meta, 'Unexpected source/helper/configuration change: '+path
                differences.append(dict(path=path,before_sha256=digest,after_sha256=observed))
            else: assert observed==digest
        common.save('STORAGE-REOPEN.v1.json',dict(rows=rows,textures_verified=10,
            source_originals_unchanged=True,native_result_sha256=common.sha(ROOT/'export/RESULT.json'),
            private_meta_serialization_differences=differences,full_import=False,
            normal_decode_qualified=False,gpu_sampling_qualified=False))
        state.update(status='ten_native_rocks_firepit_texture_storage_captures_reopened',textures_verified=10,
            mips_verified=sum(r['mip_count'] for r in rows),native_result_sha256=common.sha(ROOT/'export/RESULT.json'),
            private_meta_serialization_differences=differences,independent_reopen_still_required=True)
    except Exception as error:
        state.update(status='refused_or_unverified',error=type(error).__name__+': '+str(error))
    state['finished_utc']=datetime.datetime.now(datetime.timezone.utc).isoformat()
    common.save('RUN-RESULT.v1.json',state)

if __name__=='__main__': run() if len(sys.argv)>1 and sys.argv[1]=='run' else prepare()
