"""Replay actual host collision methods followed by the existing 207 LOD cycles."""
from pathlib import Path
import re

R = Path('E:/OpenRPG_Forge/OpenRPG_Forge-Rust')
parent = Path(__file__).with_name('run_gladiator_viewport_lod_v466.py')
probe = Path(__file__).with_name('source_collider_projection_probe_v480.rs')
fixture = probe.read_text('utf-8')
fixture, n = re.subn(r'fn exercise_projection\([\s\S]*?\) \{', 'fn exercise_host_colliders(app: &mut App, host: &mut source_graph_host::SourceSceneHost, native: &serde_json::Value, basis: Mat4) {', fixture, count=1)
assert n == 1
fixture = fixture.replace('let doc = glb.document();', 'let document = host.source_document.clone(); let doc = &document;')
fixture = fixture.replace('source_colliders::', 'forge_engine::source_colliders::')
fixture, n = re.subn(r'    let plan = source_graph_scene::read_source_graph_scene[\s\S]*?    let mut policies', '    let root = host.root; let nodes = host.attached.nodes.clone();\n    let mut policies', fixture, count=1)
assert n == 1
fixture = fixture.replace('source_collider_projection::', 'forge_engine::source_collider_projection::')
fixture, n = re.subn(r'    let projection = forge_engine::source_collider_projection::attach\([\s\S]*?    assert_eq!\(projection.entities.len\(\), boxes.len\(\)\);', '    assert_eq!(host.install_colliders(app.world_mut(), &policies).unwrap(), boxes.len());\n    let projection_entities = host.collider_projection.as_ref().unwrap().entities.clone();', fixture, count=1)
assert n == 1
fixture = fixture.replace('projection.entities', 'projection_entities')
fixture = fixture.replace('projection.clear(app.world_mut());', 'assert_eq!(host.remove_colliders(app.world_mut()), boxes.len()); assert_eq!(host.remove_colliders(app.world_mut()), 0);')
fixture = fixture.replace('state.get(app.world());', 'state.get(app.world()).unwrap();')
fixture = fixture.replace('.project_point(query, true, QueryFilter::default())', '.project_point(query, 1000., true, QueryFilter::default())')
fixture = fixture.replace('assert!(context.colliders.colliders.is_empty());', 'assert!(context.colliders.colliders.is_empty());\n    drop(read); *app.world_mut().get_mut::<Transform>(root).unwrap() = Transform::IDENTITY; app.update();')

code = parent.read_text('utf-8').replace('viewport-lod-cycle-v466', 'source-collider-host-cycle-v486').replace('viewport-lod-v466', 'source-collider-host-v486')
code = code.replace("'context','lights'", "'context','bootstrap','lights'")
code = code.replace("(O/'Cargo.toml').write_text(cargo+", "cargo += '\\nbevy_rapier3d={version=\"=0.36.0\",default-features=false,features=[\"dim3\"]}\\n[patch.crates-io]\\nrapier3d={path=\"'+(R/'third_party/rapier3d-0.35.0-glamx0.2').as_posix()+'\"}\\n'\n(O/'Cargo.toml').write_text(cargo+")
old = "(O/'Cargo.lock').write_text((B/'Cargo.lock').read_text('utf-8').replace('openrpg-studio-source-bootstrap-check',name),'utf-8')"
assert old in code
code = code.replace(old, "shutil.copyfile(R/'Cargo.lock',O/'Cargo.lock')")
code = code.replace("{(p['name'],p['version'],p.get('source')) for p in data['package']}", "{(p['name'],p['version'],p.get('source')) for p in data['package'] if p['name'] != name}")
needle = "(O/'src/main.rs').write_text(source,'utf-8')"
injection = "source=source.replace('app.add_plugins((MinimalPlugins,bevy::transform::TransformPlugin));','app.add_plugins((MinimalPlugins,bevy::transform::TransformPlugin,bevy_rapier3d::prelude::RapierPhysicsPlugin::<bevy_rapier3d::prelude::NoUserData>::default().in_fixed_schedule()));')\n"
injection += "source=source.replace('let host=source_graph_host::SourceSceneHost{','let mut host=source_graph_host::SourceSceneHost{')\n"
injection += "source=source.replace('original_state:Some(plan),object_basis:Some(basis)};', 'original_state:Some(plan),object_basis:Some(basis),source_document:glb.document().clone(),collider_projection:None};')\n"
injection += "source=source.replace('let policy=source_graph_runtime::RuntimePolicy', 'let native:serde_json::Value=serde_json::from_slice(&std::fs::read(std::env::args().nth(3).unwrap()).unwrap()).unwrap();exercise_host_colliders(&mut app,&mut host,&native,basis.target_from_source);let policy=source_graph_runtime::RuntimePolicy')\n"
injection += "source=source.replace('\"material_binding_count\":0,', '\"material_binding_count\":0,\"actual_host_collision_install_remove_verified\":true,')\n"
injection += 'source+=' + repr(fixture) + '\n' + needle
assert code.count(needle) == 1
code = code.replace(needle, injection)
native = Path('F:/OpenRPG_ImportWork/gladiator-native-box-metric-v471/NATIVE-RESULT.v1.json')
code = code.replace("files=[Path(__file__),O/'src/main.rs'", "files=[parent,Path(" + repr(str(probe)) + "),Path(" + repr(str(native)) + "),Path(__file__),O/'src/main.rs'")
code = code.replace("[str(exe),row['glb'],str(metric)]", "[str(exe),row['glb'],str(metric)," + repr(str(native)) + "]")
code = code.replace('207_actual_viewport_lod_cycles_and_camera_refusal_recovery_checks_passed', 'five_actual_hosts_install_remove_colliders_and_pass_207_lod_cycles')
exec(compile(code, str(parent) + '#host-collision-v486', 'exec'), globals())
