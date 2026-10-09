"""Independently reopen native observations; never turn a default into fidelity proof."""
import copy
import datetime
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys

W = Path(r"F:\OpenRPG_ImportWork")
CAPTURE = W / "unity-delta-null-material-defaults-v1"
ROOT = W / "unity-delta-null-material-defaults-reopen-v1"
LEDGER = W / "nested-prefab-delta-null-texture-resolution-v1/RESULT.v1.json"
PS = r"C:\Users\karen\.cache\codex-runtimes\codex-primary-runtime\dependencies\native\powershell\pwsh.exe"


def read(p):
    return json.loads(Path(p).read_text("utf-8-sig"))


def sha(p):
    with Path(p).open("rb") as f:
        return hashlib.file_digest(f, "sha256").hexdigest()


def save(name, value):
    target = ROOT / name
    temporary = target.with_suffix(".tmp")
    temporary.write_text(json.dumps(value, indent=2), "utf-8")
    os.replace(temporary, target)


def require(condition, message):
    if not condition:
        raise ValueError(message)


def validate(native, requests, bindings):
    require(native["requested"] == len(requests) == native["captured"] == len(native["cases"])
            and native["failed"] == 0, "Incomplete native coverage")
    require(native["unity_version"] == "6000.5.8f1", "Unexpected Unity version")
    require(native["graphics_api"] in {"Direct3D11", "Direct3D12"} and native["graphics_device"], "Native GPU absent")
    require(native["selected_pipeline_type"] == "UnityEngine.Rendering.HighDefinition.HDRenderPipelineAsset", "Wrong context")
    require(not native["purchased_scripts_executed"] and not native["gpu_render_parity_qualified"]
            and not native["original_pack_unity_version_qualified"], "Unsupported fidelity claim")
    packages = native["packages"]
    for name in ["com.unity.render-pipelines.high-definition", "com.unity.render-pipelines.universal"]:
        matching = [p for p in packages if p["name"] == name]
        require(len(matching) == 1 and matching[0]["version"] == "17.5.0", "Pipeline package mismatch")
    expected = {r["material_guid"]: r for r in requests}
    require(len(expected) == len(requests), "Duplicate request")
    seen, observations = set(), []
    for row in native["cases"]:
        guid = row["material_guid"]
        require(guid in expected and guid not in seen, "Duplicate/foreign native material")
        seen.add(guid)
        request = expected[guid]
        require(row["actual_material_file_id"] == "2100000"
                and row["shader_guid"] == request["expected_shader_guid"]
                and isinstance(row["shader_file_id"], str)
                and row["shader_file_id"] == request["expected_shader_file_id"], "Source identity differs")
        require(not row.get("error") and row["shader_supported"] and not row["shader_has_errors"]
                and row["shader_name"] and row["native_material_json"], "Native material/shader refused")
        require(not row["original_shader_render_parity_qualified"], "Unsupported render claim")
        require(row["compile_passes_executed"] and row["passes"] and all(
            p["compiled"] and p["name"] and not p["name"].startswith("<Unnamed") for p in row["passes"]), "Uncompiled/unnamed pass")
        require(not any(m["severity"] == "Error" for m in row.get("messages", [])), "Compiler diagnostic error")
        props = {p["name"]: p for p in row["properties"]}
        require(len(props) == len(row["properties"]), "Duplicate property")
        material_bindings = [b for b in bindings if b["material_guid"] == guid]
        require(material_bindings, "Source ledger coverage missing")
        for binding in material_bindings:
            name = binding["property"]
            require(name in props and props[name]["type"] == "Texture", "Required texture property absent")
            p = props[name]
            require(isinstance(p["texture_null"], bool) and isinstance(p["texture_exact_identity"], bool), "Malformed native flags")
            require(isinstance(p["default_texture_name"], str) and p["default_texture_name"], "Native default name absent")
            if p["texture_null"]:
                require(not p["texture_exact_identity"] and not p["texture_guid"] and not p["texture_file_id"]
                        and not p["texture_asset_path"] and not p["texture_name"], "Contradictory null texture")
                classification = "native_null_and_declared_default_observed"
            elif p["texture_exact_identity"]:
                require(len(p["texture_guid"]) == 32 and int(p["texture_file_id"]) != 0
                        and p["texture_asset_path"] and p["texture_name"], "Native texture identity incomplete")
                classification = "native_texture_identity_observed_not_storage_qualified"
            else:
                require(p["texture_name"] and not p["texture_guid"] and not p["texture_file_id"], "Anonymous texture inconsistent")
                classification = "native_texture_without_asset_identity_requires_adapter"
            observations.append({"material_guid": guid, "property": name, "node_id": binding["node_id"],
                                 "slot_id": binding["slot_id"], "classification": classification,
                                 "native_texture": p, "graph_declared_texture_reference": binding["graph_declared_texture_reference"],
                                 "missing_authored_texture_substitution_authorized": False,
                                 "runtime_binding_qualified": False, "gpu_render_parity_qualified": False})
    require(seen == set(expected), "Native material set mismatch")
    return observations


def controls():
    request = {"material_guid": "a" * 32, "expected_shader_guid": "b" * 32,
               "expected_shader_file_id": "-6465566751694199999"}
    binding = {"material_guid": "a" * 32, "property": "_Mask", "node_id": "node", "slot_id": 1,
               "graph_declared_texture_reference": {"guid": "c" * 32, "fileID": 2800000}}
    prop = {"name": "_Mask", "type": "Texture", "texture_null": True, "texture_exact_identity": False,
            "texture_guid": "", "texture_file_id": "", "texture_name": "", "texture_asset_path": "", "default_texture_name": "white"}
    row = {"material_guid": "a" * 32, "actual_material_file_id": "2100000", "shader_guid": "b" * 32,
           "shader_file_id": request["expected_shader_file_id"], "shader_supported": True, "shader_has_errors": False,
           "shader_name": "Source", "native_material_json": "{}", "original_shader_render_parity_qualified": False,
           "compile_passes_executed": True, "passes": [{"name": "Forward", "compiled": True}], "messages": [], "properties": [prop]}
    native = {"requested": 1, "captured": 1, "failed": 0, "cases": [row], "unity_version": "6000.5.8f1",
              "graphics_api": "Direct3D11", "graphics_device": "control fixture", "selected_pipeline_type": "UnityEngine.Rendering.HighDefinition.HDRenderPipelineAsset",
              "purchased_scripts_executed": False, "gpu_render_parity_qualified": False, "original_pack_unity_version_qualified": False,
              "packages": [{"name": n, "version": "17.5.0"} for n in ["com.unity.render-pipelines.high-definition", "com.unity.render-pipelines.universal"]]}
    observation = validate(native, [request], [binding])[0]
    require(not observation["runtime_binding_qualified"] and not observation["missing_authored_texture_substitution_authorized"], "Default promoted to fidelity")
    mutations = {
        "partial_coverage": lambda n: n.update(captured=0),
        "duplicate_material": lambda n: n.update(cases=n["cases"] * 2),
        "lost_64_bit_file_id": lambda n: n["cases"][0].update(shader_file_id="-6465566751694200000"),
        "null_gpu": lambda n: n.update(graphics_api="Null"),
        "package_drift": lambda n: n["packages"][0].update(version="17.4.0"),
        "unnamed_pass": lambda n: n["cases"][0]["passes"][0].update(name="<Unnamed Pass>"),
        "pass_not_compiled": lambda n: n["cases"][0]["passes"][0].update(compiled=False),
        "texture_property_missing": lambda n: n["cases"][0].update(properties=[]),
        "null_with_identity": lambda n: n["cases"][0]["properties"][0].update(texture_guid="c" * 32),
        "compiler_error": lambda n: n["cases"][0].update(messages=[{"severity": "Error"}]),
        "false_gpu_parity": lambda n: n.update(gpu_render_parity_qualified=True),
    }
    passed = []
    for name, mutate in mutations.items():
        changed = copy.deepcopy(native)
        mutate(changed)
        try:
            validate(changed, [request], [binding])
        except ValueError:
            passed.append(name)
        else:
            raise AssertionError("Unsafe receipt accepted: " + name)
    return {"positive_controls": 1, "negative_controls_passed": passed, "fixture_is_native_capture": False}


def prepare():
    require(not ROOT.exists(), "Previous reopening preserved")
    job = read(CAPTURE / "JOB.v1.json")
    require(job["controller_pid"] == 45700, "Controller identity changed")
    ticks = subprocess.check_output([PS, "-NoProfile", "-Command",
        "(Get-Process -Id 45700 -ErrorAction Stop).StartTime.ToUniversalTime().Ticks"], text=True).strip()
    require(ticks.isdigit(), "Live controller birth required")
    ROOT.mkdir()
    save("CONTROLS.v1.json", controls())
    pins = {str(p): sha(p) for p in [Path(__file__).resolve(), CAPTURE / "PREPARED.v1.json", CAPTURE / "JOB.v1.json", LEDGER]}
    save("PREPARED.v1.json", {"wait_pid": 45700, "wait_birth_ticks": ticks, "input_pins": pins})
    with (ROOT / "controller.log").open("xb") as log:
        child = subprocess.Popen([sys.executable, str(Path(__file__).resolve()), "run"], cwd=ROOT,
                                 stdin=subprocess.DEVNULL, stdout=log, stderr=log, creationflags=subprocess.CREATE_NO_WINDOW)
    save("JOB.v1.json", {"pid": child.pid, "launched_utc": datetime.datetime.now(datetime.timezone.utc).isoformat()})
    print(json.dumps({"queued": True, "pid": child.pid, "negative_controls_passed": 11, "native_materials_reopened": 0}))


def run():
    state = {"status": "waiting_for_native_material_capture", "native_materials_reopened": 0, "full_import": False,
             "studio_verified": False, "gpu_render_parity_qualified": False}
    save("RUN-RESULT.v1.json", state)
    try:
        prepared = read(ROOT / "PREPARED.v1.json")
        wait = "$ErrorActionPreference='Stop';while($true){$p=Get-Process -Id 45700 -ErrorAction SilentlyContinue;if(!$p -or $p.StartTime.ToUniversalTime().Ticks -ne " + prepared["wait_birth_ticks"] + "L){break};$p.WaitForExit(45000)|Out-Null}"
        subprocess.run([PS, "-NoProfile", "-Command", wait], check=True, creationflags=subprocess.CREATE_NO_WINDOW)
        for p, expected in prepared["input_pins"].items():
            require(sha(p) == expected, "Reopening input changed: " + p)
        capture = read(CAPTURE / "RUN-RESULT.v1.json")
        require(capture["status"] == "13_native_material_texture_defaults_captured_in_isolated_hdrp_context"
                and capture["native_materials_verified"] == 13 and capture["original_sources_unchanged"], "Native capture not complete; inspect retained logs")
        prep, ledger = read(CAPTURE / "PREPARED.v1.json"), read(LEDGER)
        require(ledger["all_input_pins_unchanged"] and len(ledger["rows"]) == 13, "Thirteen source bindings required")
        for p, expected in ledger["source_pins"].items():
            require(sha(p) == expected, "Source ledger drift: " + p)
        completed = {p["pack_id"]: p for p in capture["completed_projects"]}
        require(len(completed) == len(prep["projects"]) == 2, "Project coverage differs")
        observations, result_pins = [], {}
        for project in prep["projects"]:
            done = completed[project["pack_id"]]
            path = project["native_result"]
            require(done["native_result"] == path and sha(path) == done["sha256"], "Native receipt changed")
            result_pins[path] = sha(path)
            requests = read(project["manifest"])["cases"]
            rows = validate(read(path), requests, [r for r in ledger["rows"] if r["pack_id"] == project["pack_id"]])
            for row in rows:
                row["pack_id"] = project["pack_id"]
            observations.extend(rows)
            state["native_materials_reopened"] += len(requests)
        for copied in prep["copies"]:
            require(sha(copied["original"]) == copied["sha256"], "Original source changed")
        for p, expected in result_pins.items():
            require(sha(p) == expected, "Native result changed during reopening")
        require(len(observations) == 13 and state["native_materials_reopened"] == 13, "Binding coverage incomplete")
        state.update(status="native_material_observations_independently_reopened", observations=observations,
                     native_result_pins=result_pins, original_sources_unchanged=True,
                     missing_authored_texture_substitution_performed=False, original_project_pipeline_configuration=False)
    except Exception as error:
        state.update(status="refused_or_partial", error=str(error))
    state["finished_utc"] = datetime.datetime.now(datetime.timezone.utc).isoformat()
    save("RUN-RESULT.v1.json", state)
    if state["status"] == "refused_or_partial":
        raise SystemExit(1)


if __name__ == "__main__":
    {"controls": lambda: print(json.dumps(controls())), "prepare": prepare, "run": run}[sys.argv[1]]()
