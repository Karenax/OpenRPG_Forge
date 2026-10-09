"""Recover the API-only Villa observation using immutable copied context.

Original assets and the old refusal remain intact. The historical template
settings are reconciled with retained preimages, not silently accepted as changed.
"""
import os
from pathlib import Path
import subprocess
import sys

import observe_villa_normal_defaults as previous
import queue_hdrp_snapshot_v4 as common

ROOT = common.W / "unity-villa-normal-default-api-observation-v3"
OLD = common.W / "unity-villa-normal-default-api-observation-v2"
TEMPLATE = common.W / "unity-dungeon-original-shadergraph-hdrp-startup-v4/project"
ARCHIVE = common.W / "unity-dungeon-original-shadergraph-hdrp-context-v3"
common.ROOT = ROOT


def prepare():
    if ROOT.exists():
        raise ValueError("Previous recovery preparation preserved")
    old_path = OLD / "PREPARED.v1.json"
    old = common.read(old_path)
    pins = {str(old_path): common.sha(old_path), str(Path(__file__).resolve()): common.sha(__file__), str(Path(previous.__file__).resolve()): common.sha(previous.__file__), str(Path(common.__file__).resolve()): common.sha(common.__file__)}
    historical = []
    for path, expected in old["source_pins"].items():
        source = Path(path)
        if source.parent == previous.TEMPLATE / "ProjectSettings" and source.name in ["GraphicsSettings.asset", "QualitySettings.asset"]:
            retained = ARCHIVE / ("PREVIOUS-" + source.name)
            if common.sha(retained) != expected:
                raise ValueError("Historical settings preimage differs")
            pins[str(retained)] = expected
            historical.append({"mutable_reference": path, "expected_sha256": expected, "retained_preimage": str(retained)})
        else:
            if common.sha(source) != expected:
                raise ValueError("Original source/probe input changed: " + path)
            pins[path] = expected
    if len(historical) != 2:
        raise ValueError("Exact two reference setting preimages required")
    ROOT.mkdir()
    common.save("RUN-RESULT.v1.json", {"status": "preparing_independent_context", "native_observations": 0, "render_binding_proven": False, "full_import": False})
    project = ROOT / "project"
    contexts = []
    for folder in ["Packages", "ProjectSettings", "Assets/OpenRPGProbe", "Assets/HDRPDefaultResources"]:
        contexts.extend(path for path in (TEMPLATE / folder).rglob("*") if path.is_file())
    contexts.extend(path for path in (TEMPLATE / "Assets").iterdir() if path.is_file())
    context_pins = {}
    for source in contexts:
        if source.is_symlink() or source.suffix.lower() == ".cs":
            raise ValueError("Executable context not copied")
        expected = common.sha(source)
        target = project / source.relative_to(TEMPLATE)
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(source.read_bytes())
        if common.sha(source) != expected or common.sha(target) != expected:
            raise ValueError("Context copy changed")
        context_pins[str(target)] = expected
    copies = []
    for copied in old["private_copies"]:
        source = Path(copied["source"])
        expected = copied["sha256"]
        if common.sha(source) != expected:
            raise ValueError("Original Villa asset changed")
        target = project / source.relative_to(previous.SOURCE)
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(source.read_bytes())
        if common.sha(target) != expected:
            raise ValueError("Private Villa copy differs")
        pins[str(source)] = expected
        copies.append({"source": str(source), "copy": str(target), "sha256": expected})
    helper = project / "Assets/Editor/VillaNormalDefaultApiObservation.cs"
    helper.parent.mkdir(parents=True, exist_ok=True)
    code = previous.CS.replace('public string unity_version; public string material_guid;', 'public string selected_pipeline_type; public string unity_version; public string material_guid;')
    code = code.replace('var path="Assets/Hivemind/VillaForge', 'var selected=UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;\n   if(selected==null || selected.GetType().FullName!="UnityEngine.Rendering.HighDefinition.HDRenderPipelineAsset")throw new Exception("Startup HDRP context required");\n   var path="Assets/Hivemind/VillaForge')
    code = code.replace('var result=new Result{unity_version=', 'var result=new Result{selected_pipeline_type=selected.GetType().FullName,unity_version=')
    helper.write_text(code, encoding="utf-8")
    pins[str(helper)] = common.sha(helper)
    base = ARCHIVE / "api-check.v3.rsp"
    arguments = [line for line in base.read_text("utf-8-sig").splitlines() if line.startswith("-") and not line.startswith("-out:")]
    rsp = ROOT / "api-check.v1.rsp"
    rsp.write_text("\n".join(arguments + [f'-out:"{ROOT / "Observation.api.dll"}"', f'"{helper}"']), encoding="utf-8")
    sdk = Path(r"F:\Unity\6000.5.8f1\Editor\Data\DotNetSdk")
    compilers = list((sdk / "sdk").glob("*/Roslyn/bincore/csc.dll"))
    if len(compilers) != 1:
        raise ValueError("Installed compiler ambiguous")
    with (ROOT / "api.stdout.log").open("xb") as out, (ROOT / "api.stderr.log").open("xb") as err:
        exit_code = subprocess.run([str(sdk / "dotnet.exe"), str(compilers[0]), "@" + str(rsp)], cwd=ROOT, stdout=out, stderr=err).returncode
    common.save("API-COMPILE.v1.json", {"actual_exit_code": exit_code, "helper_sha256": common.sha(helper), "render_binding_proven": False})
    if exit_code:
        raise ValueError("Actual Unity API compile refused")
    common.check(pins)
    common.check(context_pins)
    common.save("PREPARED.v1.json", {"project": str(project), "source_pins": pins, "context_pins": context_pins, "private_copies": copies, "historical_template_settings_reconciled": historical, "previous_refusal": str(OLD / "RUN-RESULT.v1.json"), "original_project_pipeline_configuration": False, "render_binding_proven": False, "full_import": False})
    with (ROOT / "controller.log").open("xb") as log:
        child = subprocess.Popen([sys.executable, "-B", str(Path(__file__).resolve()), "run"], cwd=ROOT, stdout=log, stderr=log, stdin=subprocess.DEVNULL, creationflags=subprocess.CREATE_NO_WINDOW)
    common.save("JOB.v1.json", {"controller_pid": child.pid, "created_utc": common.now()})
    print({"queued_pid": child.pid, "private_copies": len(copies), "actual_api_compile_exit_code": 0, "native_observations": 0})


def run():
    state = {"status": "waiting_for_exact_medieval_material_controller", "native_observations": 0, "render_binding_proven": False, "full_import": False}
    common.save("RUN-RESULT.v1.json", state)
    try:
        wait = "$ErrorActionPreference='Stop';while($true){$p=Get-Process -Id 41288 -ErrorAction SilentlyContinue;if(!$p -or $p.StartTime.ToUniversalTime().Ticks -ne 639269641116536114L){break};$p.WaitForExit(45000)|Out-Null};while(Get-Process -Name Unity -ErrorAction SilentlyContinue){Start-Sleep -Seconds 30}"
        subprocess.run([str(common.PS), "-NoProfile", "-Command", wait], check=True, creationflags=subprocess.CREATE_NO_WINDOW)
        prep = common.read(ROOT / "PREPARED.v1.json")
        common.check(prep["source_pins"])
        common.check(prep["context_pins"])
        common.check({row["copy"]: row["sha256"] for row in prep["private_copies"]})
        output = ROOT / "NATIVE-RESULT.v1.json"
        env = os.environ.copy()
        env.update(TEMP=str(ROOT), TMP=str(ROOT), UPM_CACHE_ROOT=str(common.OLD / "upm-cache"))
        args = [str(previous.UNITY), "-batchmode", "-force-d3d11", "-quit", "-projectPath", prep["project"], "-executeMethod", "VillaNormalDefaultApiObservation.Run", "-normalDefaultOutput", str(output), "-logFile", str(ROOT / "unity.log")]
        with (ROOT / "unity.stdout.log").open("xb") as out, (ROOT / "unity.stderr.log").open("xb") as err:
            unity = subprocess.Popen(args, cwd=ROOT, env=env, stdout=out, stderr=err, stdin=subprocess.DEVNULL, creationflags=subprocess.CREATE_NO_WINDOW)
            state.update(status="observing_api_defaults_in_independent_hdrp_context", unity_pid=unity.pid)
            common.save("RUN-RESULT.v1.json", state)
            state["actual_unity_exit_code"] = unity.wait()
        common.check(prep["source_pins"])
        if state["actual_unity_exit_code"] != 0:
            raise ValueError("Native API observation refused; preserve diagnostics")
        native = common.read(output)
        if native["selected_pipeline_type"] != "UnityEngine.Rendering.HighDefinition.HDRenderPipelineAsset" or len(native["rows"]) != 4 or native["render_binding_proven"] or native["material_guid"] != "abc00000000015195609946836415000" or native["shader_guid"] != "e6f99812ca2d22a4c98d5e88e4d78f2a" or native["shader_file_id"] != "-6465566751694194690":
            raise ValueError("Exact source/API observation scope differs")
        if native["rows"][2]["is_null"] or native["rows"][2]["guid"] != "abc00000000006596162916920159822":
            raise ValueError("Native source base texture control differs")
        state.update(status="four_native_default_api_observations_captured_in_independent_context", native_observations=4, native_result_sha256=common.sha(output), original_source_pins_unchanged=True)
    except Exception as error:
        state.update(status="refused", error=str(error))
    state["finished_utc"] = common.now()
    common.save("RUN-RESULT.v1.json", state)
    print(state)


if __name__ == "__main__":
    {"prepare": prepare, "run": run}[sys.argv[1]]()
