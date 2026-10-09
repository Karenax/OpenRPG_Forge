"""Queue a bounded HDRP capture after the already active Unity controllers."""
import datetime
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys

W = Path(r"F:\OpenRPG_ImportWork")
ROOT = W / "unity-dungeon-original-shadergraph-hdrp-context-v3"
OLD = W / "unity-dungeon-original-shadergraph-compiler-v1"
PS = Path(r"C:\Users\karen\.cache\codex-runtimes\codex-primary-runtime\dependencies\native\powershell\pwsh.exe")

def sha(path):
    with Path(path).open("rb") as source:
        return hashlib.file_digest(source, "sha256").hexdigest()

def read(path):
    return json.loads(Path(path).read_text("utf-8-sig"))

def save(name, value):
    path = ROOT / name
    temporary = path.with_suffix(f".json.{os.getpid()}.tmp")
    temporary.write_text(json.dumps(value, indent=2), encoding="utf-8")
    os.replace(temporary, path)

def now():
    return datetime.datetime.now(datetime.timezone.utc).isoformat()

def verify_sources(prep):
    for row in prep["copies"]:
        for field in ["original", "original_meta"]:
            if sha(row[field]) != row[field + "_sha256"]:
                raise RuntimeError("Original source changed: " + row[field])

def prepare():
    if (ROOT / "QUEUED.v1.json").exists():
        raise RuntimeError("Existing queued job preserved")
    api = read(ROOT / "API-COMPILE.v3.json")
    if api["actual_api_compile_exit_code"] != 0 or sha(api["source_helper"]) != api["source_helper_sha256"]:
        raise RuntimeError("Actual compiled API helper required")
    if read(OLD / "RUN-RESULT.v2.json").get("unity_exit_code") != 0:
        raise RuntimeError("Previous isolated project still unverified")
    manifest = read(OLD / "manifest.json")
    manifest["output_path"] = str(ROOT / "NATIVE-RESULT.v3.json")
    with (ROOT / "manifest.v3.json").open("x", encoding="utf-8") as output:
        json.dump(manifest, output, indent=2)
    pins = {str(path): sha(path) for path in [Path(__file__), ROOT / "manifest.v3.json", ROOT / "API-COMPILE.v3.json", Path(api["source_helper"]), OLD / "PREPARED.v1.json"]}
    save("QUEUED.v1.json", {"created_utc": now(), "input_pins": pins, "wait_for_controllers": [41576, 43256], "project_is_isolated": True, "original_project_pipeline_configuration": False})
    with (ROOT / "controller.v3.log").open("xb") as log:
        child = subprocess.Popen([sys.executable, str(Path(__file__).resolve()), "run"], cwd=ROOT, stdin=subprocess.DEVNULL, stdout=log, stderr=log, creationflags=subprocess.CREATE_NO_WINDOW)
    save("JOB.v3.json", {"controller_pid": child.pid, "created_utc": now()})
    print(json.dumps({"queued": True, "controller_pid": child.pid, "root": str(ROOT)}))

def run():
    state = {"status": "waiting_for_prior_unity_controllers", "started_utc": now(), "native_unity_launched": False, "full_import": False, "new_qualified_assets": 0}
    save("RUN-RESULT.v3.json", state)
    try:
        # Birth times prevent recycled PIDs from becoming dependencies.
        wait_script = r"""
        $ErrorActionPreference='Stop'
        foreach($job in @(@{pid=41576;ticks=639268921659654069L},@{pid=43256;ticks=639269118471258673L})) {
          while($true) {$p=Get-Process -Id $job.pid -ErrorAction SilentlyContinue;if(!$p -or $p.StartTime.ToUniversalTime().Ticks -ne $job.ticks){break};$p.WaitForExit(45000)|Out-Null}
        }
        while(Get-Process -Name Unity -ErrorAction SilentlyContinue){Start-Sleep -Seconds 30}
        """
        subprocess.run([str(PS), "-NoProfile", "-Command", wait_script], check=True, creationflags=subprocess.CREATE_NO_WINDOW)
        queued = read(ROOT / "QUEUED.v1.json")
        for path, expected in queued["input_pins"].items():
            if sha(path) != expected:
                raise RuntimeError("Changed queued input: " + path)
        prep = read(OLD / "PREPARED.v1.json")
        verify_sources(prep)
        project = Path(prep["project"])
        scripts = list((project / "Assets").rglob("*.cs"))
        original_helper = OLD / "OriginalGraphMaterialSnapshot.v2.cs"
        if len(scripts) != 1 or sha(scripts[0]) != sha(original_helper):
            raise RuntimeError("Only the exact previous own helper may be replaced")
        with (ROOT / "PREVIOUS-PROJECT-HELPER.cs").open("xb") as output:
            output.write(scripts[0].read_bytes())
        for name in ["GraphicsSettings.asset", "QualitySettings.asset"]:
            with (ROOT / ("PREVIOUS-" + name)).open("xb") as output:
                output.write((project / "ProjectSettings" / name).read_bytes())
        api = read(ROOT / "API-COMPILE.v3.json")
        scripts[0].write_bytes(Path(api["source_helper"]).read_bytes())
        env = os.environ.copy()
        env["UPM_CACHE_ROOT"] = str(OLD / "upm-cache")
        arguments = [r"F:\Unity\6000.5.8f1\Editor\Unity.exe", "-batchmode", "-force-d3d11", "-quit", "-projectPath", str(project), "-executeMethod", "OpenRPGOriginalGraphHDRP.OriginalGraphMaterialSnapshotHDRP.Export", "--openrpg-manifest", str(ROOT / "manifest.v3.json"), "-logFile", str(ROOT / "unity.v3.log")]
        with (ROOT / "unity.stdout.v3.log").open("xb") as out, (ROOT / "unity.stderr.v3.log").open("xb") as err:
            unity = subprocess.Popen(arguments, env=env, cwd=ROOT, stdin=subprocess.DEVNULL, stdout=out, stderr=err, creationflags=subprocess.CREATE_NO_WINDOW)
            state.update(status="capturing_original_material_passes_in_isolated_hdrp_context", native_unity_launched=True, unity_pid=unity.pid, unity_launched_utc=now(), command=arguments)
            save("RUN-RESULT.v3.json", state)
            state["actual_unity_exit_code"] = unity.wait()
        verify_sources(prep)
        if state["actual_unity_exit_code"] != 0:
            raise RuntimeError("Unity refused capture; inspect retained native rows and log")
        native = read(ROOT / "NATIVE-RESULT.v3.json")
        manifest = read(ROOT / "manifest.v3.json")
        if native["requested"] != 42 or native["captured"] != 42 or native["failed"] or len(native["cases"]) != 42 or native["selected_pipeline_type"] != "UnityEngine.Rendering.HighDefinition.HDRenderPipelineAsset" or native["graphics_api"] == "Null":
            raise RuntimeError("Full actual HDRP pass coverage absent")
        for request, row in zip(manifest["cases"], native["cases"]):
            if any(row[key] != request[key] for key in ["material_guid"]):
                raise RuntimeError("Material identity changed")
            if row["shader_guid"] != request["expected_shader_guid"] or row["shader_file_id"] != request["expected_shader_file_id"] or row["actual_material_file_id"] != "2100000":
                raise RuntimeError("Original shader identity changed")
            if row["error"] or row["shader_has_errors"] or not row["shader_supported"] or not row["compile_passes_executed"] or not row["passes"]:
                raise RuntimeError("Original native passes unverified")
            if any(not p["compiled"] or not p["name"] or p["name"].startswith("<Unnamed") for p in row["passes"]):
                raise RuntimeError("Unnamed or uncompiled pass refused")
        state.update(status="42_original_material_hdrp_passes_verified_in_isolated_context", native_result_sha256=sha(ROOT / "NATIVE-RESULT.v3.json"), original_sources_unchanged=True, gpu_render_parity_qualified=False, original_project_pipeline_configuration=False, studio_verified=False)
    except Exception as error:
        state.update(status="refused", error=str(error))
    state["finished_utc"] = now()
    save("RUN-RESULT.v3.json", state)
    print(json.dumps(state))

if __name__ == "__main__":
    {"prepare": prepare, "run": run}[sys.argv[1]]()
