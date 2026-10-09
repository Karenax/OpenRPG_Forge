"""Install the verified scene adapter and test the public canonical engine API."""
import datetime
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import re
import subprocess
import sys

R = Path(r"E:\OpenRPG_Forge\OpenRPG_Forge-Rust")
W = Path(r"F:\OpenRPG_ImportWork")
ROOT = W / "canonical-source-scene-geometry-integration-v1"
CARGO = Path(r"C:\Users\karen\.rustup\toolchains\1.98.0-x86_64-pc-windows-msvc\bin\cargo.exe")
CACHE = Path(r"F:\OpenRPG_Forge\relocated-work\Rust-target")


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def write(value):
    target = ROOT / "RUN-RESULT.v1.json"
    tmp = ROOT / "RUN-RESULT.v1.tmp"
    tmp.write_text(json.dumps(value, indent=2), "utf-8")
    os.replace(tmp, target)


def main():
    if ROOT.exists():
        raise RuntimeError("Previous canonical integration preserved")
    candidate = R / "scripts/hivemind/candidates/source_graph_scene_geometry.v2.rs"
    target = R / "crates/forge_engine/src/source_graph_scene_geometry.rs"
    lib = target.with_name("lib.rs")
    prior_path = W / "source-graph-scene-geometry-adapter-v3/RUN-RESULT.v1.json"
    prior = json.loads(prior_path.read_text("utf-8"))
    if prior["status"] != "native_scene_geometry_candidate_verified" or prior["actual_tests"] != 6:
        raise RuntimeError("Six verified candidate checks required")
    if sha(candidate) != prior["source_pins"][str(candidate)]:
        raise RuntimeError("Verified candidate changed")
    if target.exists():
        raise RuntimeError("Canonical geometry module already exists; preserve")
    before = lib.read_bytes()
    declaration = b"pub mod source_graph_scene;"
    if before.count(declaration) != 1 or b"pub mod source_graph_scene_geometry;" in before:
        raise RuntimeError("Canonical module insertion point changed")
    helper = R / "scripts/hivemind/verify_scene_geometry_candidate_v3.py"
    if sha(helper) != prior["source_pins"][str(helper)]:
        raise RuntimeError("Verified candidate test harness changed")
    spec = importlib.util.spec_from_file_location("verified_scene_checks", helper)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    native = module.NATIVE_CHECK
    imports = "mod source_graph_scene;\nmod source_graph_scene_geometry;"
    if native.count(imports) != 1:
        raise RuntimeError("Test imports changed")
    native = native.replace(imports, "use forge_engine::source_graph_scene_geometry;")
    inputs = W / "source-graph-scene-normal-bound-glb-v1/INPUTS.v1.json"
    rows = json.loads(inputs.read_text("utf-8-sig"))
    if len(rows) != 39 or any(sha(Path(row["glb"])) != row["sha256"] for row in rows):
        raise RuntimeError("Exact 39 unchanged GLBs required")
    (ROOT / "src").mkdir(parents=True)
    (ROOT / "lib.before.rs").write_bytes(before)
    (ROOT / "candidate.rs").write_bytes(candidate.read_bytes())
    (ROOT / "src/main.rs").write_text(native, "utf-8")
    (ROOT / "INPUTS.v1.json").write_bytes(inputs.read_bytes())
    (ROOT / "Cargo.toml").write_text('''[package]
name="openrpg-canonical-source-scene-geometry-check"
version="0.1.0"
edition="2024"
[workspace]
[dependencies]
forge_engine={path="E:/OpenRPG_Forge/OpenRPG_Forge-Rust/crates/forge_engine"}
forge_assets={path="E:/OpenRPG_Forge/OpenRPG_Forge-Rust/crates/forge_assets"}
forge_core={path="E:/OpenRPG_Forge/OpenRPG_Forge-Rust/crates/forge_core"}
serde_json="=1.0.151"
''', "utf-8")
    if lib.read_bytes() != before:
        raise RuntimeError("Canonical module list changed concurrently")
    target.write_bytes(candidate.read_bytes())
    lib.write_bytes(before.replace(declaration, declaration + b"\npub mod source_graph_scene_geometry;"))
    pins = {str(p): sha(p) for p in [target, candidate, lib, helper, inputs, prior_path,
            Path(__file__).resolve(), ROOT / "Cargo.toml", ROOT / "src/main.rs", ROOT / "INPUTS.v1.json"]}
    for name in ["forge_engine", "forge_assets", "forge_core"]:
        base = R / "crates" / name
        for p in [base / "Cargo.toml", *sorted((base / "src").rglob("*.rs"))]:
            pins[str(p)] = sha(p)
    result = {"status":"testing_canonical_scene_geometry", "started_utc":datetime.datetime.now(datetime.timezone.utc).isoformat(),
              "canonical_installed":True,"candidate_byte_identical":sha(target)==sha(candidate),
              "source_pins":pins,"actual_tests":0,"gpu_rendered":False,"studio_verified":False,"full_import":False}
    write(result)
    environment = os.environ.copy()
    environment.update(CARGO_TARGET_DIR=str(CACHE), CARGO_INCREMENTAL="0", CARGO_BUILD_JOBS="1",
                       RUSTC=str(CARGO.parent / "rustc.exe"), RUSTDOC=str(CARGO.parent / "rustdoc.exe"),
                       RUSTUP_TOOLCHAIN="1.98.0-x86_64-pc-windows-msvc", TEMP=str(ROOT), TMP=str(ROOT))
    environment["PATH"] = str(CARGO.parent) + os.pathsep + environment.get("PATH", "")
    try:
        for stage, args in [("lock", ["generate-lockfile","--offline"]),
                            ("native", ["test","--locked","--offline","-j1","scene_geometry_checks::","--","--nocapture"])]:
            with (ROOT / (stage+".stdout.log")).open("wb") as out, (ROOT / (stage+".stderr.log")).open("wb") as err:
                child = subprocess.Popen([str(CARGO),*args],cwd=ROOT,env=environment,stdout=out,stderr=err,
                                         creationflags=subprocess.CREATE_NO_WINDOW)
                (ROOT / "CARGO-JOB.v1.json").write_text(json.dumps({"pid":child.pid,"stage":stage,"started_utc":datetime.datetime.now(datetime.timezone.utc).isoformat()}),"utf-8")
                code = child.wait()
            result[stage+"_exit_code"] = code
            write(result)
            if code != 0:
                raise RuntimeError(stage+" check failed; logs retained")
        output = (ROOT / "native.stdout.log").read_text("utf-8")
        summary = re.search(r"test result: ok\. (\d+) passed; 0 failed",output)
        totals = [json.loads(line) for line in output.splitlines() if line.startswith('{') and '"actual_scenes"' in line]
        if not summary or int(summary.group(1))!=6 or len(totals)!=1 or totals[0]["actual_scenes"]!=39:
            raise RuntimeError("Six actual canonical tests over 39 scenes not proven")
        if any(sha(Path(p))!=expected for p,expected in pins.items()) or any(sha(Path(row["glb"]))!=row["sha256"] for row in rows):
            raise RuntimeError("Canonical source or GLB changed during verification")
        result.update(status="canonical_scene_geometry_native_verified",actual_tests=6,scene_summary=totals[0],
                      all_input_pins_unchanged=True,stdout_sha256=sha(ROOT/"native.stdout.log"))
    except Exception as error:
        result.update(status="refused_or_unverified",error=str(error))
    result["finished_utc"] = datetime.datetime.now(datetime.timezone.utc).isoformat()
    write(result)
    print(json.dumps({k:v for k,v in result.items() if k!="source_pins"}))
    if result["status"] != "canonical_scene_geometry_native_verified":
        raise SystemExit(1)


if __name__ == "__main__":
    main()
