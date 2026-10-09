"""Measure original null-mask rendering against explicit controls on source mesh.

Copies source closure only; retains unresolved GUIDs and refuses no-draw results.
"""
from pathlib import Path
import ast
import datetime
import hashlib
import json
import math
import os
import re
import shutil
import struct
import subprocess
import sys

HERE=Path(__file__).resolve().parent
W=Path('F:/OpenRPG_ImportWork')
ROOT=W/'unity-source-null-mask-hdrp-v1'
SOURCE=W/'hivemind-run/native-american-village/source'
PROJECT=W/'unity-dungeon-original-shadergraph-hdrp-startup-v4/project'
UNITY=Path('F:/Unity/6000.5.8f1/Editor/Unity.exe')
PS=Path('C:/Users/karen/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell/pwsh.exe')
HELPER=HERE/'candidates/SourceNullMaskHdrpProbe.cs'
BROKER=HERE/'queue_delta_firepit_particle_behavior_v4.py'
PROOF=W/'delta-native-defaults-independent-reopen-v2/RESULT.v2.json'
GUID='abc00000000012210656512993269337'
MISSING='abc00000000001326820419954783426'
def read(p): return json.loads(p.read_text('utf-8-sig'))
def sha(p):
    with p.open('rb') as stream: return hashlib.file_digest(stream,'sha256').hexdigest()
def check(pins):
    for p,digest in pins.items(): assert sha(Path(p))==digest,'Changed source: '+p
def save(name,data):
    p=ROOT/(name+'.tmp');p.write_text(json.dumps(data,indent=2),'utf-8');p.replace(ROOT/name)
def idle():
    assert subprocess.check_output([str(PS),'-NoProfile','-Command','@(Get-Process -Name Unity -ErrorAction SilentlyContinue).Count'],text=True).strip()=='0','Preserve running Unity'

def prepare():
    assert not ROOT.exists(); idle()
    proof=read(PROOF); observation=next(r for r in proof['observations'] if r['material_guid']==GUID)
    assert observation['native_texture']['texture_null'] and observation['native_texture']['default_texture_name']=='white'
    assert observation['graph_declared_texture_reference']['guid']==MISSING and not observation['runtime_binding_qualified']
    ledger_path=W/'nested-prefab-delta-material-source-ir-v1/RESULT.v1.json'
    source=next(r for r in read(ledger_path)['rows'] if r['material_identity']['guid']==GUID)
    use=source['uses'][0]; renderer=use['renderer_identity']
    inventory_path=SOURCE.parent/'source.v1.json'; inventory=read(inventory_path)
    records={str((SOURCE.parent/r['stored_path']).resolve()):r for r in inventory['files']}
    pins={str(p):sha(p) for p in [PROOF,ledger_path,inventory_path,HELPER,BROKER,Path(__file__).resolve(),UNITY]}
    assert sha(Path(source['source_material']['path']))==source['source_material']['sha256']
    ROOT.mkdir(); (ROOT/'temp').mkdir()
    todo=[renderer['asset_path'],source['material_identity']['asset_path']]; seen=set(); copies=[]; unresolved=set()
    while todo:
        relative=todo.pop()
        if relative in seen: continue
        seen.add(relative); original=SOURCE/relative
        assert original.is_file() and original.suffix.lower() not in {'.cs','.dll','.exe','.py','.js'},'Do not execute purchased source scripts'
        for origin in [original,Path(str(original)+'.meta')]:
            record=records[str(origin.resolve())]; digest=sha(origin); assert digest==record['sha256']
            pins[str(origin)]=digest
            target=PROJECT/origin.relative_to(SOURCE); target.parent.mkdir(parents=True,exist_ok=True)
            if target.exists(): assert sha(target)==digest,'Existing private source differs; retain it: '+str(target)
            else: shutil.copyfile(origin,target); copies.append(str(target))
            pins[str(target)]=digest
            if origin.suffix.lower() in {'.prefab','.mat','.shadergraph','.shadersubgraph','.shader','.meta','.asset','.hlsl'}:
                text=origin.read_text('utf-8-sig',errors='strict')
                for guid in set(re.findall(r'(?:guid\s*:\s*|"guid"\s*:\s*")([a-f0-9]{32})',text)):
                    if guid in inventory['guid_paths']:
                        dependency=inventory['guid_paths'][guid]
                        if not (SOURCE/dependency).suffix.lower() in {'.cs','.dll','.exe','.py','.js'}: todo.append(dependency)
                    elif guid!='0'*32: unresolved.add(guid)
    for relative in ['ProjectSettings/GraphicsSettings.asset','ProjectSettings/QualitySettings.asset','ProjectSettings/ProjectSettings.asset','Packages/manifest.json','Packages/packages-lock.json']:
        p=PROJECT/relative; pins[str(p)]=sha(p)
    destination=PROJECT/'Assets/Editor/SourceNullMaskHdrpProbe.cs'; assert not destination.exists()
    shutil.copyfile(HELPER,destination); pins[str(destination)]=sha(destination)
    manifest=dict(output=str(ROOT/'NATIVE.v1.json'),prefab_path=renderer['asset_path'],prefab_guid=renderer['guid'],
        renderer_file_id=renderer['file_id'],node_path=use['node_path'],slot=use['slot_index'],
        material_path=source['material_identity']['asset_path'],material_guid=GUID,shader_guid='53284c37c978c3442b89935f545c724f')
    save('manifest.json',manifest);pins[str(ROOT/'manifest.json')]=sha(ROOT/'manifest.json'); check(pins)
    save('PREPARED.v1.json',dict(input_pins=pins,source_closure_files=len(seen),copied_private_files=copies,
        unresolved_source_guids=sorted(unresolved),missing_authored_guid=MISSING,
        source_geometry_only=True,expected_images=12,comparison_tolerance=1e-5,
        original_source_texture_substituted=False,new_authored_models=False,full_import=False))
    with (ROOT/'controller.log').open('xb') as out:
        child=subprocess.Popen([sys.executable,'-B',str(Path(__file__).resolve()),'run'],cwd=ROOT,
            stdin=subprocess.DEVNULL,stdout=out,stderr=out,creationflags=subprocess.CREATE_NO_WINDOW)
    save('JOB.v1.json',dict(controller_pid=child.pid));print(json.dumps(dict(controller_pid=child.pid,source_files=len(seen),expected_images=12)))

def run():
    state=dict(status='preparing_original_hdrp_null_mask_render',native_default_render_behavior_verified=False,
        whole_prefab_qualified=False,full_import=False);save('RUN-RESULT.v1.json',state)
    broker=None; namespace=None
    try:
        prepared=read(ROOT/'PREPARED.v1.json');check(prepared['input_pins']);idle()
        tree=ast.parse(BROKER.read_text('utf-8-sig'))
        selected=[n for n in tree.body if isinstance(n,(ast.Import,ast.ImportFrom)) or isinstance(n,ast.FunctionDef) and n.name in {'sha','read','write_state','stop_broker','start_broker'}]
        namespace=dict(ROOT=ROOT,W=W,UPM=Path('F:/Unity/6000.5.8f1/Editor/Data/Resources/PackageManager/Server/UnityPackageManager.exe'),LOG_HANDLES=[])
        exec(compile(ast.Module(body=selected,type_ignores=[]),str(BROKER),'exec'),namespace)
        broker,label=namespace['start_broker'](state)
        env=os.environ.copy();env.update(TEMP=str(ROOT/'temp'),TMP=str(ROOT/'temp'),UPM_CACHE_ROOT=str(W/'unity-package-cache'))
        with (ROOT/'unity.stdout.log').open('xb') as out,(ROOT/'unity.stderr.log').open('xb') as err:
            child=subprocess.Popen([str(UNITY),'-batchmode','-force-d3d11','-quit','-upmIpcPath',label,
                '-projectPath',str(PROJECT),'-executeMethod','OpenRPGSourceNullMaskHdrpProbe.Export',
                '--openrpg-manifest',str(ROOT/'manifest.json'),'-logFile',str(ROOT/'unity.log')],cwd=ROOT,env=env,
                stdin=subprocess.DEVNULL,stdout=out,stderr=err,creationflags=subprocess.CREATE_NO_WINDOW)
            state.update(status='rendering_original_mesh_with_null_white_black_controls',unity_pid=child.pid);save('RUN-RESULT.v1.json',state)
            state['unity_exit_code']=child.wait()
        assert state['unity_exit_code']==0,'Original HDRP rendering refused'
        report=read(ROOT/'NATIVE.v1.json'); manifest=read(ROOT/'manifest.json')
        assert report['pipeline']=='UnityEngine.Rendering.HighDefinition.HDRenderPipeline' and report['gpu']=='Direct3D11'
        assert report['source_material_unchanged'] and report['source_renderer_unchanged'] and report['source_mask_null']
        assert report['material_guid']==GUID and report['renderer_file_id']==manifest['renderer_file_id']
        assert report['shader_guid']==manifest['shader_guid'] and report['default_name']=='white'
        assert len(report['rows'])==12 and not report['full_import']; output_pins={str(ROOT/'NATIVE.v1.json'):sha(ROOT/'NATIVE.v1.json')}
        groups={};maximum=0;changed=0
        for row in report['rows']:
            p=Path(row['path']);assert p.resolve().is_relative_to((ROOT/'native-pixels').resolve())
            raw=p.read_bytes();assert len(raw)==8192 and sha(p)==row['sha256']
            values=list(struct.iter_unpack('<4e',raw));assert all(all(math.isfinite(v) for v in pixel) for pixel in values)
            output_pins[str(p)]=sha(p);assert row['variant'] not in groups.setdefault(row['camera'],{})
            groups[row['camera']][row['variant']]=(values,row)
        assert set(groups)=={0,1,2}
        for group in groups.values():
            baseline,baseline_row=group['original_null'];white,white_row=group['explicit_white_control'];black,_=group['explicit_black_control'];again,again_row=group['original_null_repeat']
            assert baseline==again,'Native original render not repeatable'
            assert baseline_row['source_time_after']==white_row['source_time_after']==again_row['source_time_after'],'Source time changed between comparison requests'
            maximum=max(maximum,*(abs(a-b) for p,q in zip(baseline,white,strict=True) for a,b in zip(p,q,strict=True)))
            changed+=sum(p!=q for p,q in zip(baseline,black,strict=True))
        save('COMPARISON.v1.json',dict(input_pins=prepared['input_pins'],output_pins=output_pins,
            maximum_null_white_error=maximum,black_control_changed_pixels=changed,tolerance=prepared['comparison_tolerance'],
            pixels_reopened=12288,original_source_texture_substituted=False,whole_prefab_qualified=False,full_import=False))
        assert maximum<=prepared['comparison_tolerance'] and changed>0,'Default mapping unqualified/no source-mask coverage; keep comparison'
        check(prepared['input_pins']);check(output_pins)
        state.update(status='original_null_mask_render_matches_white_and_refuses_black_control',native_default_render_behavior_verified=True,
            images_verified=12,pixels_verified=12288,maximum_null_white_error=maximum,black_control_changed_pixels=changed,
            comparison_receipt_sha256=sha(ROOT/'COMPARISON.v1.json'))
    except Exception as error:
        import traceback
        state.update(status='refused_or_unverified',error=type(error).__name__+': '+str(error),diagnostic_traceback=traceback.format_exc())
    finally:
        if broker is not None: namespace['stop_broker'](broker,state)
    state['finished_utc']=datetime.datetime.now(datetime.timezone.utc).isoformat();save('RUN-RESULT.v1.json',state)

if __name__=='__main__': run() if len(sys.argv)>1 and sys.argv[1]=='run' else prepare()
