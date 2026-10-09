"""Capture the two real Detail textures with the previously verified exporter."""
from pathlib import Path
import ast
import ctypes
from ctypes import wintypes
import datetime
import hashlib
import json
import os
import re
import runpy
import subprocess
import sys
import time

HERE=Path(__file__).resolve().parent
W=Path('F:/OpenRPG_ImportWork');ROOT=W/'detail-native-texture-storage-v1'
OLD=W/'unity-villa-texture-source-probe-linear-v2';PROJECT=OLD/'project'
SOURCE=W/'hivemind-run/native-american-village/source'
UPSTREAM=W/'detail-native-normal-loop-aov-v14'
PS='C:/Users/karen/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell/pwsh.exe'
def read(p):return json.loads(Path(p).read_bytes())
def sha(p):
    with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
def save(name,d):
    p=ROOT/name;t=ROOT/(name+'.tmp');t.write_text(json.dumps(d,indent=2),'utf-8');t.replace(p)
def check(pins):
    for p,h in pins.items():assert sha(p)==h,p
def prepare():
    assert not ROOT.exists()
    kernel_path=W/'detail-native-default-kernel-v1/RESULT.v1.json';kernel=read(kernel_path)
    inventory=read(SOURCE.parent/'source.v1.json')
    assert kernel['actual_naga_exit_code']==0 and len(kernel['source_texture_bindings'])==2
    prior_path=W/'unity-delta-foliage-texture-storage-v2/PREPARED.v1.json';prior=read(prior_path)
    helper=Path(prior['helper']);verifier=OLD/'verify_texture_export.py'
    assert sha(helper)==prior['exporter_sha256'] and sha(verifier)==prior['verifier_sha256']
    original_mat=SOURCE/inventory['guid_paths'][kernel['material_guid']]
    mat_text=original_mat.read_text('utf-8-sig')
    pins={str(p):sha(p) for p in [kernel_path,prior_path,helper,verifier,original_mat,Path(__file__).resolve()]}
    ROOT.mkdir();(ROOT/'temp').mkdir();requests=[];copies=[]
    for binding in kernel['source_texture_bindings']:
        image,meta=[Path(x['path']) for x in binding['source_files']];guid=binding['guid']
        assert image.resolve().is_relative_to(SOURCE.resolve()) and meta==Path(str(image)+'.meta')
        for item in binding['source_files']:assert sha(item['path'])==item['sha256'];pins[item['path']]=item['sha256']
        assert re.search(r'^guid:\s*'+guid+r'\s*$',meta.read_text('utf-8-sig'),re.M)
        ids=set(re.findall(r'm_Texture:\s*\{fileID:\s*(-?\d+),\s*guid:\s*'+guid+r',\s*type:\s*3\}',mat_text));assert len(ids)==1
        relative=Path('Assets/DetailOriginalTextures')/guid/image.name;target=PROJECT/relative
        assert not target.exists() and not Path(str(target)+'.meta').exists();target.parent.mkdir(parents=True)
        for source,dest in [(image,target),(meta,Path(str(target)+'.meta'))]:
            with dest.open('xb') as f:f.write(source.read_bytes())
            assert sha(source)==sha(dest);copies.append(dict(source=str(source),copy=str(dest),sha256=sha(source),is_meta=source==meta))
        requests.append(dict(asset_id='hivemind.native-american-village.texture-'+guid,
                             asset_path=relative.as_posix(),expected_guid=guid,expected_file_id=ids.pop(),expected_name=image.stem))
    save('manifest.json',dict(schema_version=1,output_root=str(ROOT/'export'),cases=requests))
    pins[str(ROOT/'manifest.json')]=sha(ROOT/'manifest.json')
    # The separate texture project is not used by the active HDRP capture.
    save('PREPARED.v1.json',dict(input_pins=pins,copies=copies,helper=str(helper),verifier=str(verifier),
                               project=str(PROJECT),upstream=str(UPSTREAM),full_import=False,expected_textures=2))
    with (ROOT/'controller.log').open('xb') as log:
        child=subprocess.Popen([sys.executable,'-B',str(Path(__file__).resolve()),'run'],cwd=ROOT,
            stdin=subprocess.DEVNULL,stdout=log,stderr=log,creationflags=subprocess.CREATE_NO_WINDOW)
    save('JOB.v1.json',dict(controller_pid=child.pid));print(json.dumps(dict(controller_pid=child.pid,expected_textures=2,waiting_for_owned_aov_job=True)))
def run():
    state=dict(status='waiting_for_owned_aov_controller',full_import=False,normal_decode_qualified=False,gpu_sampling_qualified=False)
    save('RUN-RESULT.v1.json',state);handle=None;broker=None;ns=None
    try:
        prepared=read(ROOT/'PREPARED.v1.json');check(prepared['input_pins'])
        native=read(UPSTREAM/'RUN-RESULT.v1.json')
        if not native.get('finished_utc'):
            kernel=ctypes.WinDLL('kernel32',use_last_error=True)
            kernel.OpenProcess.argtypes=[wintypes.DWORD,wintypes.BOOL,wintypes.DWORD];kernel.OpenProcess.restype=wintypes.HANDLE
            kernel.WaitForSingleObject.argtypes=[wintypes.HANDLE,wintypes.DWORD];kernel.CloseHandle.argtypes=[wintypes.HANDLE]
            pid=read(UPSTREAM/'JOB.v1.json')['controller_pid'];handle=kernel.OpenProcess(0x100000,False,pid)
            assert handle,'Upstream absent without terminal receipt'
            while not native.get('finished_utc'):
                assert kernel.WaitForSingleObject(handle,0)==258,'Upstream exited without terminal receipt'
                time.sleep(3);native=read(UPSTREAM/'RUN-RESULT.v1.json')
        state['upstream_terminal_status']=native['status']
        assert subprocess.check_output([PS,'-NoProfile','-Command','@(Get-Process -Name Unity -ErrorAction SilentlyContinue).Count'],text=True).strip()=='0'
        check(prepared['input_pins'])
        for copy in prepared['copies']:assert sha(copy['source'])==sha(copy['copy'])==copy['sha256']
        broker_source=HERE/'queue_delta_firepit_particle_behavior_v4.py';tree=ast.parse(broker_source.read_text('utf-8-sig'))
        nodes=[n for n in tree.body if isinstance(n,(ast.Import,ast.ImportFrom)) or isinstance(n,ast.FunctionDef) and n.name in {'sha','read','write_state','stop_broker','start_broker'}]
        ns=dict(ROOT=ROOT,W=W,UPM=Path('F:/Unity/6000.5.8f1/Editor/Data/Resources/PackageManager/Server/UnityPackageManager.exe'),LOG_HANDLES=[])
        exec(compile(ast.Module(body=nodes,type_ignores=[]),str(broker_source),'exec'),ns);broker,label=ns['start_broker'](state)
        env=os.environ.copy();env.update(TEMP=str(ROOT/'temp'),TMP=str(ROOT/'temp'),UPM_CACHE_ROOT=str(W/'unity-package-cache'))
        with (ROOT/'unity.stdout.log').open('xb') as out,(ROOT/'unity.stderr.log').open('xb') as err:
            child=subprocess.Popen(['F:/Unity/6000.5.8f1/Editor/Unity.exe','-batchmode','-force-d3d11','-quit','-upmIpcPath',label,
                '-projectPath',str(PROJECT),'-executeMethod','OpenRPGDeltaFoliageTexturesV1.TextureSourceSnapshot.Export',
                '--openrpg-manifest',str(ROOT/'manifest.json'),'-logFile',str(ROOT/'unity.log')],cwd=ROOT,env=env,
                stdin=subprocess.DEVNULL,stdout=out,stderr=err,creationflags=subprocess.CREATE_NO_WINDOW)
            state.update(status='capturing_two_original_detail_texture_storages',unity_pid=child.pid);save('RUN-RESULT.v1.json',state);state['unity_exit_code']=child.wait()
        assert state['unity_exit_code']==0
        report=read(ROOT/'export/RESULT.json');manifest=read(ROOT/'manifest.json')
        assert report['total']==report['matched']==len(report['cases'])==2 and report['failed']==0
        assert report['color_space']=='Linear' and report['graphics_device_type']=='Direct3D11'
        verifier=runpy.run_path(prepared['verifier'],run_name='retained_texture_verifier')
        rows=[verifier['check_case'](request,row,ROOT/'export'/f'{i:03}') for i,(request,row) in enumerate(zip(manifest['cases'],report['cases'],strict=True))]
        differences=[]
        for copy in prepared['copies']:
            assert sha(copy['source'])==copy['sha256']
            if sha(copy['copy'])!=copy['sha256']:
                assert copy['is_meta'],'Private image changed'
                differences.append(dict(path=copy['copy'],before_sha256=copy['sha256'],after_sha256=sha(copy['copy'])))
        check(prepared['input_pins'])
        save('STORAGE-REOPEN.v1.json',dict(rows=rows,textures_verified=2,source_originals_unchanged=True,
            private_meta_differences=differences,importer_parity_qualified=False,normal_decode_qualified=False,
            gpu_sampling_qualified=False,native_result_sha256=sha(ROOT/'export/RESULT.json'),full_import=False))
        state.update(status='two_detail_native_texture_storages_reopened',textures_verified=2,mips_verified=sum(row['mip_count'] for row in rows))
    except Exception as error:
        import traceback
        state.update(status='refused_or_unverified',error=repr(error),traceback=traceback.format_exc())
    finally:
        if handle:kernel.CloseHandle(handle)
        if broker is not None:ns['stop_broker'](broker,state)
    state['finished_utc']=datetime.datetime.now(datetime.timezone.utc).isoformat();save('RUN-RESULT.v1.json',state)
if __name__=='__main__':run() if len(sys.argv)>1 and sys.argv[1]=='run' else prepare()
