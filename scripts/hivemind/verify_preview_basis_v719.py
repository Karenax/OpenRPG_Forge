from pathlib import Path
import json,hashlib,os,subprocess
R=Path(__file__).resolve().parents[2]; W=Path('F:/OpenRPG_ImportWork'); O=W/'source-preview-basis-v719'; B=Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin')
assert not O.exists(); O.mkdir(); (O/'src').mkdir(); (O/'temp').mkdir()
def sha(p):
 with Path(p).open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
M=R/'apps/forge_studio/src/authoring_assets'; ctx=M/'source_graph_context.rs'; preview=M/'source_graph_preview.rs'; runtime=M/'source_graph_runtime.rs'
c=ctx.read_text('utf-8'); v=preview.read_text('utf-8'); rt=runtime.read_text('utf-8')
# Exact current source sections, including validation; no replacement formulas.
base=c[c.index('#[derive(Clone, Copy)]'):c.index('/// Supplied by the shared viewport')]
world=c[c.index('pub(super) fn world_mapping('):c.index('/// HDRP HDCamera')]
policy=v[v.index('fn policy('):v.index('fn mount(')]
fields=rt[rt.index('#[derive(Clone, Copy)]'):rt.index('impl RuntimePolicy')].replace('pub(super) struct','struct')
main=r"""
use bevy::prelude::*;use serde_json::Value;
mod source_graph_context{use super::*;@@BASE@@ @@WORLD@@}
@@FIELDS@@
@@POLICY@@
fn main(){let receipt:Value=serde_json::from_slice(&std::fs::read(std::env::args().nth(1).unwrap()).unwrap()).unwrap();let mut count=0;for row in receipt["rows"].as_array().unwrap(){let bytes=std::fs::read(row["glb"].as_str().unwrap()).unwrap();let glb=forge_assets::source_graph_glb::read_source_graph_glb(&bytes).unwrap().unwrap();let basis=source_graph_context::ObjectBasis::from_document(glb.document()).unwrap().unwrap();assert_eq!(basis.source_from_target,Mat4::from_scale(Vec3::new(1.,1.,-1.)));let offset=Vec3::new(0.,1024.,0.);let p=policy(basis,offset).unwrap();for point in [Vec3::new(12.,7.,-3.),Vec3::new(-8.,2.,11.),Vec3::ZERO]{let expected=Vec3::new(point.x,point.y,-point.z);assert_eq!(p.source_world_from_target_world.transform_point3(point+offset),expected);assert_eq!(p.source_absolute_world_from_target_world.transform_point3(point+offset),expected);}count+=1;}assert_eq!(count,24);let x=Mat4::from_scale(Vec3::new(-1.,1.,1.));let p=policy(source_graph_context::ObjectBasis{source_from_target:x,target_from_source:x},Vec3::ZERO).unwrap();assert_eq!(p.source_world_from_target_world.transform_point3(Vec3::new(2.,3.,4.)),Vec3::new(-2.,3.,4.));assert!(source_graph_context::ObjectBasis::from_document(&serde_json::json!({"extras":{"source_frame_binding":{}}})).is_err());println!("24 carriers Z + independent X + partial-declaration refusal PASS");}
"""
main=main.replace('@@BASE@@',base).replace('@@WORLD@@',world).replace('@@FIELDS@@',fields).replace('@@POLICY@@',policy)
(O/'src/main.rs').write_text(main,'utf-8')
seed=W/'house-common-collider-host-v718/Cargo.toml'; cargo=seed.read_text().replace('house-common-collider-host-v718','source-preview-basis-v719');(O/'Cargo.toml').write_text(cargo);(O/'Cargo.lock').write_bytes((seed.parent/'Cargo.lock').read_bytes())
receipt=W/'house-runtime-state-glb-v675/RESULT.v1.json'; files=[Path(__file__),ctx,preview,runtime,receipt,O/'src/main.rs',O/'Cargo.toml',O/'Cargo.lock']+[Path(row['glb']) for row in json.loads(receipt.read_text())['rows']];pins={str(p):sha(p) for p in files}
for f in [ctx,preview,runtime]:(O/f.name).write_bytes(f.read_bytes())
env=os.environ.copy();env.update(RUSTC=str(B/'rustc.exe'),RUSTDOC=str(B/'rustdoc.exe'),CARGO_INCREMENTAL='0',CARGO_TARGET_DIR=str(W/'firepit-static-glb-product-check-v2/target'),TEMP=str(O/'temp'),TMP=str(O/'temp'))
with (O/'cargo.log').open('wb') as log: code=subprocess.call([str(B/'cargo.exe'),'run','--locked','--offline','-j','1','--manifest-path',str(O/'Cargo.toml'),'--',str(receipt)],env=env,cwd=O,stdout=log,stderr=log)
state=dict(exit_code=code,status='preview_basis_verified' if code==0 else 'failed',input_pins=pins,unchanged_input_pins=all(sha(p)==h for p,h in pins.items()),studio_verified=False,full_import_increment=0)
(O/'RUN-RESULT.v1.json').write_text(json.dumps(state,indent=2));print({k:v for k,v in state.items() if k!='input_pins'})
