"""Compile actual Studio registration and attachment after their predecessor."""
import datetime
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import sys

R = Path(r"E:\OpenRPG_Forge\OpenRPG_Forge-Rust")
W = Path(r"F:\OpenRPG_ImportWork")
ROOT = W / "studio-source-scene-resources-native-v1"
PRIOR = W / "studio-source-scene-attachment-native-v1"
SOURCES = [R / "apps/forge_studio/src/authoring_assets" / name for name in ["source_graph_scene.rs", "source_graph_resources.rs"]]
BIN = Path(r"C:\Users\karen\.rustup\toolchains\1.98.0-x86_64-pc-windows-msvc\bin")
PS = r"C:\Users\karen\.cache\codex-runtimes\codex-primary-runtime\dependencies\native\powershell\pwsh.exe"


def read(p):
    return json.loads(Path(p).read_text("utf-8-sig"))


def sha(p):
    with Path(p).open("rb") as f:
        return hashlib.file_digest(f, "sha256").hexdigest()


def save(name, value):
    path = ROOT / name
    temporary = path.with_suffix(".tmp")
    temporary.write_text(json.dumps(value, indent=2), "utf-8")
    os.replace(temporary, path)


def prepare():
    if ROOT.exists():
        raise RuntimeError("Prior resource check preserved")
    ticks = subprocess.check_output([PS, "-NoProfile", "-Command",
        "(Get-Process -Id 46380 -ErrorAction Stop).StartTime.ToUniversalTime().Ticks"], text=True).strip()
    if not ticks.isdigit():
        raise RuntimeError("Live attachment check required")
    ROOT.mkdir()
    (ROOT / "src").mkdir()
    dependencies = '\n'.join(f'{name}={{path="{(R / "crates" / name).as_posix()}"}}' for name in ["forge_engine", "forge_assets"])
    (ROOT / "Cargo.toml").write_text('[package]\nname="openrpg-studio-source-scene-resource-check"\nversion="0.1.0"\nedition="2024"\n[workspace]\n[dependencies]\n' + dependencies + '\nbevy={version="=0.19.1",default-features=false,features=["3d","ui"]}\n', "utf-8")
    wrapper = '\n'.join(f'#[allow(dead_code)]\n#[path="{p.as_posix()}"]\nmod {p.stem};' for p in SOURCES) + '\nfn main(){}\n'
    (ROOT / "src/main.rs").write_text(wrapper, "utf-8")
    pins = {str(p): sha(p) for p in [*SOURCES, Path(__file__).resolve(), ROOT / "Cargo.toml", ROOT / "src/main.rs"]}
    save("PREPARED.v1.json", {"wait_pid": 46380, "wait_birth_ticks": ticks, "input_pins": pins,
         "expected_tests": 9, "actual_gpu_tests": 0, "full_studio_application_check": False})
    with (ROOT / "controller.log").open("xb") as log:
        child = subprocess.Popen([sys.executable, str(Path(__file__).resolve()), "run"], cwd=ROOT,
            stdin=subprocess.DEVNULL, stdout=log, stderr=log, creationflags=subprocess.CREATE_NO_WINDOW)
    save("JOB.v1.json", {"pid": child.pid, "launched_utc": datetime.datetime.now(datetime.timezone.utc).isoformat()})
    print(json.dumps({"queued": True, "pid": child.pid, "expected_tests": 9, "actual_tests_passed": 0}))


def run():
    state = {"status": "waiting_for_attachment_native_check", "actual_tests_passed": 0,
             "gpu_rendered": False, "actual_studio_preview_loaded": False, "full_import": False}
    save("RUN-RESULT.v1.json", state)
    try:
        prep = read(ROOT / "PREPARED.v1.json")
        wait = "$ErrorActionPreference='Stop';while($true){$p=Get-Process -Id 46380 -ErrorAction SilentlyContinue;if(!$p -or $p.StartTime.ToUniversalTime().Ticks -ne " + prep["wait_birth_ticks"] + "L){break};$p.WaitForExit(45000)|Out-Null}"
        subprocess.run([PS, "-NoProfile", "-Command", wait], check=True, creationflags=subprocess.CREATE_NO_WINDOW)
        prior_path = PRIOR / "RUN-RESULT.v1.json"
        prior = read(prior_path)
        if prior["status"] != "studio_source_attachment_structural_native_tests_verified" or prior["actual_tests_passed"] != 6 or not prior["all_input_pins_unchanged"]:
            raise RuntimeError("Attachment check incomplete/refused; no competing compilation")
        pins = {**prep["input_pins"], **prior["input_pins"], str(prior_path): sha(prior_path)}
        if any(sha(p) != expected for p, expected in pins.items()):
            raise RuntimeError("Registration check input changed")
        env = os.environ.copy()
        env.update(CARGO_TARGET_DIR=r"F:\OpenRPG_Forge\relocated-work\Rust-target", CARGO_INCREMENTAL="0", CARGO_BUILD_JOBS="1",
            RUSTC=str(BIN / "rustc.exe"), RUSTDOC=str(BIN / "rustdoc.exe"),
            RUSTUP_TOOLCHAIN="1.98.0-x86_64-pc-windows-msvc", TEMP=str(ROOT), TMP=str(ROOT))
        env["PATH"] = str(BIN) + os.pathsep + env.get("PATH", "")
        for stage, args in [("lock", ["generate-lockfile", "--offline"]),
                            ("native", ["test", "--locked", "--offline", "-j1", "--", "--nocapture"])]:
            state["status"] = "checking_actual_studio_source_modules_" + stage
            save("RUN-RESULT.v1.json", state)
            with (ROOT / (stage + ".stdout.log")).open("xb") as out, (ROOT / (stage + ".stderr.log")).open("xb") as err:
                child = subprocess.Popen([str(BIN / "cargo.exe"), *args], cwd=ROOT, env=env, stdout=out, stderr=err,
                    creationflags=subprocess.CREATE_NO_WINDOW)
                save("CARGO-JOB.v1.json", {"pid": child.pid, "stage": stage})
                code = child.wait()
            state[stage + "_exit_code"] = code
            save("RUN-RESULT.v1.json", state)
            if code != 0:
                raise RuntimeError(stage + " compilation/tests failed; logs preserved")
        stdout = (ROOT / "native.stdout.log").read_text("utf-8")
        result = re.search(r"test result: ok\. (\d+) passed; 0 failed", stdout)
        if not result or int(result.group(1)) != 9:
            raise RuntimeError("Nine actual module tests not proven")
        if any(sha(p) != expected for p, expected in pins.items()):
            raise RuntimeError("Source changed during module checks")
        state.update(status="actual_studio_source_resource_and_attachment_modules_native_checked", actual_tests_passed=9,
            all_input_pins_unchanged=True, input_pins=pins, stdout_sha256=sha(ROOT / "native.stdout.log"),
            complete_studio_application_compiled=False, source_gpu_resource_registration_exercised=False,
            original_shader_render_parity_qualified=False)
    except Exception as error:
        state.update(status="refused_or_unverified", error=str(error))
    state["finished_utc"] = datetime.datetime.now(datetime.timezone.utc).isoformat()
    save("RUN-RESULT.v1.json", state)
    if state["status"] == "refused_or_unverified":
        raise SystemExit(1)


if __name__ == "__main__":
    {"prepare": prepare, "run": run}[sys.argv[1]]()
