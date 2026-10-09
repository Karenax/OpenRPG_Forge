"""Run the existing pinned scene probe with visibility-only fragment outputs."""
from pathlib import Path
from datetime import datetime,timezone
import importlib.util,subprocess,sys,json
BASE=Path(__file__).with_name('check_source_scene_actual_device_mount_v1.py')
spec=importlib.util.spec_from_file_location('source_ssao_runner',BASE);runner=importlib.util.module_from_spec(spec);spec.loader.exec_module(runner)
W=runner.W;R=runner.R;ROOT=W/'firepit-ssao-native-v1';OLD=W/'firepit-specular-scene-v2';CASE=W/'firepit-ssao-native-case-v1';runner.ROOT=ROOT
def prepare():
    assert not ROOT.exists()
    protocol=runner.read(CASE/'RESULT.v1.json');fixture=Path(protocol['private_fixture']);assert runner.sha(fixture)==protocol['private_fixture_sha256']
    for p,h in protocol['input_pins'].items():assert runner.sha(p)==h,p
    template=runner.read(OLD/'PREPARED.v1.json')
    for name in ['Cargo.toml','Cargo.lock','src/main.rs']:assert runner.sha(OLD/name)==template['input_pins'][str(OLD/name)],name
    rust=(OLD/'src/main.rs').read_text('utf-8')
    rust=rust.replace('"measured_diffuse_applied_on_object_and_receiver":true','"measured_diffuse_applied_on_object_and_receiver":false').replace('"measured_source_specular_applied_on_object_and_receiver":true','"measured_source_specular_applied_on_object_and_receiver":false')
    marker='"shared_sky_materials":5,';assert rust.count(marker)==1;rust=rust.replace(marker,marker+'"actual_native_ssao_visibility_diagnostic":true,')
    ROOT.mkdir();(ROOT/'src').mkdir();(ROOT/'CASE.v1.json').write_bytes((CASE/'CASE.v1.json').read_bytes())
    for name in ['Cargo.toml','Cargo.lock']:(ROOT/name).write_text((OLD/name).read_text('utf-8').replace('openrpg-firepit-specular-scene-v2','openrpg-firepit-ssao-native-v1'),'utf-8')
    (ROOT/'src/main.rs').write_text(rust,'utf-8')
    files=[Path(__file__).resolve(),BASE,fixture,CASE/'RESULT.v1.json',CASE/'CASE.v1.json',*[OLD/n for n in ['Cargo.toml','Cargo.lock','src/main.rs']],*[runner.A/('source_graph_'+m+'.rs') for m in runner.MODULES],*[p for p in ROOT.rglob('*') if p.is_file()],*[Path(p) for p in protocol['input_pins']]]
    files.extend(p for p in (R/'crates').rglob('*') if p.is_file() and p.suffix in ['.rs','.wgsl','.toml'])
    runner.save('PREPARED.v1.json',dict(input_pins={str(p):runner.sha(p) for p in files},fixture=str(fixture),actual_device_required=True,full_import=False))
    with (ROOT/'controller.log').open('xb') as log:proc=subprocess.Popen([sys.executable,'-B',str(Path(__file__).resolve()),'run'],cwd=ROOT,stdout=log,stderr=log,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
    runner.save('JOB.v1.json',dict(pid=proc.pid,created_utc=datetime.now(timezone.utc).isoformat()));print(json.dumps(dict(pid=proc.pid,root=str(ROOT))))
if __name__=='__main__':prepare() if sys.argv[-1]=='prepare' else runner.run()
