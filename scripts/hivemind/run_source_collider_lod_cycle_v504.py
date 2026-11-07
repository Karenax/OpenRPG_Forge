"""Keep actual host colliders alive throughout all LOD/camera transitions."""
from pathlib import Path
W = Path('F:/OpenRPG_ImportWork')
S = Path(__file__).resolve().parent
parent = S/'run_gladiator_viewport_lod_v466.py'
base = W/'gladiator-source-collider-host-cycle-v496'
source_path = base/'src/main.rs'
probe_path = S/'assert_source_colliders_lod_v504.rs'
source = source_path.read_text('utf-8')
assert source.count('let native:serde_json::Value=') == 1
source = source.replace('let native:serde_json::Value=', 'let native_boxes:serde_json::Value=')
source = source.replace('exercise_host_colliders(&mut app,&mut host,&native,', 'exercise_host_colliders(&mut app,&mut host,&native_boxes,')
tail = 'drop(read); *app.world_mut().get_mut::<Transform>(root).unwrap() = Transform::IDENTITY; app.update();'
assert source.count(tail) == 1
source = source.replace(tail, tail+' assert_eq!(host.install_colliders(app.world_mut(), &policies).unwrap(), boxes.len()); app.update(); app.world_mut().run_schedule(FixedUpdate);')
needle = 'let policy=source_graph_runtime::RuntimePolicy'
assert source.count(needle) == 1
source = source.replace(needle, 'let collider_entities=host.collider_projection.as_ref().unwrap().entities.clone();let collider_handles:Vec<_>=collider_entities.iter().map(|e|app.world().get::<RapierColliderHandle>(*e).unwrap().0).collect();'+needle)
source = source.replace('let mut cases=0;', 'let mut cases=0;let mut physical_phases=0;let mut physical_rays=0;')
check = 'physical_rays+=assert_live_colliders(&mut app,&collider_entities,&collider_handles,&native_boxes,basis.target_from_source);physical_phases+=1;'
for old, new, count in [
    ('app.update();\n  assert_eq!', 'app.update();'+check+'\n  assert_eq!', 1),
    ('app.update();assert!(matches!', 'app.update();'+check+'assert!(matches!', 2),
    ('app.update();assert_eq!', 'app.update();'+check+'assert_eq!', 1),
]:
    assert source.count(old) == count, old
    source = source.replace(old, new)
source = source.replace('assert!(cases>0);app.world_mut().despawn(root);app.update();', 'assert!(cases>0);assert_eq!(physical_phases,cases*4);assert_eq!(physical_rays,cases*4*6*collider_entities.len());app.world_mut().despawn(root);app.update();assert_removed_colliders(&mut app,&collider_entities);')
source = source.replace('"material_binding_count":0,', '"material_binding_count":0,"colliders_during_lod_verified":true,"physical_phases":physical_phases,"physical_rays":physical_rays,"source_boxes":collider_entities.len(),')
source += probe_path.read_text('utf-8')

code = parent.read_text('utf-8').replace('gladiator-viewport-lod-cycle-v466', 'source-collider-lod-cycle-v504').replace('openrpg-gladiator-viewport-lod-v466', 'openrpg-source-collider-lod-cycle-v504')
code = code.replace("'context','lights'", "'context','bootstrap','lights'")
old = "cargo=(B/'Cargo.toml').read_text('utf-8').replace('openrpg-studio-source-bootstrap-check',name)"
assert old in code
code = code.replace(old, 'cargo=Path('+repr(str(base/'Cargo.toml'))+").read_text('utf-8').split('[[bin]]')[0]")
old = "(O/'Cargo.lock').write_text((B/'Cargo.lock').read_text('utf-8').replace('openrpg-studio-source-bootstrap-check',name),'utf-8')"
assert old in code
code = code.replace(old, 'shutil.copyfile(Path('+repr(str(base/'Cargo.lock'))+"),O/'Cargo.lock')")
write = "(O/'src/main.rs').write_text(source,'utf-8')"
assert code.count(write) == 1
code = code.replace(write, 'source='+repr(source)+'\n'+write)
pins = 'pins={str(p):sha(p) for p in files}'
extra = 'files.extend([parent,Path('+repr(str(source_path))+'),Path('+repr(str(probe_path))+'),Path('+repr(str(W/'gladiator-native-box-metric-v471/NATIVE-RESULT.v1.json'))+')])\n'
extra += "files.extend(p for directory in ['rerecast-0.4.0','rapier3d-0.35.0-glamx0.2'] for p in (R/'third_party'/directory).rglob('*') if p.is_file() and (p.suffix=='.rs' or p.name=='Cargo.toml'))\n" + pins
code = code.replace(pins, extra)
code = code.replace("[str(exe),row['glb'],str(metric)]", "[str(exe),row['glb'],str(metric),"+repr(str(W/'gladiator-native-box-metric-v471/NATIVE-RESULT.v1.json'))+"]")
code = code.replace('207_actual_viewport_lod_cycles_and_camera_refusal_recovery_checks_passed', '207_lod_cycles_and_828_physics_phases_keep_source_colliders_live')
exec(compile(code, str(parent)+'#concurrent-source-colliders', 'exec'), globals())
