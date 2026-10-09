"""Compile the bounded HDRP snapshot helper against installed Unity APIs; do not launch Unity."""
import datetime,hashlib,json,subprocess
from pathlib import Path
W=Path(r"F:\OpenRPG_ImportWork"); OLD=W/"unity-dungeon-original-shadergraph-compiler-v1"; ROOT=W/"unity-dungeon-original-shadergraph-hdrp-context-v3"
SRC=Path(r"E:\OpenRPG_Forge\OpenRPG_Forge-Rust\scripts\hivemind\candidates\OriginalGraphMaterialSnapshotHDRP.v3.cs")
ROOT.mkdir(exist_ok=False)
helper=ROOT/SRC.name;helper.write_bytes(SRC.read_bytes())
lines=(OLD/"api-check.v2.rsp").read_text(encoding="utf-8-sig").splitlines()
lines=[line for line in lines if line.startswith("-") and not line.startswith("-out:")]
assemblies=OLD/"project"/"Library"/"ScriptAssemblies"
for name in ["Unity.RenderPipelines.HighDefinition.Runtime.dll","Unity.RenderPipelines.Core.Runtime.dll","Unity.RenderPipelines.Core.Runtime.Shared.dll","Unity.Mathematics.dll"]:
    path=assemblies/name
    if not path.is_file():raise RuntimeError("Required installed API assembly absent: "+name)
    lines.append(f'-reference:"{path}"')
lines.extend([f'-out:"{ROOT / "OriginalGraphMaterialSnapshotHDRP.api.dll"}"',f'"{helper}"'])
rsp=ROOT/"api-check.v3.rsp";rsp.write_text("\n".join(lines),encoding="utf-8")
sdk=Path(r"F:\Unity\6000.5.8f1\Editor\Data\DotNetSdk")
candidates=list((sdk/"sdk").glob("*/Roslyn/bincore/csc.dll"))
if len(candidates)!=1:raise RuntimeError("Ambiguous installed Roslyn compiler")
started=datetime.datetime.now(datetime.timezone.utc).isoformat()
with (ROOT/"api.stdout.log").open("w",encoding="utf-8") as out, (ROOT/"api.stderr.log").open("w",encoding="utf-8") as err:
    completed=subprocess.run([str(sdk/"dotnet.exe"),str(candidates[0]),"@"+str(rsp)],cwd=ROOT,stdout=out,stderr=err,check=False)
receipt={"started_utc":started,"finished_utc":datetime.datetime.now(datetime.timezone.utc).isoformat(),"actual_api_compile_exit_code":completed.returncode,"source_helper_sha256":hashlib.sha256(helper.read_bytes()).hexdigest(),"source_helper":str(helper),"native_unity_launched":False,"pipeline_context_is_original_project_config":False,"target_pipeline_pass_coverage_verified":False,"full_import":False,"next_launch_waits_for_current_unity_and_nested_capture":True}
(ROOT/"API-COMPILE.v3.json").write_text(json.dumps(receipt,indent=2),encoding="utf-8")
print(json.dumps(receipt))
raise SystemExit(completed.returncode)
