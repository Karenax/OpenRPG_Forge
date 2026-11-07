"""Measure Rapier candidate shape queries against Unity; preserve every mismatch."""
from pathlib import Path
import datetime, hashlib, json, os, re, shutil, subprocess, traceback

R = Path(__file__).resolve().parents[2]
W = Path('F:/OpenRPG_ImportWork')
O = W / 'house-rapier-mesh-query-v686'
BIN = Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin')
T = W / 'firepit-static-glb-product-check-v2/target'
assert not O.exists()

def read(p): return json.loads(Path(p).read_text('utf-8-sig'))
def sha(p):
    with Path(p).open('rb') as f: return hashlib.file_digest(f, 'sha256').hexdigest()

checkpoint = W / 'house-native-mesh-checkpoint-v685/RESULT.v1.json'
plan_path = W / 'house-collider-binding-plan-v678/RESULT.v1.json'
native, plan = read(checkpoint), read(plan_path)
for p, h in native['input_pins'].items(): assert sha(p) == h, p
rows = {(r['source_component']['guid'], r['source_component']['file_id']): r
        for r in plan['rows'] if r['kind'] == 'assigned_mesh'}
cases = []
for reference in native['rows']:
    assert reference['world_position'] == [0, 0, 0]
    assert reference['world_rotation'] == [0, 0, 0, 1] and reference['lossy_scale'] == [1, 1, 1]
    row = rows[(reference['source_guid'], reference['source_file_id'])]
    assert sha(row['glb']) == row['glb_sha256']
    cases.append(dict(reference=reference, geometry=row['embedded_geometry'], glb=row['glb'],
                      glb_sha256=row['glb_sha256']))
assert len(cases) == 19
O.mkdir(); (O / 'src').mkdir(); (O / 'temp').mkdir()
name = 'house-rapier-mesh-query-v686'
seed = W / 'gladiator-viewport-lod-cycle-v469/Cargo.toml'
cargo = seed.read_text('utf-8').split('[[bin]]')[0]
cargo = re.sub(r'^forge_engine=.*\n', '', cargo, flags=re.M)
cargo += '\nbevy_rapier3d={version="=0.36.0",default-features=false,features=["dim3"]}\n'
cargo += '[patch.crates-io]\nrapier3d={path="' + (R / 'third_party/rapier3d-0.35.0-glamx0.2').as_posix() + '"}\n'
cargo += '[[bin]]\nname="' + name + '"\npath="src/main.rs"\n'
(O / 'Cargo.toml').write_text(cargo, 'utf-8')
shutil.copyfile(R / 'Cargo.lock', O / 'Cargo.lock')
(O / 'CASES.v1.json').write_text(json.dumps(cases, indent=2), 'utf-8')
SOURCE = r'''
use bevy::prelude::*;
use bevy_rapier3d::prelude::Collider;
use serde_json::{Value,json};
fn vec3(v:&Value)->Vec3 { Vec3::new(v[0].as_f64().unwrap() as f32,v[1].as_f64().unwrap() as f32,v[2].as_f64().unwrap() as f32) }
fn words(bytes:&[u8])->Vec<f32>{assert_eq!(bytes.len()%4,0);bytes.chunks_exact(4).map(|v|f32::from_le_bytes(v.try_into().unwrap())).collect()}
fn near(a:f32,b:f32)->bool{a.is_finite()&&b.is_finite()&&(a-b).abs()<=1e-6*b.abs().max(1.)}
fn vn(a:Vec3,b:Vec3)->bool{a.to_array().into_iter().zip(b.to_array()).all(|(a,b)|near(a,b))}
fn main(){
 let cases:Vec<Value>=serde_json::from_slice(&std::fs::read(std::env::args().nth(1).unwrap()).unwrap()).unwrap();
 let basis=Mat4::from_scale(Vec3::new(1.,1.,-1.));let mut results=Vec::new();
 for case in cases {
  let raw=std::fs::read(case["glb"].as_str().unwrap()).unwrap();
  let glb=forge_assets::source_graph_glb::read_source_graph_glb(&raw).unwrap().unwrap();
  let geo=&case["geometry"];let reference=&case["reference"];
  let view=geo["attributes"]["POSITION"]["buffer_view"].as_u64().unwrap() as usize;
  let floats=words(glb.views[view]);assert_eq!(floats.len()%3,0);
  let vertices:Vec<Vec3>=floats.chunks_exact(3).map(|v|Vec3::new(v[0],v[1],v[2])).collect();
  assert!(vertices.iter().all(|v|v.is_finite()));
  let mut indices=Vec::new();
  for entry in geo["index_buffers"].as_array().unwrap(){
   let bytes=glb.views[entry["buffer_view"].as_u64().unwrap() as usize];assert_eq!(bytes.len()%12,0);
   for face in bytes.chunks_exact(12){let ids:[u32;3]=std::array::from_fn(|i|u32::from_le_bytes(face[i*4..i*4+4].try_into().unwrap()));assert!(ids.iter().all(|v|(*v as usize)<vertices.len()));indices.push(ids);}
  }
  let convex=reference["convex"].as_bool().unwrap();
  let collider=if convex{Collider::convex_hull(&vertices).expect("Rapier convex hull refused")}else{Collider::trimesh(vertices.clone(),indices.clone()).expect("Rapier original triangle mesh refused")};
  let mut failures=Vec::new();let mut point_count=0;let mut ray_count=0;
  for (i,s) in reference["points"].as_array().unwrap().iter().enumerate(){
   let q=basis.transform_point3(vec3(&s["world_query"]));let expected=basis.transform_point3(vec3(&s["closest_world"]));
   let actual=collider.project_point(Vec3::ZERO,Quat::IDENTITY,q,true).point;
   if !vn(actual,expected){failures.push(json!({"kind":"closest_point","sample":i,"actual":actual.to_array(),"expected":expected.to_array(),"absolute_error":(actual-expected).abs().to_array()}));}point_count+=1;
  }
  for (i,s) in reference["rays"].as_array().unwrap().iter().enumerate(){
   let origin=basis.transform_point3(vec3(&s["origin"]));let direction=basis.transform_vector3(vec3(&s["direction"]));
   let actual=collider.cast_ray_and_get_normal(Vec3::ZERO,Quat::IDENTITY,origin,direction,s["max_distance"].as_f64().unwrap() as f32,true);
   let expected_hit=s["hit"].as_bool().unwrap();
   if actual.is_some()!=expected_hit{failures.push(json!({"kind":"ray_presence","sample":i,"actual_hit":actual.is_some(),"expected_hit":expected_hit}));}
   else if let Some(hit)=actual{
    let point=basis.transform_point3(vec3(&s["point"]));let normal=basis.transform_vector3(vec3(&s["normal"]));let distance=s["distance"].as_f64().unwrap() as f32;
    if !near(hit.time_of_impact,distance)||!vn(hit.point,point)||!vn(hit.normal,normal){failures.push(json!({"kind":"ray_values","sample":i,"actual_distance":hit.time_of_impact,"expected_distance":distance,"actual_point":hit.point.to_array(),"expected_point":point.to_array(),"actual_normal":hit.normal.to_array(),"expected_normal":normal.to_array()}));}
   }ray_count+=1;
  }
  results.push(json!({"guid":reference["source_guid"],"file_id":reference["source_file_id"],"convex":convex,"source_cooking_options":reference["cooking_options"],"vertices":vertices.len(),"triangles":indices.len(),"points":point_count,"rays":ray_count,"failures":failures,"rapier_constructor_only":true,"physics_world_integration_qualified":false}));
 }
 println!("{}",json!({"rows":results,"full_import":false,"studio_verified":false}));
}
'''
(O / 'src/main.rs').write_text(SOURCE, 'utf-8')
files = [Path(__file__), checkpoint, plan_path, seed, O / 'CASES.v1.json', O / 'Cargo.toml', O / 'src/main.rs', R / 'Cargo.toml', R / 'Cargo.lock']
for root in [R / 'third_party/rapier3d-0.35.0-glamx0.2', R / 'crates/forge_assets', R / 'crates/forge_core']:
    files.extend(p for p in root.rglob('*') if p.is_file() and (p.suffix == '.rs' or p.name == 'Cargo.toml'))
pins = {str(p): sha(p) for p in files}
pins.update({c['glb']: c['glb_sha256'] for c in cases})
state = dict(status='prepared', full_import=False, physics_world_integration_qualified=False)
def save():
    state['utc'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
    tmp = O / 'RUN-RESULT.tmp'; tmp.write_text(json.dumps(state, indent=2), 'utf-8'); tmp.replace(O / 'RUN-RESULT.v1.json')
save()
env = os.environ.copy()
env.update(PATH=str(BIN)+os.pathsep+env.get('PATH',''),RUSTC=str(BIN/'rustc.exe'),RUSTDOC=str(BIN/'rustdoc.exe'),CARGO_TARGET_DIR=str(T),CARGO_INCREMENTAL='0',CARGO_BUILD_JOBS='1',TEMP=str(O/'temp'),TMP=str(O/'temp'))
try:
    with (O / 'lock.log').open('xb') as log:
        code = subprocess.run([str(BIN/'cargo.exe'),'update','--offline','--workspace','--manifest-path',str(O/'Cargo.toml')],env=env,cwd=O,stdout=log,stderr=log,creationflags=subprocess.CREATE_NO_WINDOW).returncode
    assert code == 0, 'Offline lock update refused'
    pins[str(O/'Cargo.lock')] = sha(O/'Cargo.lock')
    (O/'PREPARED.v1.json').write_text(json.dumps(dict(input_pins=pins),indent=2),'utf-8')
    with (O/'cargo.log').open('xb') as log:
        proc = subprocess.Popen([str(BIN/'cargo.exe'),'build','--offline','--locked','--manifest-path',str(O/'Cargo.toml'),'--bin',name],env=env,cwd=O,stdout=log,stderr=log,creationflags=subprocess.CREATE_NO_WINDOW)
        state.update(status='building_mesh_query_candidate', cargo_pid=proc.pid); save(); code = proc.wait()
    assert code == 0, 'See cargo.log'
    exe = O/'rapier-mesh-checker.exe'; shutil.copyfile(T/'debug'/(name+'.exe'),exe)
    with (O/'stdout.json').open('xb') as out, (O/'stderr.log').open('xb') as err:
        proc = subprocess.Popen([str(exe),str(O/'CASES.v1.json')],env=env,stdout=out,stderr=err,creationflags=subprocess.CREATE_NO_WINDOW)
        state.update(status='executing_mesh_query_candidate',checker_pid=proc.pid); save(); code = proc.wait()
    assert code == 0, 'See stderr.log'
    measured = read(O/'stdout.json')
    assert len(measured['rows']) == 19
    assert sum(r['points'] for r in measured['rows']) == 357 and sum(r['rays'] for r in measured['rows']) == 114
    for p,h in pins.items(): assert sha(p) == h,p
    failures = sum(len(r['failures']) for r in measured['rows'])
    state.update(status='mesh_candidate_mismatches_retained' if failures else 'mesh_candidate_queries_match_unity_controls',rows=measured['rows'],failures=failures,input_pins=pins,checker_sha256=sha(exe))
except Exception as e:
    state.update(status='mesh_query_candidate_refusal_retained',error=repr(e),traceback=traceback.format_exc())
save()
print(json.dumps({k:state.get(k) for k in ['status','failures','error']}))
