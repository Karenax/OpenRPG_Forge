"""Repeat pinned surface draw unchanged and observe native scene transforms."""
from pathlib import Path
from datetime import datetime,timezone
import importlib.util,subprocess,sys,json
BASE=Path(__file__).with_name('check_source_scene_actual_device_mount_v1.py')
spec=importlib.util.spec_from_file_location('surface_camera_runner',BASE);runner=importlib.util.module_from_spec(spec);spec.loader.exec_module(runner)
W=runner.W;R=runner.R;ROOT=W/'firepit-surface-camera-native-v1';OLD=W/'firepit-surface-native-v1';CASE=W/'firepit-surface-native-case-v1';runner.ROOT=ROOT
def prepare():
    assert not ROOT.exists()
    protocol=runner.read(CASE/'RESULT.v1.json');fixture=Path(protocol['private_fixture'])
    assert runner.sha(fixture)==protocol['private_fixture_sha256']
    for p,h in protocol['input_pins'].items():assert runner.sha(p)==h,p
    template=runner.read(OLD/'PREPARED.v1.json')
    for name in ['Cargo.toml','Cargo.lock','src/main.rs']:assert runner.sha(OLD/name)==template['input_pins'][str(OLD/name)],name
    rust=(OLD/'src/main.rs').read_text('utf-8')
    marker=' let raw_path=folder.join(format!("gpu-{view}.rgba16f"));';assert rust.count(marker)==1
    observe=r'''
 let camera_world=app.world().get::<GlobalTransform>(camera).unwrap().to_matrix();
 let clip_from_view=app.world().get::<Camera>(camera).unwrap().clip_from_view();
 let scene_transforms:Vec<_>=selected_draws.iter().map(|e|serde_json::json!({"draw":format!("{:?}",e),"world_from_local":app.world().get::<GlobalTransform>(*e).unwrap().to_matrix().to_cols_array()})).collect();
 let source_uniform_rows:Vec<_>=materials.values().map(|h|{let u=&app.world().resource::<Assets<SourceGraphMaterial>>().get(h).unwrap().extension.source;serde_json::json!({"source_object_from_target_object":u.source_object_from_target_object.to_cols_array(),"target_object_from_source_object":u.target_object_from_source_object.to_cols_array(),"source_world_from_target_world":u.source_world_from_target_world.to_cols_array()})}).collect();
 let extracted_rows={let rw=app.get_sub_app_mut(RenderApp).unwrap().world_mut();let mut q=rw.query::<&bevy::render::view::ExtractedView>();q.iter(rw).map(|v|serde_json::json!({"world_from_view":v.world_from_view.to_matrix().to_cols_array(),"clip_from_view":v.clip_from_view.to_cols_array(),"viewport":v.viewport.to_array()})).collect::<Vec<_>>()};
 let observation=serde_json::json!({"view":view,"camera_world":camera_world.to_cols_array(),"view_from_world":camera_world.inverse().to_cols_array(),"clip_from_view":clip_from_view.to_cols_array(),"root_world":app.world().get::<GlobalTransform>(root).unwrap().to_matrix().to_cols_array(),"selected_node_world":app.world().get::<GlobalTransform>(selected).unwrap().to_matrix().to_cols_array(),"draw_transforms":scene_transforms,"source_uniform_rows":source_uniform_rows,"extracted_views":extracted_rows,"gpu_uniform_readback":false});
 std::fs::write(folder.join(format!("CAMERA-STATE-{view}.json")),serde_json::to_vec_pretty(&observation)?)?;
'''
    rust=rust.replace(marker,observe+marker)
    ROOT.mkdir();(ROOT/'src').mkdir();(ROOT/'CASE.v1.json').write_bytes((CASE/'CASE.v1.json').read_bytes())
    for name in ['Cargo.toml','Cargo.lock']:(ROOT/name).write_text((OLD/name).read_text('utf-8').replace('openrpg-firepit-surface-native-v1','openrpg-firepit-surface-camera-native-v1'),'utf-8')
    (ROOT/'src/main.rs').write_text(rust,'utf-8')
    files=[Path(__file__).resolve(),BASE,fixture,CASE/'RESULT.v1.json',CASE/'CASE.v1.json',*[OLD/n for n in ['Cargo.toml','Cargo.lock','src/main.rs']],*[runner.A/('source_graph_'+m+'.rs') for m in runner.MODULES],*[p for p in ROOT.rglob('*') if p.is_file()],*[Path(p) for p in protocol['input_pins']]]
    files.extend(p for p in (R/'crates').rglob('*') if p.is_file() and p.suffix in ['.rs','.wgsl','.toml'])
    runner.save('PREPARED.v1.json',dict(input_pins={str(p):runner.sha(p) for p in files},fixture=str(fixture),actual_device_required=True,full_import=False))
    with (ROOT/'controller.log').open('xb') as log:proc=subprocess.Popen([sys.executable,'-B',str(Path(__file__).resolve()),'run'],cwd=ROOT,stdout=log,stderr=log,stdin=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
    runner.save('JOB.v1.json',dict(pid=proc.pid,created_utc=datetime.now(timezone.utc).isoformat()));print(json.dumps(dict(pid=proc.pid,root=str(ROOT))))
if __name__=='__main__':prepare() if sys.argv[-1]=='prepare' else runner.run()
