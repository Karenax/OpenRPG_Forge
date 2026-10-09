"""Instrument the exact current replay, preserving all pinned source inputs."""
from pathlib import Path
from datetime import datetime,timezone
import importlib.util,json,subprocess,sys

S=Path(__file__).parent
BASE=S/'check_source_scene_actual_device_mount_v1.py'
spec=importlib.util.spec_from_file_location('point_probe_runner',BASE)
runner=importlib.util.module_from_spec(spec);spec.loader.exec_module(runner)
W=runner.W;ROOT=W/'firepit-point-shadow-diagnostics-native-v1'
OLD=W/'firepit-source-receiver-coordinates-native-v3';runner.ROOT=ROOT

def prepare():
    assert not ROOT.exists()
    prep=runner.read(OLD/'PREPARED.v1.json');run=runner.read(OLD/'RUN-RESULT.v1.json');assert run['actual_exit_code']==0
    for p,h in prep['input_pins'].items():assert runner.sha(p)==h,p
    rust=(OLD/'src/main.rs').read_text('utf-8')
    diagnostic=S/'candidates/SourcePointShadowDiagnosticV1.rs'
    view_probe=S/'candidates/SourceShadowCasterViewReadbackV1.rs'
    point_view=view_probe.read_text('utf-8')
    old_filter='v.retained_view_entity.main_entity==light.into()&&v.retained_view_entity.auxiliary_entity==camera.into()&&v.clip_from_view.w_axis.w==1.'
    assert point_view.count(old_filter)==1
    point_view=point_view.replace(old_filter,'v.retained_view_entity.main_entity==point_entity.into()&&v.clip_from_view.w_axis.w==0.')
    point_view=point_view.replace('assert_eq!(rows.len(),4)','assert_eq!(rows.len(),6)').replace('CASTER-VIEW-','POINT-SHADOW-VIEW-').replace('"orthographic_cascades"','"point_faces"').replace('"gpu_scalars_read":448','"gpu_scalars_read":672').replace('"cpu_gpu_scalars_compared":192','"cpu_gpu_scalars_compared":288')
    marker=' let raw_path=folder.join(format!("gpu-{view}.rgba16f"));';assert rust.count(marker)==1
    rust=rust.replace(marker,diagnostic.read_text('utf-8')+'\n'+point_view+'\n'+marker)
    ROOT.mkdir();(ROOT/'src').mkdir()
    for name in ['Cargo.toml','Cargo.lock','CASE.v1.json','caster-view-probe.wgsl','depth-probe.wgsl']:(ROOT/name).write_bytes((OLD/name).read_bytes())
    (ROOT/'src/main.rs').write_text(rust,'utf-8')
    shader=S/'candidates/SourcePointLightRecordProbeV1.wgsl';(ROOT/'point-record-probe.wgsl').write_bytes(shader.read_bytes())
    files=[Path(__file__).resolve(),BASE,diagnostic,view_probe,shader,OLD/'PREPARED.v1.json',OLD/'RUN-RESULT.v1.json',OLD/'src/main.rs',*[p for p in ROOT.rglob('*') if p.is_file()]]
    pins={**prep['input_pins'],**{str(p):runner.sha(p) for p in files}}
    runner.save('PREPARED.v1.json',dict(input_pins=pins,fixture=prep['fixture'],actual_device_required=True,private_gpu_observation_only=True,shader_case_unchanged=True,full_import=False))
    with (ROOT/'controller.log').open('xb') as log:
        proc=subprocess.Popen([sys.executable,'-B',str(Path(__file__).resolve()),'run'],cwd=ROOT,stdout=log,stderr=log,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
    runner.save('JOB.v1.json',dict(pid=proc.pid,created_utc=datetime.now(timezone.utc).isoformat()));print(json.dumps({'pid':proc.pid,'root':str(ROOT)}))

if __name__=='__main__':prepare() if sys.argv[-1]=='prepare' else runner.run()
