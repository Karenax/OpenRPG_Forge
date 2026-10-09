"""Reuse the pinned completed scene probe directly; bind shared cube to five materials."""
from pathlib import Path
from datetime import datetime,timezone
import importlib.util,json,sys,subprocess
BASE=Path(__file__).with_name('check_source_scene_actual_device_mount_v1.py')
spec=importlib.util.spec_from_file_location('source_scene_runner',BASE);runner=importlib.util.module_from_spec(spec);spec.loader.exec_module(runner)
W=runner.W;R=runner.R;ROOT=W/'firepit-specular-scene-v1';OLD=W/'firepit-diffuse-scene-v5';CASE=W/'firepit-specular-scene-case-v1'
runner.ROOT=ROOT
def prepare():
    assert not ROOT.exists()
    protocol=runner.read(CASE/'RESULT.v1.json')
    for p,h in protocol['input_pins'].items():assert runner.sha(p)==h,p
    fixture=Path(protocol['private_fixture']);assert runner.sha(fixture)==protocol['private_fixture_sha256']
    template=runner.read(OLD/'PREPARED.v1.json')
    for name in ['Cargo.toml','Cargo.lock','src/main.rs']:
        assert runner.sha(OLD/name)==template['input_pins'][str(OLD/name)],name
    rust=(OLD/'src/main.rs').read_text('utf-8')
    cube='''
 let packet=&case["sky_cube_packet"];
 let cube=SourceGraphSkyCube::from_mip_major_rgba16f_le(&std::fs::read(packet["path"].as_str().unwrap())?,packet["sha256"].as_str().unwrap().parse().unwrap(),packet["width"].as_u64().unwrap() as u32,packet["cube_count"].as_u64().unwrap() as u32,packet["mip_count"].as_u64().unwrap() as u32)?;
 let sky_binding=cube.install_unqualified(&mut app.world_mut().resource_mut::<Assets<Image>>());
 for h in materials.values(){let mut assets=app.world_mut().resource_mut::<Assets<SourceGraphMaterial>>();let mut material=assets.get_mut(h).unwrap();assert!(material.extension.program_key.requires_sky_cube);material.extension.set_sky_cube(&sky_binding);}
'''
    marker=' host.install_shadow_routes(app.world_mut(),31)?;';assert rust.count(marker)==1;rust=rust.replace(marker,cube+'\n'+marker)
    marker='ground_value.extension.set_preintegrated_brdf(&binding);';assert rust.count(marker)==1;rust=rust.replace(marker,marker+'ground_value.extension.set_sky_cube(&sky_binding);')
    marker='"measured_diffuse_applied_on_object_and_receiver":true,';assert rust.count(marker)==1
    rust=rust.replace(marker,marker+'"measured_source_specular_applied_on_object_and_receiver":true,"shared_sky_materials":5,')
    ROOT.mkdir();(ROOT/'src').mkdir();(ROOT/'CASE.v1.json').write_bytes((CASE/'CASE.v1.json').read_bytes())
    for name in ['Cargo.toml','Cargo.lock']:(ROOT/name).write_text((OLD/name).read_text('utf-8').replace('openrpg-firepit-diffuse-scene-v5','openrpg-firepit-specular-scene-v1'),'utf-8')
    (ROOT/'src/main.rs').write_text(rust,'utf-8')
    files=[Path(__file__).resolve(),BASE,fixture,CASE/'RESULT.v1.json',CASE/'CASE.v1.json',*[OLD/n for n in ['Cargo.toml','Cargo.lock','src/main.rs']],*[runner.A/('source_graph_'+m+'.rs') for m in runner.MODULES],*[p for p in ROOT.rglob('*') if p.is_file()]]
    files.extend(Path(p) for p in protocol['input_pins'])
    files.extend(p for p in (R/'crates').rglob('*') if p.is_file() and p.suffix in ['.rs','.wgsl','.toml'])
    runner.save('PREPARED.v1.json',dict(input_pins={str(p):runner.sha(p) for p in files},fixture=str(fixture),actual_device_required=True,full_import=False))
    with (ROOT/'controller.log').open('xb') as log:proc=subprocess.Popen([sys.executable,'-B',str(Path(__file__).resolve()),'run'],cwd=ROOT,stdout=log,stderr=log,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
    runner.save('JOB.v1.json',dict(pid=proc.pid,created_utc=datetime.now(timezone.utc).isoformat()));print(json.dumps(dict(pid=proc.pid,root=str(ROOT))))
if __name__=='__main__':prepare() if sys.argv[-1]=='prepare' else runner.run()
