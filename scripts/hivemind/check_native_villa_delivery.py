"""Queue actual canonical engine admission after the current Studio build queue."""
import datetime
import json
import os
from pathlib import Path
import subprocess
import sys

from assemble_native_villa_delivery import read, require, sha

R = Path('E:/OpenRPG_Forge/OpenRPG_Forge-Rust')
W = Path('F:/OpenRPG_ImportWork')
ROOT = W / 'villa-native-scene-canonical-check-v1'
FRAME = W / 'villa-native-source-frame-bound-l04-villa-01-v1'
PRIOR = W / 'canonical-source-scene-geometry-integration-v1/RUN-RESULT.v1.json'
BIN = Path('C:/Users/karen/.rustup/toolchains/1.98.0-x86_64-pc-windows-msvc/bin')
PS = 'C:/Users/karen/.cache/codex-runtimes/codex-primary-runtime/dependencies/native/powershell/pwsh.exe'

NATIVE = r'''
use forge_assets::source_graph_glb::read_source_graph_glb;
use forge_engine::source_graph_scene_geometry::prepare_source_graph_scene_geometry;
use serde_json::{Value,json};
fn raw_hash<const N:usize>(values:&[[f32;N]])->String {
 let data:Vec<u8>=values.iter().flat_map(|row|row.iter().flat_map(|v|v.to_le_bytes())).collect();
 forge_core::bytes_sha256(&data).to_string()
}
fn changed_json(raw:&[u8],mutate:impl FnOnce(&mut Value))->Vec<u8>{
 let n=u32::from_le_bytes(raw[12..16].try_into().unwrap()) as usize;
 let mut doc:Value=serde_json::from_slice(&raw[20..20+n]).unwrap();mutate(&mut doc);
 let mut encoded=serde_json::to_vec(&doc).unwrap();while encoded.len()%4!=0{encoded.push(b' ')}
 let mut result=b"glTF".to_vec();result.extend(2u32.to_le_bytes());
 result.extend(((raw.len()-n+encoded.len()) as u32).to_le_bytes());
 result.extend((encoded.len() as u32).to_le_bytes());result.extend(b"JSON");
 result.extend(encoded);result.extend(&raw[20+n..]);result
}
fn main(){
 let path=std::env::args().nth(1).expect("Explicit immutable input manifest required");
 let rows:Vec<Value>=serde_json::from_slice(&std::fs::read(path).unwrap()).unwrap();
 let mut scenes=0;let mut geometries=0;let mut textures=0;let mut mips=0;let mut negative=0;
 for row in rows {
  let raw=std::fs::read(row["glb"].as_str().unwrap()).unwrap();
  assert_eq!(forge_core::bytes_sha256(&raw).to_string(),row["sha256"].as_str().unwrap());
  let glb=read_source_graph_glb(&raw).unwrap().expect("Source materials must be declared");
  let plan=prepare_source_graph_scene_geometry(&glb,None).unwrap();
  let basis=&glb.document()["extras"]["source_frame_binding"];
  let reflected=json!([-1,0,0,0,0,1,0,0,0,0,1,0,0,0,0,1]);
  assert_eq!(basis["target_object_from_source_object"],reflected);
  assert_eq!(basis["source_object_from_target_object"],reflected);
  for geometry in plan.source_geometries.values(){
   assert_eq!(raw_hash(&geometry.source.original_normals),basis["source_normal_sha256"].as_str().unwrap());
   assert_eq!(raw_hash(geometry.source.original_tangents.as_ref().unwrap()),basis["source_tangent_sha256"].as_str().unwrap());
   geometries+=1;
  }
  for (_,material) in &glb.materials{
   assert_eq!(material.schema_version,3);
   for texture in &material.textures {
    let storage=texture.imported_storage.as_ref().expect("No PNG-only fallback");
    storage.validate(&glb.views).unwrap();textures+=1;mips+=storage.mips.len();
   }
  }
  if scenes==0 {
   let (index,material)=glb.materials.iter().find(|(_,m)|!m.textures.is_empty()).unwrap();
   let storage=material.textures[0].imported_storage.as_ref().unwrap();
   let view=&glb.document()["bufferViews"][storage.storage.buffer_view];
   let json_len=u32::from_le_bytes(raw[12..16].try_into().unwrap()) as usize;
   let at=28+json_len+view["byteOffset"].as_u64().unwrap_or(0) as usize;
   let mut changed=raw.clone();changed[at]^=1;
   assert!(read_source_graph_glb(&changed).is_err());negative+=1;
   let changed=changed_json(&raw,|d|d["materials"][*index]["extensions"]["OPENRPG_source_graph"]["textures"][0]["imported_storage"]["capture_case_index"]=json!(999999));
   assert!(read_source_graph_glb(&changed).is_err());negative+=1;
  }
  scenes+=1;
  println!("{}",json!({"asset_id":row["asset_id"],"canonical_integrity_and_scene_admitted":true,"raw_captured_source_vectors_equal":true}));
 }
 assert!(scenes>0);assert_eq!(negative,2);
 println!("{}",json!({"canonical_scenes":scenes,"source_geometries":geometries,"texture_associations":textures,"mip_associations":mips,"negative_controls_passed":negative,"gpu_rendered":false,"studio_verified":false,"full_import":false}));
}
'''

def save(name, value):
    p = ROOT / name
    t = p.with_suffix('.json.tmp')
    t.write_text(json.dumps(value, indent=2), 'utf-8')
    os.replace(t, p)

def prepare():
    require(not ROOT.exists(), 'Prior canonical admission evidence preserved')
    ticks = subprocess.check_output([PS, '-NoProfile', '-Command',
        '(Get-Process -Id 1224 -ErrorAction Stop).StartTime.ToUniversalTime().Ticks'], text=True).strip()
    require(ticks.isdigit(), 'Current queue controller must be live')
    ROOT.mkdir()
    (ROOT / 'src').mkdir()
    dependencies = '\n'.join(f'{name}={{path="{(R / "crates" / name).as_posix()}"}}' for name in ['forge_engine', 'forge_assets', 'forge_core'])
    (ROOT / 'Cargo.toml').write_text('[package]\nname="openrpg-native-source-scene-delivery-check"\nversion="0.1.0"\nedition="2024"\n[workspace]\n[dependencies]\n' + dependencies + '\nserde_json="=1.0.151"\n', 'utf-8')
    (ROOT / 'src/main.rs').write_text(NATIVE, 'utf-8')
    save('PREPARED.v1.json', {'wait_pid': 1224, 'wait_birth_ticks': ticks,
        'input_pins': {str(p): sha(p) for p in [Path(__file__).resolve(), Path(__file__).with_name('assemble_native_villa_delivery.py'), ROOT / 'Cargo.toml', ROOT / 'src/main.rs']},
        'expected_scenes': 20, 'frame_receipt_required': str(FRAME / 'RUN-RESULT.v1.json')})
    with (ROOT / 'controller.log').open('xb') as log:
        child = subprocess.Popen([sys.executable, '-B', str(Path(__file__).resolve()), 'run'], cwd=ROOT,
            stdout=log, stderr=log, stdin=subprocess.DEVNULL, creationflags=subprocess.CREATE_NO_WINDOW)
    save('JOB.v1.json', {'pid': child.pid, 'launch_utc': datetime.datetime.now(datetime.timezone.utc).isoformat()})
    print(json.dumps({'queued_pid': child.pid, 'expected_scenes': 20, 'root': str(ROOT)}))

def run():
    state = {'status': 'waiting_for_existing_native_queue', 'canonical_scenes': 0,
        'gpu_rendered': False, 'studio_verified': False, 'full_import': False}
    save('RUN-RESULT.v1.json', state)
    try:
        prep = read(ROOT / 'PREPARED.v1.json')
        wait = "$ErrorActionPreference='Stop';while($true){$p=Get-Process -Id 1224 -ErrorAction SilentlyContinue;if(!$p -or $p.StartTime.ToUniversalTime().Ticks -ne " + prep['wait_birth_ticks'] + "L){break};$p.WaitForExit(45000)|Out-Null}"
        subprocess.run([PS, '-NoProfile', '-Command', wait], check=True, creationflags=subprocess.CREATE_NO_WINDOW)
        prior = read(PRIOR)
        require(prior['status'] == 'canonical_scene_geometry_native_verified' and prior['actual_tests'] == 6 and prior['all_input_pins_unchanged'], 'Canonical geometry API must be qualified')
        receipt_path = FRAME / 'RUN-RESULT.v1.json'
        receipt = read(receipt_path)
        require(receipt['status'] == 'native_original_source_frames_bound_and_reopened' and receipt['verified_scenes'] == prep['expected_scenes'], 'Closed 20-scene frame binding required')
        inputs = FRAME / 'INPUTS.v1.json'
        rows = read(inputs)
        require(len(rows) == prep['expected_scenes'] and {r['asset_id'] for r in rows} == {r['asset_id'] for r in receipt['rows']}, 'Input coverage mismatch')
        pins = {**prep['input_pins'], **prior['source_pins'], str(PRIOR): sha(PRIOR), str(receipt_path): sha(receipt_path), str(inputs): sha(inputs)}
        for row in rows:
            pins[row['glb']] = row['sha256']
        for p, expected in pins.items():
            require(sha(p) == expected, 'Canonical admission input changed: ' + p)
        environment = os.environ.copy()
        environment.update(CARGO_TARGET_DIR='F:/OpenRPG_Forge/relocated-work/Rust-target', CARGO_INCREMENTAL='0', CARGO_BUILD_JOBS='1',
            RUSTC=str(BIN / 'rustc.exe'), RUSTDOC=str(BIN / 'rustdoc.exe'), RUSTUP_TOOLCHAIN='1.98.0-x86_64-pc-windows-msvc', TEMP=str(ROOT), TMP=str(ROOT))
        environment['PATH'] = str(BIN) + os.pathsep + environment.get('PATH', '')
        for stage, args in [('lock', ['generate-lockfile', '--offline']),
                            ('native', ['run', '--locked', '--offline', '-j1', '--', str(inputs)])]:
            state['status'] = 'checking_canonical_native_delivery_' + stage
            save('RUN-RESULT.v1.json', state)
            with (ROOT / (stage + '.stdout.log')).open('xb') as out, (ROOT / (stage + '.stderr.log')).open('xb') as err:
                child = subprocess.Popen([str(BIN / 'cargo.exe'), *args], cwd=ROOT, env=environment, stdout=out, stderr=err, creationflags=subprocess.CREATE_NO_WINDOW)
                save('CARGO-JOB.v1.json', {'pid': child.pid, 'stage': stage})
                code = child.wait()
            state[stage + '_exit_code'] = code
            require(code == 0, 'Native admission failed; retained logs: ' + stage)
        results = [json.loads(line) for line in (ROOT / 'native.stdout.log').read_text('utf-8').splitlines() if line.startswith('{')]
        summary = [r for r in results if 'canonical_scenes' in r]
        require(len(summary) == 1 and summary[0]['canonical_scenes'] == 20 and summary[0]['negative_controls_passed'] == 2, 'Real 20-scene admission and controls required')
        require({r['asset_id'] for r in results if 'asset_id' in r} == {r['asset_id'] for r in rows}, 'Actual admitted IDs differ')
        for p, expected in pins.items():
            require(sha(p) == expected, 'Input changed during canonical admission')
        state.update(summary[0], status='20_native_source_scenes_canonical_integrity_and_geometry_admitted', input_pins=pins,
            all_input_pins_unchanged=True, stdout_sha256=sha(ROOT / 'native.stdout.log'))
    except Exception as error:
        state.update(status='refused_or_unverified', error=str(error))
    state['finished_utc'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
    save('RUN-RESULT.v1.json', state)
    if state['status'] == 'refused_or_unverified':
        raise SystemExit(1)

if __name__ == '__main__':
    {'prepare': prepare, 'run': run}[sys.argv[1]]()
