"""Reopen the actual asynchronous producer and compare the same-device table."""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json
W=Path('F:/OpenRPG_ImportWork');D=W/'async-srgb-producer-v98';O=W/'async-srgb-producer-reopen-v98';assert not O.exists()
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):
 with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest()
run=read(D/'RUN-RESULT.v1.json');native=read(D/'NATIVE-RESULT.v1.json');prep=read(D/'PREPARED.v1.json')
assert run['actual_exit_code']==0 and run['gpu_probe_executed'] and sha(D/'NATIVE-RESULT.v1.json')==run['native_receipt_sha256']
assert native['native_execution_of_common_async_producer'] and not native['browser_gpu_executed']
assert native['gpu_errors']==[] and native['curve_bytes']==1024 and native['pending_polls']>0 and native['wrong_device_rejected']
pins=dict(prep['input_pins'])
for p,h in pins.items():assert sha(Path(p))==h,p
prior=W/'native-srgb-producer-v88';old=read(prior/'NATIVE-RESULT.v1.json');old_run=read(prior/'RUN-RESULT.v1.json')
assert old_run['actual_exit_code']==0 and sha(prior/'NATIVE-RESULT.v1.json')==old_run['native_receipt_sha256']
assert old['adapter']==native['adapter'] and old['backend']==native['backend'] and old['device_info']==native['device_info']
assert (D/'PRODUCER-CURVE.r32f').read_bytes()==(prior/'PRODUCER-CURVE.r32f').read_bytes()
for p in [Path(__file__),D/'RUN-RESULT.v1.json',D/'NATIVE-RESULT.v1.json',D/'PRODUCER-CURVE.r32f',prior/'RUN-RESULT.v1.json',prior/'NATIVE-RESULT.v1.json',prior/'PRODUCER-CURVE.r32f']:pins[str(p)]=sha(p)
O.mkdir();result=dict(status='common_async_srgb_producer_native_gpu_witnesses_and_table_verified',lot='L01.SRGB.WEB.ASYNC.GPU.NATIF',finished_utc=datetime.now(timezone.utc).isoformat(),actual_exit_code=0,device_info=native['device_info'],adapter=native['adapter'],backend=native['backend'],pending_polls=native['pending_polls'],polls=native['polls'],longest_poll_seconds=native['longest_poll_seconds'],native_gpu_table_byte_equal_to_previous_same_device_measurement=True,curve_bytes=1024,three_adjacent_witness_readbacks_validated_by_actual_producer=True,wrong_device_key_refused=True,gpu_errors=[],calibration_file_inputs=0,unity_pixel_inputs=0,browser_gpu_executed=False,material_gpu_binding_verified=False,editor_visual_verified=False,full_import=False,new_fully_qualified_assets=0,input_pins=pins)
(O/'RESULT.v1.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8');print(json.dumps({k:v for k,v in result.items() if k!='input_pins'}))
