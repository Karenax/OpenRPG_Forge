"""Native normal-loop AOV capture with retained source closure and prior refusals."""
from pathlib import Path
import ast
import datetime
import hashlib
import importlib.util
import json
import math
import os
import shutil
import struct
import subprocess
import sys

HERE = Path(__file__).resolve().parent
W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'detail-native-normal-loop-aov-v15'
PROJECT = W / 'unity-dungeon-original-shadergraph-hdrp-startup-v4/project'
UNITY = Path('F:/Unity/6000.5.8f1/Editor/Unity.exe')
HELPER = HERE / 'candidates/SourceDetailAovV15Probe.cs'
BROKER = HERE / 'queue_delta_firepit_particle_behavior_v4.py'

def read(p): return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as stream: return hashlib.file_digest(stream,'sha256').hexdigest()
def check(pins):
    for p,digest in pins.items(): assert sha(p)==digest, 'Changed pinned file: '+p
def save(name,data):
    temp=ROOT/(name+'.tmp');temp.write_text(json.dumps(data,indent=2),'utf-8');temp.replace(ROOT/name)
def idle():
    ps='C:/Users/karen/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell/pwsh.exe'
    assert subprocess.check_output([ps,'-NoProfile','-Command','@(Get-Process -Name Unity -ErrorAction SilentlyContinue).Count'],text=True).strip()=='0'

def prepare():
    assert not ROOT.exists();idle()
    prior=W/'detail-default-native-gain-v11/PREPARED.v1.json'
    pins=read(prior)['input_pins']
    repair_path=W/'detail-aov-private-input-compatibility-v1/REPAIR.v1.json';repair=read(repair_path)
    assert pins[repair['project_config']]==repair['before_sha256'] and sha(repair['before'])==repair['before_sha256']
    assert sha(repair['project_config'])==repair['after_sha256']
    pins[repair['project_config']]=repair['after_sha256']
    pins[str(repair_path)]=sha(repair_path);pins[repair['before']]=repair['before_sha256'];pins[repair['after']]=repair['after_sha256']
    context_path=W/'detail-aov-private-material-context-v4/RESULT.v1.json';context=read(context_path)
    assert context['status']=='target_metadata_byte_proven_other_private_material_context_retained'
    assert not context['original_full_scene_context_qualified']
    for row in context['rows']:
        assert pins[row['path']]==row['before_sha256'] and sha(row['before'])==row['before_sha256']
        assert sha(row['path'])==sha(row['after'])==row['after_sha256']
        pins[row['path']]=row['after_sha256'];pins[row['before']]=row['before_sha256'];pins[row['after']]=row['after_sha256']
    pins[str(context_path)]=sha(context_path)
    check(pins)
    ROOT.mkdir();(ROOT/'temp').mkdir()
    destination=PROJECT/'Assets/Editor/SourceDetailAovV15Probe.cs'
    assert not destination.exists();shutil.copyfile(HELPER,destination)
    pins.update({str(p):sha(p) for p in [prior,HELPER,destination,Path(__file__).resolve(),BROKER]})
    sdk=PROJECT/'Library/PackageCache/com.unity.render-pipelines.high-definition@fc55a8abadc2'
    for rel in ['Runtime/RenderPipeline/HDRenderPipeline.cs','Runtime/RenderPipeline/RenderPass/AOV/AOVRequest.cs',
                'Runtime/RenderPipeline/RenderPass/AOV/AOVRequestBuilder.cs','Documentation~/AOVs.md']:
        p=sdk/rel;pins[str(p)]=sha(p)
    manifest=read(W/'detail-default-native-gain-v11/manifest.json');manifest['output']=str(ROOT/'NATIVE.v1.json')
    save('manifest.json',manifest);pins[str(ROOT/'manifest.json')]=sha(ROOT/'manifest.json')
    save('PREPARED.v1.json',dict(input_pins=pins,expected_images=36,normal_native_render_loop=True,
                               float_aov_override=True,source_geometry_only=True,full_import=False))
    with (ROOT/'controller.log').open('xb') as out:
        child=subprocess.Popen([sys.executable,'-B',str(Path(__file__).resolve()),'run'],cwd=ROOT,
                               stdin=subprocess.DEVNULL,stdout=out,stderr=out,creationflags=subprocess.CREATE_NO_WINDOW)
    save('JOB.v1.json',dict(controller_pid=child.pid));print(json.dumps(dict(controller_pid=child.pid,root=str(ROOT))))

def reopen(prepared):
    report=read(ROOT/'NATIVE.v1.json')
    assert report['gpu']=='Direct3D11' and report['pipeline']=='UnityEngine.Rendering.HighDefinition.HDRenderPipeline'
    assert report['source_material_unchanged'] and report['source_renderer_unchanged']
    assert report['active_editor_scene_preserved'] and report['editor_play_options_restored']
    assert report['targeted_submesh_indices']>0 and len(report['rows'])==36 and not report['full_import']
    manifest=read(ROOT/'manifest.json')
    for key in ['material_guid','renderer_file_id','shader_guid']:assert report[key]==manifest[key]
    groups={};outputs={str(ROOT/'NATIVE.v1.json'):sha(ROOT/'NATIVE.v1.json')}
    for row in report['rows']:
        p=Path(row['path']);assert p.resolve().is_relative_to((ROOT/'native-pixels').resolve())
        assert row['width']==row['height']==128 and row['effective_hd_camera_time']==0 and row['native_aov_callback']
        assert row['graphics_format']=='R16G16B16A16_SFloat'
        assert row['diagnostic_clone_roughness']==.125 and row['diagnostic_clone_ao_intensity']==.25
        assert row['aov_property']==('Smoothness' if row['camera']<3 else 'AmbientOcclusion')
        raw=p.read_bytes();assert len(raw)==131072 and sha(p)==row['sha256']
        values=list(struct.iter_unpack('<4e',raw));assert all(all(math.isfinite(x) for x in pixel) for pixel in values)
        assert row['variant'] not in groups.setdefault(row['camera'],{})
        groups[row['camera']][row['variant']]=values;outputs[str(p)]=sha(p)
    assert set(groups)==set(range(6))
    positives={};measurements=[]
    for view,group in groups.items():
        null=group['original_null'];assert null==group['original_null_repeat']
        assert null==group['explicit_white_control'],'Null differs from white'
        black=group['explicit_black_control'];expected=.875 if view<3 else .25;black_expected=1 if view<3 else 0
        count=0
        for a,b in zip(null,black,strict=True):
            if a==b:continue
            assert a[:3]==(expected,)*3 and b[:3]==(black_expected,)*3,'AOV response differs from linear graph transfer'
            assert a[3]==b[3]==1
            value=(1-a[0])/.125 if view<3 else a[0]/.25
            assert value==1,'Measured default differs'
            measurements.append(dict(channel='g' if view<3 else 'r',value=value,view=view))
            count+=1
        assert count>=32,'Insufficient original submesh coverage'
        positives[str(view)]=count
    check(prepared['input_pins']);check(outputs)
    packet=ROOT/'native-default-mask.rg.f32';assert not packet.exists();packet.write_bytes(struct.pack('<2f',1.,1.))
    save('INDEPENDENT-REOPEN.v1.json',dict(status='native_linear_aov_default_rg_measured_and_reopened',
         input_pins=prepared['input_pins'],output_pins=outputs,positive_pixels_by_view=positives,
         measured_rg=[1.,1.],packet=str(packet),packet_sha256=sha(packet),
         native_default_rg_qualified=True,lit_render_qualified=False,whole_prefab_qualified=False,
         full_import=False,measurements=measurements))

def run():
    state=dict(status='preparing_normal_loop_aov_capture',full_import=False,native_default_rg_qualified=False)
    save('RUN-RESULT.v1.json',state);broker=None;ns=None
    try:
        prepared=read(ROOT/'PREPARED.v1.json');check(prepared['input_pins']);idle()
        tree=ast.parse(BROKER.read_text('utf-8-sig'))
        nodes=[n for n in tree.body if isinstance(n,(ast.Import,ast.ImportFrom)) or isinstance(n,ast.FunctionDef) and n.name in {'sha','read','write_state','stop_broker','start_broker'}]
        ns=dict(ROOT=ROOT,W=W,UPM=Path('F:/Unity/6000.5.8f1/Editor/Data/Resources/PackageManager/Server/UnityPackageManager.exe'),LOG_HANDLES=[])
        exec(compile(ast.Module(body=nodes,type_ignores=[]),str(BROKER),'exec'),ns);broker,label=ns['start_broker'](state)
        env=os.environ.copy();env.update(TEMP=str(ROOT/'temp'),TMP=str(ROOT/'temp'),UPM_CACHE_ROOT=str(W/'unity-package-cache'))
        with (ROOT/'unity.stdout.log').open('xb') as out,(ROOT/'unity.stderr.log').open('xb') as err:
            child=subprocess.Popen([str(UNITY),'-batchmode','-force-d3d11','-upmIpcPath',label,'-projectPath',str(PROJECT),
                '-executeMethod','OpenRPGSourceDetailAovV15Probe.Export','--openrpg-manifest',str(ROOT/'manifest.json'),
                '-logFile',str(ROOT/'unity.log')],cwd=ROOT,env=env,stdin=subprocess.DEVNULL,stdout=out,stderr=err,creationflags=subprocess.CREATE_NO_WINDOW)
            state.update(status='native_editor_running_normal_loop_aov',unity_pid=child.pid);save('RUN-RESULT.v1.json',state)
            state['unity_exit_code']=child.wait()
        assert state['unity_exit_code']==0,'Native AOV capture refused; inspect preserved diagnostics'
        reopen(prepared);state.update(status='native_linear_aov_capture_and_reopen_passed',native_default_rg_qualified=True)
    except Exception as error:
        import traceback
        state.update(status='refused_or_unverified',error=repr(error),traceback=traceback.format_exc())
    finally:
        if broker is not None:ns['stop_broker'](broker,state)
        repair=read(W/'detail-aov-private-input-compatibility-v1/REPAIR.v1.json')
        config=Path(repair['project_config'])
        if sha(config)==repair['after_sha256'] and sha(repair['before'])==repair['before_sha256']:
            config.write_bytes(Path(repair['before']).read_bytes())
            state['private_input_configuration_restored']=sha(config)==repair['before_sha256']
        else:
            state.update(private_input_configuration_restored=False,configuration_restore_refused='Unexpected private configuration bytes; preserve them')
    state['finished_utc']=datetime.datetime.now(datetime.timezone.utc).isoformat();save('RUN-RESULT.v1.json',state)

if __name__=='__main__':run() if len(sys.argv)>1 and sys.argv[1]=='run' else prepare()
