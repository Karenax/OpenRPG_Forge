"""Prepare/run a source-pinned native scene assembly check entirely under F:."""
import datetime
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import sys

REPO = Path(r"E:\OpenRPG_Forge\OpenRPG_Forge-Rust")
WORK = Path(r"F:\OpenRPG_ImportWork")
ROOT = WORK / "source-graph-scene-geometry-adapter-v2"
CACHE = WORK / "source-graph-private-boundary-v1/target-rust198-explicit-domain-v8"
CARGO = Path(r"C:\Users\karen\.rustup\toolchains\1.98.0-x86_64-pc-windows-msvc\bin\cargo.exe")
CANDIDATE = REPO / "scripts/hivemind/candidates/source_graph_scene_geometry.v2.rs"

NATIVE_CHECK = r'''
mod source_graph_scene;
mod source_graph_scene_geometry;
#[cfg(test)]
mod scene_geometry_checks {
 use super::source_graph_scene_geometry::*;
 use forge_assets::source_graph_glb::{read_source_graph_glb, SourceGraphGlb};
 use serde_json::{Value,json};
 fn inputs()->Vec<Value>{serde_json::from_str(include_str!(concat!(env!("CARGO_MANIFEST_DIR"),"/INPUTS.v1.json"))).unwrap()}
 fn load(row:&Value)->Vec<u8>{let b=std::fs::read(row["glb"].as_str().unwrap()).unwrap();assert_eq!(forge_core::bytes_sha256(&b).to_string(),row["sha256"].as_str().unwrap());b}
 fn first()->Vec<u8>{load(&inputs()[0])}
 fn changed(raw:&[u8],edit:impl FnOnce(&mut Value))->Vec<u8>{
  let n=u32::from_le_bytes(raw[12..16].try_into().unwrap()) as usize;
  let mut doc:Value=serde_json::from_slice(&raw[20..20+n]).unwrap();edit(&mut doc);
  let mut j=serde_json::to_vec(&doc).unwrap();while j.len()%4!=0{j.push(b' ')}
  let mut b=Vec::new();b.extend_from_slice(b"glTF");b.extend_from_slice(&2u32.to_le_bytes());
  b.extend_from_slice(&((20+j.len()+raw.len()-20-n) as u32).to_le_bytes());
  b.extend_from_slice(&(j.len() as u32).to_le_bytes());b.extend_from_slice(b"JSON");b.extend_from_slice(&j);b.extend_from_slice(&raw[20+n..]);b
 }
 fn raw_bits<const N:usize>(glb:&SourceGraphGlb<'_>,key:SourcePrimitiveKey,name:&str)->Vec<[u32;N]>{
  let d=glb.document();let a=&d["meshes"][key.mesh]["primitives"][key.primitive]["attributes"];
  let accessor=&d["accessors"][a[name].as_u64().unwrap() as usize];
  let vi=accessor["bufferView"].as_u64().unwrap() as usize;
  let offset=accessor["byteOffset"].as_u64().unwrap_or(0) as usize;
  let stride=d["bufferViews"][vi]["byteStride"].as_u64().map_or(N*4,|v|v as usize);
  (0..accessor["count"].as_u64().unwrap() as usize).map(|v|std::array::from_fn(|c|{
   let at=offset+v*stride+c*4;u32::from_le_bytes(glb.views[vi][at..at+4].try_into().unwrap())
  })).collect()
 }
 fn bits<const N:usize>(v:&[[f32;N]])->Vec<[u32;N]>{v.iter().map(|a|a.map(f32::to_bits)).collect()}
 #[test]
 fn actual_pinned_scenes_preserve_all_primitive_bindings_and_buffers(){
  let mut cases=0;let mut instances=0;let mut geometries=0;let mut absent=0;
  for row in inputs(){
   let b=load(&row);let glb=read_source_graph_glb(&b).unwrap().unwrap();
   let plan=prepare_source_graph_scene_geometry(&glb,None).unwrap();
   let d=glb.document();let mut expected=0;
   for node in &plan.hierarchy.nodes {if let Some(mesh)=node.mesh{expected+=d["meshes"][mesh]["primitives"].as_array().unwrap().len()}}
   assert_eq!(expected,plan.instances.len());
   for instance in &plan.instances {
    let declared=&d["meshes"][instance.geometry.mesh]["primitives"][instance.geometry.primitive];
    assert!(plan.hierarchy.nodes.iter().any(|n|n.source_node==instance.source_node&&n.mesh==Some(instance.geometry.mesh)));
    if let ScenePrimitiveRoute::SourceGraph{material}=instance.route {
     assert_eq!(declared["material"].as_u64().unwrap() as usize,material);
     assert_eq!(plan.source_geometries[&instance.geometry].source.material_index,material);
    }
   }
   for (key,g) in &plan.source_geometries {
    assert_eq!(bits(&g.positions),raw_bits::<3>(&glb,*key,"POSITION"));
    assert_eq!(bits(&g.normals),raw_bits::<3>(&glb,*key,"NORMAL"));
    assert_eq!(bits(&g.uv0),raw_bits::<2>(&glb,*key,"TEXCOORD_0"));
    assert_eq!(bits(&g.source.original_normals),raw_bits::<3>(&glb,*key,"_OPENRPG_SOURCE_NORMAL"));
    assert_eq!(bits(&g.source.original_uv0),raw_bits::<4>(&glb,*key,"_OPENRPG_SOURCE_UV0"));
    if let Some(v)=&g.tangents{assert_eq!(bits(v),raw_bits::<4>(&glb,*key,"TANGENT"));}
    if let Some(v)=&g.source.original_tangents{assert_eq!(bits(v),raw_bits::<4>(&glb,*key,"_OPENRPG_SOURCE_TANGENT"));}
    if let Some(v)=&g.source.original_colors{assert_eq!(bits(v),raw_bits::<4>(&glb,*key,"_OPENRPG_SOURCE_COLOR"));}
   }
   cases+=1;instances+=plan.instances.len();geometries+=plan.source_geometries.len();absent+=plan.absent_original_tangents().count();
   println!("{}",json!({"asset_id":row["asset_id"],"primitive_instances":plan.instances.len(),"shared_geometries":plan.source_geometries.len(),"raw_float_bits_equal":true}));
  }
  assert_eq!(cases,57);
  println!("{}",json!({"actual_scenes":cases,"primitive_instances":instances,"shared_geometries":geometries,"absent_original_tangents":absent,"gpu_rendered":false,"studio_verified":false}));
 }
 #[test]
 fn shared_mesh_and_mixed_materials_preserve_instance_routes(){
  let b=first();let glb=read_source_graph_glb(&b).unwrap().unwrap();let initial=prepare_source_graph_scene_geometry(&glb,None).unwrap();
  let mesh=initial.instances[0].geometry.mesh;let primitives=glb.document()["meshes"][mesh]["primitives"].as_array().unwrap().len();
  let repeated=changed(&b,|d|{let index=d["nodes"].as_array().unwrap().len();d["nodes"].as_array_mut().unwrap().push(json!({"mesh":mesh,"translation":[12,34,56]}));let scene=d["scene"].as_u64().unwrap() as usize;d["scenes"][scene]["nodes"].as_array_mut().unwrap().push(index.into());});
  let glb=read_source_graph_glb(&repeated).unwrap().unwrap();let plan=prepare_source_graph_scene_geometry(&glb,None).unwrap();
  assert_eq!(plan.instances.len(),initial.instances.len()+primitives);assert_eq!(plan.source_geometries.len(),initial.source_geometries.len());
  let copies=plan.hierarchy.nodes.iter().filter(|n|n.mesh==Some(mesh)).count();
  let mixed=changed(&repeated,|d|{let material=d["materials"].as_array().unwrap().len();d["materials"].as_array_mut().unwrap().push(json!({"name":"ordinary-original-material"}));let mut p=d["meshes"][mesh]["primitives"][0].clone();p["material"]=material.into();d["meshes"][mesh]["primitives"].as_array_mut().unwrap().push(p);});
  let glb=read_source_graph_glb(&mixed).unwrap().unwrap();let mixed=prepare_source_graph_scene_geometry(&glb,None).unwrap();
  assert_eq!(mixed.instances.len(),plan.instances.len()+copies);assert_eq!(mixed.source_geometries.len(),plan.source_geometries.len());
  assert_eq!(mixed.instances.iter().filter(|p|matches!(p.route,ScenePrimitiveRoute::HostGltf{..})).count(),copies);
 }
 #[test]
 fn bad_materials_and_missing_source_streams_refuse_without_replacement(){
  let b=first();let glb=read_source_graph_glb(&b).unwrap().unwrap();let plan=prepare_source_graph_scene_geometry(&glb,None).unwrap();let key=plan.instances[0].geometry;
  for bad in [json!(-1),Value::Null,json!(999999)] {
   let raw=changed(&b,|d|d["meshes"][key.mesh]["primitives"][key.primitive]["material"]=bad);
   let glb=read_source_graph_glb(&raw).unwrap().unwrap();assert!(prepare_source_graph_scene_geometry(&glb,None).is_err());
  }
  let raw=changed(&b,|d|{d["meshes"][key.mesh]["primitives"][key.primitive]["attributes"].as_object_mut().unwrap().remove("_OPENRPG_SOURCE_UV0");});
  let glb=read_source_graph_glb(&raw).unwrap().unwrap();assert!(prepare_source_graph_scene_geometry(&glb,None).is_err());
 }
 #[test]
 fn authored_missing_tangent_stays_absent(){
  let b=first();let glb=read_source_graph_glb(&b).unwrap().unwrap();let plan=prepare_source_graph_scene_geometry(&glb,None).unwrap();let key=plan.instances[0].geometry;
  let raw=changed(&b,|d|{d["meshes"][key.mesh]["primitives"][key.primitive]["attributes"].as_object_mut().unwrap().remove("_OPENRPG_SOURCE_TANGENT");});
  let glb=read_source_graph_glb(&raw).unwrap().unwrap();let plan=prepare_source_graph_scene_geometry(&glb,None).unwrap();
  assert!(plan.source_geometries[&key].source.original_tangents.is_none());assert!(plan.absent_original_tangents().any(|k|k==key));
 }
 #[test]
 fn edited_public_material_list_cannot_fall_back_to_ordinary_gltf(){
  let b=first();let mut glb=read_source_graph_glb(&b).unwrap().unwrap();glb.materials.clear();
  let error=prepare_source_graph_scene_geometry(&glb,None).err().unwrap();
  assert!(error.contains("identities differ"));
 }
 #[test]
 fn edited_public_contract_cannot_override_the_embedded_source(){
  let b=first();let mut glb=read_source_graph_glb(&b).unwrap().unwrap();glb.materials[0].1.state.render_queue+=1;
  let error=prepare_source_graph_scene_geometry(&glb,None).err().unwrap();
  assert!(error.contains("contract changed"));
 }
}
fn main(){}
'''

def now():
    return datetime.datetime.now(datetime.timezone.utc).isoformat()

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def save(name, value):
    target = ROOT / name
    temporary = target.with_suffix(target.suffix + f".{os.getpid()}.tmp")
    temporary.write_text(json.dumps(value, indent=2, ensure_ascii=False), encoding="utf-8")
    os.replace(temporary, target)

def prepare():
    if ROOT.exists():
        raise RuntimeError("existing candidate work preserved; inspect its exact job before resuming")
    source = WORK / "source-graph-scene-native-tests-v3/INPUTS.v1.json"
    # The existing 57-case inventory is immutable and carries each GLB hash.
    source = source if source.exists() else WORK / "source-graph-scene-native-tests-v3/CANDIDATES.v1.json"
    rows = json.loads(source.read_text(encoding="utf-8-sig"))
    if len(rows) != 57:
        raise RuntimeError("expected the exact 57 existing scene cases")
    for row in rows:
        if sha(Path(row["glb"])) != row["sha256"]:
            raise RuntimeError(f"changed GLB: {row['asset_id']}")
    scene = REPO / "crates/forge_engine/src/source_graph_scene.rs"
    pins = {str(p): sha(p) for p in [CANDIDATE, scene, source, Path(__file__).resolve()]}
    for crate in ["forge_assets", "forge_core"]:
        base = REPO / "crates" / crate
        for p in [base / "Cargo.toml", *sorted((base / "src").rglob("*.rs"))]:
            pins[str(p)] = sha(p)
    for p in [REPO / "Cargo.toml", REPO / "Cargo.lock"]:
        pins[str(p)] = sha(p)
    (ROOT / "src").mkdir(parents=True)
    (ROOT / "src/source_graph_scene.rs").write_bytes(scene.read_bytes())
    (ROOT / "src/source_graph_scene_geometry.rs").write_bytes(CANDIDATE.read_bytes())
    (ROOT / "src/main.rs").write_text(NATIVE_CHECK, encoding="utf-8")
    (ROOT / "INPUTS.v1.json").write_bytes(source.read_bytes())
    (ROOT / "Cargo.toml").write_text('''[package]
name="openrpg-source-scene-geometry-check"
version="0.1.0"
edition="2024"
[workspace]
[dependencies]
bevy={version="=0.19.1",default-features=false,features=["std","bevy_asset","bevy_mesh","bevy_camera"]}
forge_assets={path="E:/OpenRPG_Forge/OpenRPG_Forge-Rust/crates/forge_assets"}
forge_core={path="E:/OpenRPG_Forge/OpenRPG_Forge-Rust/crates/forge_core"}
serde_json="=1.0.151"
''', encoding="utf-8")
    for p in sorted(ROOT.rglob("*.rs")) + [ROOT / "Cargo.toml", ROOT / "INPUTS.v1.json"]:
        pins[str(p)] = sha(p)
    save("PREPARED.v1.json", {"source_pins": pins, "actual_scene_count": 57, "utc": now(), "canonical_installed": False})
    with (ROOT / "controller.log").open("ab", buffering=0) as log:
        child = subprocess.Popen([sys.executable, str(Path(__file__).resolve()), "run"], cwd=ROOT,
            stdin=subprocess.DEVNULL, stdout=log, stderr=log, creationflags=subprocess.CREATE_NO_WINDOW)
    save("JOB.v1.json", {"controller_pid": child.pid, "created_utc": now(), "script": str(Path(__file__).resolve())})
    print(json.dumps({"prepared": True, "controller_pid": child.pid, "root": str(ROOT)}))

def run():
    proof = json.loads((ROOT / "PREPARED.v1.json").read_text(encoding="utf-8"))
    pins = proof["source_pins"]
    result = {"status": "running_native_scene_geometry_checks", "started_utc": now(),
              "canonical_installed": False, "gpu_rendered": False, "studio_verified": False,
              "new_qualified_prefabs": 0, "full_import": False}
    save("RUN-RESULT.v1.json", result)
    try:
        changed = [p for p, h in pins.items() if sha(Path(p)) != h]
        if changed:
            raise RuntimeError(f"changed prepared inputs: {changed}")
        env = os.environ.copy()
        env["CARGO_TARGET_DIR"] = str(CACHE)
        env["CARGO_BUILD_JOBS"] = "1"
        # F: is exFAT: incremental hard-link reuse falls back to copying.
        # Keep all work on F: while avoiding this cache copy for local crates.
        env["CARGO_INCREMENTAL"] = "0"
        toolchain = CARGO.parent
        env["RUSTC"] = str(toolchain / "rustc.exe")
        env["RUSTDOC"] = str(toolchain / "rustdoc.exe")
        env["RUSTUP_TOOLCHAIN"] = "1.98.0-x86_64-pc-windows-msvc"
        env["PATH"] = str(toolchain) + os.pathsep + env.get("PATH", "")
        for stage, args in [("lock", ["generate-lockfile", "--offline"]),
            ("native", ["test", "--locked", "--offline", "-j1", "scene_geometry_checks::", "--", "--nocapture"])]:
            with (ROOT / f"{stage}.stdout.log").open("wb") as out, (ROOT / f"{stage}.stderr.log").open("wb") as err:
                process = subprocess.Popen([str(CARGO), *args], cwd=ROOT, env=env,
                    stdin=subprocess.DEVNULL, stdout=out, stderr=err, creationflags=subprocess.CREATE_NO_WINDOW)
                save("NATIVE-JOB.v1.json", {"pid": process.pid, "stage": stage, "created_utc": now(), "command": [str(CARGO), *args]})
                code = process.wait()
            result[f"{stage}_exit_code"] = code
            if code:
                raise RuntimeError(f"{stage} command failed with exit {code}; retained logs")
            if stage == "lock":
                pins[str(ROOT / "Cargo.lock")] = sha(ROOT / "Cargo.lock")
        output = (ROOT / "native.stdout.log").read_text(encoding="utf-8", errors="strict")
        summary = re.search(r"test result: ok\. (\d+) passed; 0 failed", output)
        scene_rows = [json.loads(line) for line in output.splitlines() if line.startswith('{')]
        totals = [row for row in scene_rows if row.get("actual_scenes") == 57]
        if not summary or int(summary.group(1)) != 6 or len(totals) != 1:
            raise RuntimeError("six actual candidate tests and the 57-scene receipt not proven")
        changed = [p for p, h in pins.items() if sha(Path(p)) != h]
        changed += [row["glb"] for row in json.loads((ROOT / "INPUTS.v1.json").read_text(encoding="utf-8-sig")) if sha(Path(row["glb"])) != row["sha256"]]
        result["changed_inputs"] = changed
        if changed:
            raise RuntimeError("inputs changed during verification; success withheld")
        result.update(status="native_scene_geometry_candidate_verified", actual_tests=6, scene_summary=totals[0],
            source_pins=pins, stdout_sha256=sha(ROOT / "native.stdout.log"), stderr_sha256=sha(ROOT / "native.stderr.log"))
    except Exception as error:
        result.update(status="refused", error=str(error))
    result["finished_utc"] = now()
    save("RUN-RESULT.v1.json", result)
    print(json.dumps(result))
    if result["status"] == "refused":
        raise SystemExit(1)

if __name__ == "__main__":
    {"prepare": prepare, "run": run}[sys.argv[1]]()
