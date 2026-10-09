"""Queue exact native material/default observations for thirteen null bindings.

Reuse the unchanged HDRP exporter. Do not replace missing authored textures,
execute bought scripts, or qualify the original project's unknown pipeline.
"""
import datetime
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys

W = Path(r"F:\OpenRPG_ImportWork")
ROOT = W / "unity-delta-null-material-defaults-v1"
OLD = W / "unity-dungeon-original-shadergraph-compiler-v1"
API_ROOT = W / "unity-dungeon-original-shadergraph-hdrp-context-v3"
PS = Path(r"C:\Users\karen\.cache\codex-runtimes\codex-primary-runtime\dependencies\native\powershell\pwsh.exe")
UNITY = Path(r"F:\Unity\6000.5.8f1\Editor\Unity.exe")


def sha(path):
    with Path(path).open("rb") as source:
        return hashlib.file_digest(source,"sha256").hexdigest()


def read(path):
    return json.loads(Path(path).read_text("utf-8-sig"))


def now():
    return datetime.datetime.now(datetime.timezone.utc).isoformat()


def save(name,value):
    target = ROOT / name
    temporary = target.with_suffix(".tmp")
    temporary.write_text(json.dumps(value,indent=2),"utf-8")
    os.replace(temporary,target)


def check(pins):
    for p,expected in pins.items():
        if sha(p) != expected:
            raise RuntimeError("Pinned source changed: "+p)


def prepare():
    if ROOT.exists():
        raise RuntimeError("Previous native defaults project preserved")
    ledger_path = W / "nested-prefab-delta-null-texture-resolution-v1/RESULT.v1.json"
    inventory_path = W / "nested-prefab-delta-material-dependency-inventory-v1/RESULT.v1.json"
    typed_path = W / "nested-prefab-delta-material-source-ir-v1/RESULT.v1.json"
    ledger,inventory,typed = [read(p) for p in [ledger_path,inventory_path,typed_path]]
    if ledger["refused_materials"]!=13 or not ledger["all_input_pins_unchanged"]:
        raise RuntimeError("Closed thirteen-material texture ledger required")
    api_path = API_ROOT / "API-COMPILE.v3.json"
    api = read(api_path)
    exporter = Path(api["source_helper"])
    if api["actual_api_compile_exit_code"]!=0 or sha(exporter)!=api["source_helper_sha256"]:
        raise RuntimeError("Existing unchanged compiled HDRP exporter required")
    if sha(exporter)!="2318fd5dd694db77f9faac0d3351c77b1ab405dfc7655e9106a3207b03a323a1":
        raise RuntimeError("HDRP exporter source identity differs")
    original_prep = read(OLD / "PREPARED.v1.json")
    template = Path(original_prep["project"])
    settings = template / "ProjectSettings/ProjectSettings.asset"
    if "m_ActiveColorSpace: 1" not in settings.read_text("utf-8-sig"):
        raise RuntimeError("Explicit isolated Linear settings required")
    pins = {str(p):sha(p) for p in [Path(__file__).resolve(),ledger_path,inventory_path,typed_path,
                                   api_path,exporter,settings,template/"ProjectSettings/ProjectVersion.txt",
                                   template/"Packages/manifest.json",UNITY]}
    identities = {(r["pack_id"],r["material_guid"]) for r in ledger["rows"]}
    material_sources = {(r["pack_id"],r["material_identity"]["guid"]):r for r in typed["rows"]}
    grouped = {}
    for material in inventory["rows"]:
        key = (material["pack_id"],material["material"]["guid"])
        if key in identities:
            grouped.setdefault(key[0],[]).append(material)
    if sum(len(v) for v in grouped.values())!=13 or len(grouped)!=2:
        raise RuntimeError("Thirteen native requests in two distinct source packs required")
    check(pins)
    ROOT.mkdir()
    copies,projects = [],[]
    for pack,materials in sorted(grouped.items()):
        base = W / "hivemind-run" / pack.removeprefix("hivemind.") / "source"
        project = ROOT / pack.removeprefix("hivemind.") / "project"
        for directory in ["Assets/Editor","ProjectSettings","Packages"]:
            (project/directory).mkdir(parents=True)
        (project/"Assets/Editor"/exporter.name).write_bytes(exporter.read_bytes())
        for src,dst in [(settings,project/"ProjectSettings/ProjectSettings.asset"),
                        (template/"ProjectSettings/ProjectVersion.txt",project/"ProjectSettings/ProjectVersion.txt"),
                        (template/"Packages/manifest.json",project/"Packages/manifest.json")]:
            shutil.copyfile(src,dst)
            pins[str(dst)] = sha(src)
        pins[str(project/"Assets/Editor"/exporter.name)] = sha(exporter)
        seen = set()
        for material in materials:
            for dependency in material["dependencies"]:
                for file in dependency["verified_files"]:
                    original = Path(file["path"])
                    if str(original) in seen:
                        continue
                    seen.add(str(original))
                    relative = original.relative_to(base)
                    if not relative.parts or relative.parts[0]!="Assets":
                        raise RuntimeError("Native project input must remain under source Assets")
                    if original.suffix.lower() in {".cs",".dll",".exe",".bat",".ps1",".js"}:
                        raise RuntimeError("Purchased executable/script dependency excluded")
                    if sha(original)!=file["sha256"]:
                        raise RuntimeError("Original material dependency changed")
                    destination = project/relative
                    destination.parent.mkdir(parents=True,exist_ok=True)
                    shutil.copyfile(original,destination)
                    if sha(destination)!=file["sha256"]:
                        raise RuntimeError("Native private copy differs")
                    pins[str(original)] = file["sha256"]
                    copies.append({"pack_id":pack,"original":str(original),"copy":str(destination),
                                   "sha256":file["sha256"],"private_payload_is_meta":original.suffix==".meta"})
        cases = []
        for material in materials:
            key = (pack,material["material"]["guid"])
            source = material_sources[key]
            shader = source["source_material_properties"]["m_Shader"]
            # Python retains the exact signed 64-bit fileID. Do not pass this
            # identity through JavaScript's floating point JSON parser.
            cases.append({"material_guid":key[1],"asset_path":material["material"]["asset_path"],
                          "expected_shader_guid":shader["guid"],"expected_shader_file_id":str(shader["fileID"])})
        manifest = project.parent/"manifest.v1.json"
        native = project.parent/"NATIVE-RESULT.v1.json"
        manifest.write_text(json.dumps({"output_path":str(native),"cases":cases},indent=2),"utf-8")
        pins[str(manifest)] = sha(manifest)
        projects.append({"pack_id":pack,"project":str(project),"manifest":str(manifest),
                         "native_result":str(native),"requested":len(cases)})
    check(pins)
    save("PREPARED.v1.json",{"status":"isolated_native_default_material_projects_prepared","created_utc":now(),
         "requested_materials":13,"projects":projects,"copies":copies,"input_pins":pins,
         "unchanged_exporter_api_receipt":str(api_path),"source_payload_copies":len(copies),
         "purchased_scripts_copied_or_executed":False,"original_project_pipeline_configuration":False,"full_import":False})
    # Exact prior controller identity, confirmed by JOB.v2.json. Wait for this
    # queue's termination, then for Unity itself; do not run competing editors.
    predecessor = read(W / "unity-nested-material-texture-storage-v2/JOB.v2.json")
    save("QUEUED.v1.json",{"wait_pid":17752,"wait_birth_ticks":639269292678355524,
                            "predecessor_receipt":str(W/"unity-nested-material-texture-storage-v2/JOB.v2.json"),
                            "predecessor":predecessor,"created_utc":now()})
    with (ROOT/"controller.log").open("xb") as log:
        child = subprocess.Popen([sys.executable,str(Path(__file__).resolve()),"run"],cwd=ROOT,
                                  stdin=subprocess.DEVNULL,stdout=log,stderr=log,creationflags=subprocess.CREATE_NO_WINDOW)
    save("JOB.v1.json",{"controller_pid":child.pid,"launched_utc":now(),"script":str(Path(__file__).resolve())})
    print(json.dumps({"queued":True,"controller_pid":child.pid,"projects":len(projects),"requested_materials":13,"root":str(ROOT)}))


def run():
    state = {"status":"waiting_for_prior_texture_controller","started_utc":now(),"native_unity_launched":False,
             "native_materials_verified":0,"completed_projects":[],"full_import":False,"studio_verified":False}
    save("RUN-RESULT.v1.json",state)
    try:
        wait = r"""$ErrorActionPreference='Stop';while($true){$p=Get-Process -Id 17752 -ErrorAction SilentlyContinue;if(!$p -or $p.StartTime.ToUniversalTime().Ticks -ne 639269292678355524L){break};$p.WaitForExit(45000)|Out-Null};while(Get-Process -Name Unity -ErrorAction SilentlyContinue){Start-Sleep -Seconds 30}"""
        subprocess.run([str(PS),"-NoProfile","-Command",wait],check=True,creationflags=subprocess.CREATE_NO_WINDOW)
        prep = read(ROOT/"PREPARED.v1.json")
        check(prep["input_pins"])
        for item in prep["projects"]:
            for copied in prep["copies"]:
                if sha(copied["original"])!=copied["sha256"]:
                    raise RuntimeError("Original material source changed")
                if copied["pack_id"]==item["pack_id"] and sha(copied["copy"])!=copied["sha256"]:
                    raise RuntimeError("Private source changed before native capture")
            native_path = Path(item["native_result"])
            if native_path.exists():
                raise RuntimeError("Previous native result preserved")
            args = [str(UNITY),"-batchmode","-force-d3d11","-quit","-projectPath",item["project"],
                    "-executeMethod","OpenRPGOriginalGraphHDRP.OriginalGraphMaterialSnapshotHDRP.Export",
                    "--openrpg-manifest",item["manifest"],"-logFile",str(native_path.parent/"unity.log")]
            env = os.environ.copy()
            env["UPM_CACHE_ROOT"] = str(OLD/"upm-cache")
            with (native_path.parent/"unity.stdout.log").open("xb") as out,(native_path.parent/"unity.stderr.log").open("xb") as err:
                child = subprocess.Popen(args,cwd=ROOT,env=env,stdout=out,stderr=err,creationflags=subprocess.CREATE_NO_WINDOW)
                state.update(status="capturing_native_material_texture_defaults",native_unity_launched=True,
                             unity_pid=child.pid,current_pack=item["pack_id"],unity_launched_utc=now())
                save("RUN-RESULT.v1.json",state)
                code = child.wait()
            state["last_unity_exit_code"] = code
            for copied in prep["copies"]:
                if sha(copied["original"])!=copied["sha256"]:
                    raise RuntimeError("Original source changed during native capture")
            if code!=0:
                raise RuntimeError("Native material defaults capture refused; retained logs")
            native,requests = read(native_path),read(item["manifest"])["cases"]
            if (native["requested"]!=len(requests) or native["captured"]!=len(requests) or native["failed"]
                    or len(native["cases"])!=len(requests) or native["graphics_api"]=="Null"
                    or native["selected_pipeline_type"]!="UnityEngine.Rendering.HighDefinition.HDRenderPipelineAsset"):
                raise RuntimeError("Native HDRP context coverage incomplete")
            for expected,row in zip(requests,native["cases"]):
                if (row["material_guid"]!=expected["material_guid"] or row["actual_material_file_id"]!="2100000"
                        or row["shader_guid"]!=expected["expected_shader_guid"] or row["shader_file_id"]!=expected["expected_shader_file_id"]
                        or row.get("error") or row["shader_has_errors"] or not row["shader_supported"]
                        or not row["compile_passes_executed"] or not row["passes"]
                        or any(not p["compiled"] or not p["name"] or p["name"].startswith("<Unnamed") for p in row["passes"])):
                    raise RuntimeError("Original shader identity/pass compilation not proven")
            private_changes = [{"path":c["copy"],"before_sha256":c["sha256"],"after_sha256":sha(c["copy"])}
                               for c in prep["copies"] if c["pack_id"]==item["pack_id"] and sha(c["copy"])!=c["sha256"]]
            state["completed_projects"].append({"pack_id":item["pack_id"],"native_result":str(native_path),
                                               "sha256":sha(native_path),"materials":len(requests),
                                               "private_importer_or_payload_changes":private_changes,
                                               "original_importer_parity_qualified":False})
            state["native_materials_verified"] += len(requests)
            save("RUN-RESULT.v1.json",state)
        for copied in prep["copies"]:
            if sha(copied["original"])!=copied["sha256"]:
                raise RuntimeError("Final original source guard failed")
        state.update(status="13_native_material_texture_defaults_captured_in_isolated_hdrp_context",
                     original_sources_unchanged=True,native_texture_observations_require_independent_reopening=True,
                     missing_authored_texture_substitution_performed=False,gpu_render_parity_qualified=False,
                     original_project_pipeline_configuration=False)
    except Exception as error:
        state.update(status="refused_or_partial",error=str(error))
    state["finished_utc"] = now()
    save("RUN-RESULT.v1.json",state)
    if state["status"]=="refused_or_partial":
        raise SystemExit(1)


if __name__=="__main__":
    {"prepare":prepare,"run":run}[sys.argv[1]]()
