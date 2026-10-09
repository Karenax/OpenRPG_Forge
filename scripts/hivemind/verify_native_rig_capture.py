"""Reopen rig data without dropping influences, optional vertex streams, curves or events."""
import hashlib
import json
import math
from pathlib import Path
import struct
import sys

ROOT=Path(r"F:\OpenRPG_ImportWork\unity-medieval-rig-source-capture-v1")

def sha(path):
 with Path(path).open("rb") as source:return hashlib.file_digest(source,"sha256").hexdigest()
def read(path):return json.loads(Path(path).read_text("utf-8-sig"))
def floats(path,count):
 data=Path(path).read_bytes()
 if len(data)!=count*4:raise ValueError("Wrong native buffer size: "+str(path))
 values=struct.unpack("<"+str(count)+"f",data)
 if not all(math.isfinite(value) for value in values):raise ValueError("Nonfinite native geometry")
 return values

def skin(counts,weights,vertices,bones):
 if len(counts)!=vertices or any(count==0 for count in counts) or sum(counts)*8!=len(weights):raise ValueError("Incomplete per-vertex influences")
 cursor=0;maximum_error=0.0
 for count in counts:
  total=0.0
  for _ in range(count):
   joint,weight=struct.unpack_from("<if",weights,cursor);cursor+=8
   if not 0<=joint<bones or not math.isfinite(weight) or not 0<=weight<=1:raise ValueError("Invalid source influence")
   total+=weight
  error=abs(total-1)
  # Sum of count rounded normalized float32 weights: count half-ULPs at magnitude <=1.
  if error>count*2**-24:raise ValueError("Native weights not normalized; no repair or renormalization")
  maximum_error=max(maximum_error,error)
 return {"influences":len(weights)//8,"maximum_normalized_sum_error":maximum_error,"normalization_applied":False}

def self_test():
 valid=struct.pack("<if",0,1.0)
 assert skin(b"\x01",valid,1,1)["influences"]==1
 failures=[(b"",valid,1,1),(b"\x02",valid,1,1),(b"\x01",struct.pack("<if",1,1.0),1,1),(b"\x01",struct.pack("<if",0,-0.1),1,1),(b"\x01",struct.pack("<if",0,0.5),1,1),(b"\x01",struct.pack("<if",0,float("nan")),1,1)]
 for case in failures:
  try:skin(*case)
  except ValueError:continue
  raise AssertionError("Malformed rig influence accepted")
 print(json.dumps({"positive_control_passed":True,"negative_controls_passed":6,"native_capture_verified":False}))

def verify():
 prepared=read(ROOT/"PREPARED.v1.json");run=read(ROOT/"RUN-RESULT.v1.json");request=read(ROOT/"manifest.json")
 if run["status"]!="native_rig_source_captured_independent_reopening_remaining" or run["actual_unity_exit_code"]!=0:raise ValueError("Actual complete native capture absent")
 pins=dict(prepared["input_pins"])
 for path,expected in pins.items():
  if sha(path)!=expected:raise ValueError("Changed native source input")
 output=ROOT/"export";result=read(output/"RESULT.json")
 if result["guid"]!=request["expected_guid"] or result["file_id"]!=request["expected_file_id"] or result["mesh_name"]!=request["expected_name"] or result["asset_id"]!=request["asset_id"]:raise ValueError("Exact source identity changed")
 if result["unity_version"]!="6000.5.8f1" or result["rig_flattened"] or result["animation_events_executed"] or result["blend_shapes"]!=0:raise ValueError("Unsupported or altered rig capture")
 n=result["vertices"];bones=result["bind_poses"]
 if type(n)!=int or n<=0 or bones<=0 or len(result["bone_nodes"])!=bones:raise ValueError("Incomplete rig counts")
 nodes=result["hierarchy"]
 for index,node in enumerate(nodes):
  if node["index"]!=index or node["parent"]>=index or node["parent"] < -1:raise ValueError("Invalid/cyclic native hierarchy")
  for name in ["position","rotation","scale","world"]:
   if not all(math.isfinite(value) for value in node[name].values()):raise ValueError("Nonfinite native transform")
 if any(not 0<=index<len(nodes) for index in result["bone_nodes"]) or not 0<=result["renderer_node"]<len(nodes):raise ValueError("Bone outside captured hierarchy")
 expected_files={"RESULT.json","positions.f32","normals.f32","tangents.f32","colors.f32","bindposes.f32","bones-per-vertex.u8","boneweights.i32-f32"}
 floats(output/"positions.f32",n*3)
 for name,size in [("normals",3),("tangents",4),("colors",4)]:
  if result[name] not in [0,n]:raise ValueError("Truncated optional source stream")
  floats(output/(name+".f32"),result[name]*size)
 if len(result["uv_counts"])!=8:raise ValueError("Incomplete UV declaration")
 for index,count in enumerate(result["uv_counts"]):
  if count not in [0,n]:raise ValueError("Truncated UV stream")
  name=f"uv{index}.f32";floats(output/name,count*4);expected_files.add(name)
 for index in range(result["submeshes"]):
  name=f"submesh-{index}.i32";data=(output/name).read_bytes();expected_files.add(name)
  if len(data)%12:raise ValueError("Nontriangle/truncated source indices")
  if any(not 0<=value<n for value in struct.unpack("<"+str(len(data)//4)+"i",data)):raise ValueError("Out-of-range source index")
 floats(output/"bindposes.f32",bones*16)
 influence=skin((output/"bones-per-vertex.u8").read_bytes(),(output/"boneweights.i32-f32").read_bytes(),n,bones)
 if len(result["probe_poses"])!=2:raise ValueError("Missing native skin probe poses")
 for index,pose in enumerate(result["probe_poses"]):
  if pose["vertices"]!=n or len(pose["joint_world"])!=bones:raise ValueError("Truncated native skin deformation")
  for value in [pose["mesh_world"],*pose["joint_world"]]:
   if not all(math.isfinite(component) for component in value.values()):raise ValueError("Nonfinite pose matrix")
  for name,size,count in [("positions",3,n),("normals",3,result["normals"]),("tangents",4,result["tangents"])]:
   filename=f"pose-{index}-{name}.f32";floats(output/filename,count*size);expected_files.add(filename)
 for clip in result["clips"]:
  if not clip["guid"] or not clip["file_id"] or not math.isfinite(clip["length"]) or not math.isfinite(clip["frame_rate"]):raise ValueError("Incomplete clip identity/timing")
  for curve in clip["curves"]:
   if not all(key in curve for key in ["path","type","property","pre_wrap","post_wrap","keys"]):raise ValueError("Incomplete curve binding")
   for key in curve["keys"]:
    if not all(math.isfinite(key[name]) for name in ["time","value","in_weight","out_weight"]):raise ValueError("Invalid animation key")
    if any(type(key[name])!=int or not 0<=key[name]<=0xffffffff for name in ["in_tangent_bits","out_tangent_bits"]):raise ValueError("Tangent bit representation incomplete")
 if {path.name for path in output.iterdir()}!=expected_files:raise ValueError("Unexpected or missing native buffers")
 for path in output.iterdir():pins[str(path)]=sha(path)
 for path,expected in pins.items():
  if sha(path)!=expected:raise ValueError("Source/capture changed during reopening")
 report={"schema":"openrpg.complete-native-rig-source-reopen.v1","source_guid":result["guid"],"source_file_id":result["file_id"],"vertices":n,"bind_poses":bones,"hierarchy_nodes":len(nodes),"clips_recorded":len(result["clips"]),"native_probe_poses":2,"influence_check":influence,"input_pins":pins,"rig_flattened":False,"native_capture_rerun":False,"skin_deformation_parity_verified":False,"skin_glb_converted":False,"source_shader_render_parity_qualified":False,"full_import":False}
 with (ROOT/"REOPEN.v1.json").open("x",encoding="utf-8") as file:json.dump(report,file,indent=2,allow_nan=False)
 print(json.dumps({key:value for key,value in report.items() if key!="input_pins"}))

if __name__=="__main__":self_test() if "--self-test" in sys.argv else verify()
