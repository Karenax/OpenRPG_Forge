from pathlib import Path
import json,hashlib,struct,bisect
from datetime import datetime,timezone
W=Path("F:/OpenRPG_ImportWork");O=W/"native-srgb-producer-v88";Q=W/"native-srgb-producer-reopen-v88"
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
pins={}
def pin(p,expected=None):
 h=sha(p)
 if expected is not None:assert h==expected,str(p)
 pins[str(p)]=h
 return p.read_bytes()
run=json.loads(pin(O/"RUN-RESULT.v1.json"));assert run["actual_exit_code"]==0 and run["gpu_probe_executed"]
native=json.loads(pin(O/"NATIVE-RESULT.v1.json",run["native_receipt_sha256"]));assert native["gpu_errors"]==[] and native["wrong_device_rejected"]
prep=json.loads(pin(O/"PREPARED.v1.json"))
for key,h in prep["input_pins"].items():pin(Path(key),h)
oldpath=W/"native-srgb-generated-boundaries-reopen-v85/RESULT.v1.json";old=json.loads(pin(oldpath))
for key,h in old["input_pins"].items():pin(Path(key),h)
for key in ["adapter","backend","device_info"]:assert native[key]==old[key],key
reference=W/"native-srgb-generated-boundaries-v85/NATIVE-SRGB-INPUT-THRESHOLDS.f32"
actual=pin(O/"PRODUCER-CURVE.r32f");expected=pin(reference,old["input_pins"][str(reference)]);assert actual==expected and len(actual)==1024
thresholds=struct.unpack("<256f",actual)
ramp=W/"native-srgb-generated-transfer-ramp-v84"
values=struct.unpack("<16384f",pin(ramp/"GENERATED-VALUES.f32"))
raws=list(ramp.glob("*.rgba8srgb"));assert len(raws)==3
raw=pin(raws[0]);assert len(raw)==65536
for p in raws[1:]:assert pin(p)==raw
for i,x in enumerate(values):
 code=bisect.bisect_right(thresholds,min(1.,max(0.,x)))-1
 assert raw[i*4:i*4+4]==bytes([code,code,code,255]),i
for p in O.iterdir():
 if p.is_file():pin(p)
pin(Path(__file__))
Q.mkdir(exist_ok=False)
r={"status":"native_srgb_common_producer_verified_in_isolation","lot":"L01.SRGB.NATIF.S03.PRODUCTEUR","finished_utc":datetime.now(timezone.utc).isoformat(),"actual_exit_code":0,"curve_identical_to_verified_native_boundaries":True,"threshold_bytes":1024,"independent_native_ramp_values_equal":16384,"calibration_file_inputs":0,"purchased_asset_inputs":0,"unity_pixel_inputs":0,"wrong_device_rejected":True,"gpu_errors":[],"probe_elapsed_seconds":native["elapsed_seconds"],"input_pins":pins,"material_binding_integrated":False,"studio_verified":False,"additional_full_assets_qualified":0,"full_import":False}
(Q/"RESULT.v1.json").write_text(json.dumps(r,indent=2),encoding="utf-8")
print(json.dumps({"status":r["status"],"pins":len(pins),"proof":str(Q/"RESULT.v1.json")}))
