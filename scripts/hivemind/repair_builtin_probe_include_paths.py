"""Make the queued shader's includes self-contained; retain original preparation and sources."""
import hashlib
import json
import os
from pathlib import Path

ROOT=Path(r"F:\OpenRPG_ImportWork\unity-builtin-normal-constant-reference-v1")
CORE=Path(r"F:\OpenRPG_ImportWork\unity-dungeon-original-shadergraph-compiler-v1\project\Library\PackageCache\com.unity.render-pipelines.core@a4c9a9793789\ShaderLibrary")

def sha(path):return hashlib.sha256(Path(path).read_bytes()).hexdigest()

def main():
 state=json.loads((ROOT/"RUN-RESULT.v1.json").read_text("utf-8"))
 if state["status"]!="waiting_for_prior_hdrp_and_mesh_capture" or state["native_unity_launched"]:
  raise RuntimeError("Preparation repair withheld after native launch")
 pointer=ROOT/"PREPARED.v1.json";previous_bytes=pointer.read_bytes();prepared=json.loads(previous_bytes)
 for path,expected in prepared["input_pins"].items():
  if sha(path)!=expected:raise RuntimeError("Queued input already changed")
 prefix="Packages/com.unity.render-pipelines.core/ShaderLibrary/"
 destination=ROOT/"project/Assets/Probe/CoreShaderLibrary"
 files=["Common.hlsl","Packing.hlsl","API/D3D11.hlsl","API/Validate.hlsl","Macros.hlsl","Random.hlsl","CommonDeprecated.hlsl"]
 rows=[]
 for name in files:
  source=CORE/name;target=destination/name;target.parent.mkdir(parents=True,exist_ok=True)
  original=source.read_bytes()
  replacement=original.replace(prefix.encode(),b"Assets/Probe/CoreShaderLibrary/")
  # Reversing the include prefix is exact; no HLSL equations or precision are changed.
  if replacement.replace(b"Assets/Probe/CoreShaderLibrary/",prefix.encode())!=original:raise RuntimeError("Non-include change")
  with target.open("xb") as output:output.write(replacement)
  prepared["input_pins"][str(source)]=sha(source);prepared["input_pins"][str(target)]=sha(target)
  rows.append({"source":str(source),"source_sha256":sha(source),"local_copy":str(target),"local_copy_sha256":sha(target),"equations_and_platform_conditions_unchanged":True})
 shader=ROOT/"project/Assets/Probe/BuiltinNormalConstant.compute"
 original_shader=shader.read_bytes()
 rewritten=original_shader.replace((CORE/"Common.hlsl").as_posix().encode(),b"Assets/Probe/CoreShaderLibrary/Common.hlsl").replace((CORE/"Packing.hlsl").as_posix().encode(),b"Assets/Probe/CoreShaderLibrary/Packing.hlsl")
 with (ROOT/"INITIAL-BuiltinNormalConstant.compute").open("xb") as output:output.write(original_shader)
 with (ROOT/"PREPARED.initial.v1.json").open("xb") as output:output.write(previous_bytes)
 shader.write_bytes(rewritten)
 prepared["input_pins"][str(shader)]=sha(shader)
 receipt=ROOT/"INCLUDE-PATH-REPAIR.v2.json"
 with receipt.open("x",encoding="utf-8") as output:json.dump({"previous_preparation_sha256":hashlib.sha256(previous_bytes).hexdigest(),"rows":rows,"includes_resolved_inside_isolated_assets":True,"original_package_files_modified":False,"native_probe_launched":False,"full_import":False},output,indent=2)
 prepared["input_pins"][str(receipt)]=sha(receipt)
 prepared["input_pins"][str(ROOT/"PREPARED.initial.v1.json")]=sha(ROOT/"PREPARED.initial.v1.json")
 prepared["include_path_repair_receipt"]=str(receipt)
 temporary=pointer.with_suffix(f".json.{os.getpid()}.tmp");temporary.write_text(json.dumps(prepared,indent=2),"utf-8")
 if pointer.read_bytes()!=previous_bytes:raise RuntimeError("Concurrent preparation change preserved")
 os.replace(temporary,pointer)
 print(json.dumps({"shader_library_files":len(rows),"include_only_changes":True,"full_import":False}))

if __name__=="__main__":main()
