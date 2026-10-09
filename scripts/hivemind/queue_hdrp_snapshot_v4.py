"""Capture original material passes with HDRP persisted before Unity starts.

Use a new project and receipts. Never mutate the v3 project used by other jobs.
"""
import datetime
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys
import time

W = Path(r"F:\OpenRPG_ImportWork")
ROOT = W / "unity-dungeon-original-shadergraph-hdrp-startup-v4"
OLD = W / "unity-dungeon-original-shadergraph-compiler-v1"
V3 = W / "unity-dungeon-original-shadergraph-hdrp-context-v3"
PS = Path(r"C:\Users\karen\.cache\codex-runtimes\codex-primary-runtime\dependencies\native\powershell\pwsh.exe")
WAIT = [(17752, 639269292678355524), (45700, 639269316542014449), (42212, 639269442801261812), (33944, 639269554070723078)]


def sha(path):
    with Path(path).open("rb") as source:
        return hashlib.file_digest(source, "sha256").hexdigest()


def read(path):
    return json.loads(Path(path).read_text("utf-8-sig"))


def save(name, value):
    path = ROOT / name
    temporary = path.with_suffix(f".json.{os.getpid()}.tmp")
    temporary.write_text(json.dumps(value, indent=2), encoding="utf-8")
    for attempt in range(40):
        try:
            os.replace(temporary, path)
            return
        except PermissionError:
            if attempt == 39:
                raise
            time.sleep(0.25)


def now():
    return datetime.datetime.now(datetime.timezone.utc).isoformat()


def check(pins):
    for path, expected in pins.items():
        if sha(path) != expected:
            raise ValueError("Changed pinned input: " + path)


def prepare():
    if ROOT.exists():
        raise ValueError("Previous preparation preserved")
    ROOT.mkdir()
    save("RUN-RESULT.v4.json", {"status": "preparing_separate_startup_hdrp_project", "full_import": False})
    prep = read(OLD / "PREPARED.v1.json")
    original_pins = {}
    replacements = {}
    old_project = Path(prep["project"])
    for row in prep["copies"]:
        for original, copied in [("original", "project_copy"), ("original_meta", "project_meta")]:
            original_pins[row[original]] = row[original + "_sha256"]
            replacements[str(Path(row[copied]).resolve())] = Path(row[original])
    check(original_pins)
    project = ROOT / "project"
    copied_pins = {}
    for folder in ["Assets", "Packages", "ProjectSettings"]:
        for path in (old_project / folder).rglob("*"):
            if not path.is_file():
                continue
            if path.is_symlink():
                raise ValueError("Project links are not copied")
            if path.suffix.lower() == ".cs":
                # Only our previous helper is present, but it is replaced below.
                if sha(path) != read(V3 / "API-COMPILE.v3.json")["source_helper_sha256"]:
                    raise ValueError("Untrusted script in isolated project")
                continue
            if path.name.endswith(".cs.meta"):
                continue
            source = replacements.get(str(path.resolve()), path)
            target = project / path.relative_to(old_project)
            target.parent.mkdir(parents=True, exist_ok=True)
            before = sha(source)
            target.write_bytes(source.read_bytes())
            if sha(source) != before or sha(target) != before:
                raise ValueError("Unstable project copy")
            copied_pins[str(target)] = before
    graphics = project / "ProjectSettings/GraphicsSettings.asset"
    pipeline_meta = project / "Assets/OpenRPGProbe/SourcePassHDRPContext.asset.meta"
    pipeline_guid = next(line.split(":", 1)[1].strip() for line in pipeline_meta.read_text().splitlines() if line.startswith("guid:"))
    if f"m_CustomRenderPipeline: {{fileID: 11400000, guid: {pipeline_guid}, type: 2}}" not in graphics.read_text():
        raise ValueError("Persisted default HDRP asset absent")
    helper_text = Path(read(V3 / "API-COMPILE.v3.json")["source_helper"]).read_text()
    begin = helper_text.index("   // This asset selects")
    end = helper_text.index("   foreach(var item in request.cases)", begin)
    helper_text = helper_text[:begin] + '''   // The copied settings select HDRP before this process imports any graph.
   var pipeline=UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.HighDefinition.HDRenderPipelineAsset;
   if(pipeline==null)throw new Exception("HDRP must be selected at process startup; no runtime pipeline switch");
''' + helper_text[end:]
    helper_text = helper_text.replace("string material_guid,actual_material_file_id,", "string active_render_pipeline_tag,material_guid,actual_material_file_id,")
    marker = "row.shader_supported=shader.isSupported;"
    helper_text = helper_text.replace(marker, 'row.active_render_pipeline_tag=material.GetTag("RenderPipeline",false,"");if(row.active_render_pipeline_tag!="HDRenderPipeline")throw new Exception("Selected source subshader is not HDRP: "+row.active_render_pipeline_tag);' + marker)
    helper = project / "Assets/Editor/OriginalGraphMaterialSnapshotHDRP.cs"
    helper.parent.mkdir(parents=True, exist_ok=True)
    helper.write_text(helper_text, encoding="utf-8")
    copied_pins[str(helper)] = sha(helper)
    manifest = read(V3 / "manifest.v3.json")
    manifest["output_path"] = str(ROOT / "NATIVE-RESULT.v4.json")
    save("manifest.v4.json", manifest)
    rsp = ROOT / "api-check.v4.rsp"
    base = V3 / "api-check.v3.rsp"
    arguments = [line for line in base.read_text("utf-8-sig").splitlines() if line.startswith("-") and not line.startswith("-out:")]
    arguments += [f'-out:"{ROOT / "Snapshot.api.dll"}"', f'"{helper}"']
    rsp.write_text("\n".join(arguments), encoding="utf-8")
    sdk = Path(r"F:\Unity\6000.5.8f1\Editor\Data\DotNetSdk")
    compilers = list((sdk / "sdk").glob("*/Roslyn/bincore/csc.dll"))
    if len(compilers) != 1:
        raise ValueError("Actual Unity compiler ambiguous")
    with (ROOT / "api.stdout.log").open("xb") as out, (ROOT / "api.stderr.log").open("xb") as err:
        code = subprocess.run([str(sdk / "dotnet.exe"), str(compilers[0]), "@" + str(rsp)], cwd=ROOT, stdout=out, stderr=err).returncode
    save("API-COMPILE.v4.json", {"actual_exit_code": code, "helper_sha256": sha(helper)})
    if code:
        raise ValueError("Actual API compile refused")
    check(original_pins)
    pins = {**original_pins, **copied_pins, str(Path(__file__).resolve()): sha(__file__), str(ROOT / "manifest.v4.json"): sha(ROOT / "manifest.v4.json")}
    save("PREPARED.v4.json", {"project": str(project), "input_pins": pins, "source_assets": len(prep["copies"]), "copied_files": len(copied_pins), "pipeline_persisted_before_launch": True, "original_project_pipeline_configuration": False, "wait": WAIT})
    with (ROOT / "controller.v4.log").open("xb") as log:
        child = subprocess.Popen([sys.executable, str(Path(__file__).resolve()), "run"], cwd=ROOT, stdout=log, stderr=log, stdin=subprocess.DEVNULL, creationflags=subprocess.CREATE_NO_WINDOW)
    save("JOB.v4.json", {"controller_pid": child.pid, "created_utc": now()})
    print(json.dumps({"queued_pid": child.pid, "root": str(ROOT), "actual_api_compile_exit_code": code}))


def run():
    state = {"status": "waiting_for_exact_existing_unity_queue", "started_utc": now(), "full_import": False, "gpu_render_parity_qualified": False, "studio_verified": False}
    save("RUN-RESULT.v4.json", state)
    try:
        wait = "$ErrorActionPreference='Stop';"
        for pid, ticks in WAIT:
            wait += f"while($true){{$p=Get-Process -Id {pid} -ErrorAction SilentlyContinue;if(!$p -or $p.StartTime.ToUniversalTime().Ticks -ne {ticks}L){{break}};$p.WaitForExit(45000)|Out-Null}};"
        wait += "while(Get-Process -Name Unity -ErrorAction SilentlyContinue){Start-Sleep -Seconds 30}"
        subprocess.run([str(PS), "-NoProfile", "-Command", wait], check=True, creationflags=subprocess.CREATE_NO_WINDOW)
        prep = read(ROOT / "PREPARED.v4.json")
        check(prep["input_pins"])
        env = os.environ.copy()
        env.update(TEMP=str(ROOT), TMP=str(ROOT), UPM_CACHE_ROOT=str(OLD / "upm-cache"))
        args = [r"F:\Unity\6000.5.8f1\Editor\Unity.exe", "-batchmode", "-force-d3d11", "-quit", "-projectPath", prep["project"], "-executeMethod", "OpenRPGOriginalGraphHDRP.OriginalGraphMaterialSnapshotHDRP.Export", "--openrpg-manifest", str(ROOT / "manifest.v4.json"), "-logFile", str(ROOT / "unity.v4.log")]
        with (ROOT / "unity.stdout.log").open("xb") as out, (ROOT / "unity.stderr.log").open("xb") as err:
            unity = subprocess.Popen(args, cwd=ROOT, env=env, stdout=out, stderr=err, stdin=subprocess.DEVNULL, creationflags=subprocess.CREATE_NO_WINDOW)
            state.update(status="capturing_original_passes_with_startup_hdrp", unity_pid=unity.pid)
            save("RUN-RESULT.v4.json", state)
            state["actual_unity_exit_code"] = unity.wait()
        # Unity may upgrade copied meta/settings; preserve those separately, but
        # purchased originals and the exact copied source content must not change.
        stable = {path: value for path, value in prep["input_pins"].items() if not path.startswith(str(ROOT / "project")) or (path.startswith(str(ROOT / "project/Assets")) and not path.endswith(".meta"))}
        check(stable)
        native = read(ROOT / "NATIVE-RESULT.v4.json")
        state.update(captured=native["captured"], failed=native["failed"], native_result_sha256=sha(ROOT / "NATIVE-RESULT.v4.json"))
        if state["actual_unity_exit_code"] != 0 or native["requested"] != 42 or native["captured"] != 42 or native["failed"]:
            raise ValueError("Full startup HDRP capture absent; retain native errors")
        manifest = read(ROOT / "manifest.v4.json")
        if len(native["cases"]) != 42 or native["graphics_api"] == "Null" or native["selected_pipeline_type"] != "UnityEngine.Rendering.HighDefinition.HDRenderPipelineAsset":
            raise ValueError("Actual native HDRP context absent")
        for request, row in zip(manifest["cases"], native["cases"]):
            if row["material_guid"] != request["material_guid"] or row["actual_material_file_id"] != "2100000" or row["shader_guid"] != request["expected_shader_guid"] or row["shader_file_id"] != request["expected_shader_file_id"]:
                raise ValueError("Original native identity mismatch")
            if row["error"] or row["shader_has_errors"] or not row["shader_supported"] or row["active_render_pipeline_tag"] != "HDRenderPipeline" or not row["compile_passes_executed"] or not row["passes"]:
                raise ValueError("Original HDRP passes unverified")
            if any(not p["compiled"] or not p["name"] or p["name"].startswith("<Unnamed") for p in row["passes"]):
                raise ValueError("Unnamed/uncompiled source pass")
        state.update(status="42_original_material_passes_verified_with_startup_hdrp", original_sources_unchanged=True, original_project_pipeline_configuration=False)
    except Exception as error:
        state.update(status="refused", error=str(error))
    state["finished_utc"] = now()
    save("RUN-RESULT.v4.json", state)
    print(json.dumps(state))


if __name__ == "__main__":
    {"prepare": prepare, "run": run}[sys.argv[1]]()
