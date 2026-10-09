"""Retain Unity shader-library licences alongside the isolated copied includes."""
import hashlib
import json
import os
from pathlib import Path

ROOT=Path(r"F:\OpenRPG_ImportWork\unity-builtin-normal-constant-reference-v1")
PACKAGE=Path(r"F:\OpenRPG_ImportWork\unity-dungeon-original-shadergraph-compiler-v1\project\Library\PackageCache\com.unity.render-pipelines.core@a4c9a9793789")
def sha(path):return hashlib.sha256(Path(path).read_bytes()).hexdigest()
def main():
 state=json.loads((ROOT/"RUN-RESULT.v1.json").read_text("utf-8"))
 if state["native_unity_launched"]:raise RuntimeError("Preserve active inputs; probe already launched")
 pointer=ROOT/"PREPARED.v1.json";before=pointer.read_bytes();prepared=json.loads(before)
 folder=ROOT/"licenses";folder.mkdir(exist_ok=False);rows=[]
 for name in ["LICENSE.md","THIRD PARTY NOTICES.md"]:
  source=PACKAGE/name;copy=folder/name;copy.write_bytes(source.read_bytes())
  if sha(source)!=sha(copy):raise RuntimeError("Licence copy mismatch")
  prepared["input_pins"][str(source)]=sha(source);prepared["input_pins"][str(copy)]=sha(copy)
  rows.append({"source":str(source),"copy":str(copy),"sha256":sha(copy)})
 report=ROOT/"LICENSES-REOPEN.v1.json"
 report.write_text(json.dumps({"package":"com.unity.render-pipelines.core","version":"17.5.0","rows":rows,"licences_preserved_verbatim":True,"private_probe_only":True},indent=2),"utf-8")
 prepared["input_pins"][str(report)]=sha(report);prepared["license_receipt"]=str(report)
 with (ROOT/"PREPARED.includes.v2.json").open("xb") as output:output.write(before)
 temporary=pointer.with_suffix(f".json.{os.getpid()}.tmp");temporary.write_text(json.dumps(prepared,indent=2),"utf-8")
 if pointer.read_bytes()!=before:raise RuntimeError("Concurrent pointer change preserved")
 os.replace(temporary,pointer);print("2 original Unity licence documents retained and pinned")
if __name__=="__main__":main()
