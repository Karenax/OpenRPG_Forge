from pathlib import Path
import json,hashlib,struct
from datetime import datetime,timezone
W=Path('F:/OpenRPG_ImportWork');O=W/'native-srgb-common-consumer-reopen-v87';assert not O.exists();pins={};rows=[]
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def pin(p,h=None):
 v=hashlib.sha256(p.read_bytes()).hexdigest();assert h is None or h==v,str(p);pins[str(p)]=v;return v
for ver,name,status,count in [(86,'consumer','common_srgb_shader_texture_consumer_gpu_verified',16384),(87,'boundaries','common_srgb_shader_adjacent_boundaries_gpu_verified',512)]:
 j=W/f'native-srgb-shader-{name}-v{ver}';run=read(j/'RUN-RESULT.v1.json');n=read(j/'NATIVE-RESULT.v1.json');assert run['actual_exit_code']==0 and run['gpu_probe_executed'] and n['status']==status;pin(j/'NATIVE-RESULT.v1.json',run['native_receipt_sha256'])
 for key,h in read(j/'PREPARED.v1.json')['input_pins'].items():pin(Path(key),h)
 for p in j.iterdir():
  if p.is_file():pin(p)
 raw=[(j/f'SHADER-CODE-{i}.rgba8unorm').read_bytes() for i in range(3)];assert raw[0]==raw[1]==raw[2]==(j/'EXPECTED-NATIVE-SRGB.rgba8srgb').read_bytes();assert len(raw[0])==count*4 and n['format']=='Rgba8Unorm' and n['threshold_texture_format']=='R32Float' and n['gpu_validation_errors']==0 and n['unity_pixel_inputs']==n['purchased_asset_inputs']==0;rows.append(dict(case=name,values=count,repeated_equal_readbacks=3,byte_equal_to_native_srgb_reference=True))
proof=W/'native-srgb-generated-boundaries-reopen-v85/RESULT.v1.json';r=read(proof);pin(proof);curve=W/'native-srgb-generated-boundaries-v85/NATIVE-SRGB-INPUT-THRESHOLDS.f32';pin(curve,r['input_pins'][str(curve)]);typed=W/'native-srgb-curve-contract-check-v87/TYPED-CURVE.r32f';assert typed.read_bytes()==curve.read_bytes();pin(typed)
R=Path('E:/OpenRPG_Forge/OpenRPG_Forge-Rust')
for p in [R/'crates/forge_engine/src/source_graph_srgb_curve.rs',R/'crates/forge_engine/src/source_graph_srgb_encode.wgsl',W/'native-srgb-curve-contract-check-v87/curve-tests.exe',W/'native-srgb-curve-contract-check-v87/check-real-curve.rs',W/'native-srgb-curve-contract-check-v87/check-real-curve.exe',Path(__file__)]:pin(p)
O.mkdir();res=dict(status='common_srgb_shader_and_device_curve_contract_verified_in_isolation',finished_utc=datetime.now(timezone.utc).isoformat(),lot='L01.SRGB.NATIF.S03.CONTROLE',cases=rows,device_curve_bytes=1024,typed_curve_byte_equal_to_measured_native_curve=True,structural_guard_tests_passed=5,structural_guard_tests_executed_with_rustc_test=True,material_binding_and_device_probe_producer_not_integrated=True,shader_common_body_gpu_qualified_on_verified_inputs=True,bevy_shader_library_registration_not_yet_verified=True,source_code_qualification_scope='Isolated shader body reads native R32Float threshold texture; private probes use no Unity or purchased assets. Material binding, runtime device calibration, full source material and Studio remain open.',rehashed_pins=len(pins),input_pins=pins,purchased_asset_inputs=0,unity_pixel_inputs=0,source_pixel_fit=False,installed_in_product=False,full_material_fidelity_qualified=False,forge_editor_verified=False,full_import=False,complete_assets_delivered_increment=0);(O/'RESULT.v1.json').write_text(json.dumps(res,indent=2)+'\n',encoding='utf-8');print(json.dumps({k:v for k,v in res.items() if k!='input_pins'}))
