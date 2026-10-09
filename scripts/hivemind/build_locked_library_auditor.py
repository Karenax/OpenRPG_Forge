"""Build the tested reader's immutable executable before replacing any audit."""
import datetime
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess

W = Path(r"F:\OpenRPG_ImportWork")
ROOT = W / "library-locked-integrity-reader-integration-v1"
TOOLCHAIN = Path(r"C:\Users\karen\.rustup\toolchains\1.98.0-x86_64-pc-windows-msvc\bin")

def read(p):
    return json.loads(p.read_text("utf-8-sig"))

def sha(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()

def save(data):
    target = ROOT / "BUILD-RESULT.v1.json"
    temporary = target.with_name(target.name + f".{os.getpid()}.tmp")
    temporary.write_text(json.dumps(data, indent=2), "utf-8")
    os.replace(temporary, target)

def main():
    if (ROOT / "BUILD-RESULT.v1.json").exists():
        raise RuntimeError("Existing build evidence preserved")
    test_path = ROOT / "RUN-RESULT.v1.json"
    proof = read(test_path)
    if proof["status"] != "native_reader_locked_hash_integration_verified" or proof["actual_tests_passed"] != 50 or not proof["prepared_source_pins_unchanged"]:
        raise RuntimeError("Fifty actual reader controls required")
    if sha(ROOT / "native-tests.stdout.log") != proof["stdout_sha256"]:
        raise RuntimeError("Native test evidence changed")
    pins = read(ROOT / "PREPARED.v1.json")["source_pins"]
    result = {"status": "building_tested_locked_reader", "started_utc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
              "parent_tests_sha256": sha(test_path), "prior_audit_stopped": False,
              "full_library_audit_executed": False, "full_import": False}
    save(result)
    env = os.environ.copy()
    env.update(RUSTC=str(TOOLCHAIN / "rustc.exe"), RUSTDOC=str(TOOLCHAIN / "rustdoc.exe"),
               RUSTUP_TOOLCHAIN="1.98.0-x86_64-pc-windows-msvc", CARGO_INCREMENTAL="0",
               CARGO_TARGET_DIR=str(W / "asset-library-all-entries-native-audit-v2/target"),
               TEMP=str(ROOT / "temp"), TMP=str(ROOT / "temp"))
    env["PATH"] = str(TOOLCHAIN) + os.pathsep + env.get("PATH", "")
    try:
        if any(sha(Path(p)) != h for p, h in pins.items()):
            raise RuntimeError("Prepared reader sources changed")
        with (ROOT / "build.stdout.log").open("xb") as out, (ROOT / "build.stderr.log").open("xb") as err:
            process = subprocess.Popen([str(TOOLCHAIN / "cargo.exe"), "build", "--locked", "--offline", "-j1"],
                cwd=ROOT, env=env, stdout=out, stderr=err, stdin=subprocess.DEVNULL,
                creationflags=subprocess.CREATE_NO_WINDOW)
            (ROOT / "BUILD-JOB.v1.json").write_text(json.dumps({"pid": process.pid,
                "created_utc": datetime.datetime.now(datetime.timezone.utc).isoformat()}), "utf-8")
            code = process.wait()
        result["actual_build_exit_code"] = code
        if code or any(sha(Path(p)) != h for p, h in pins.items()):
            raise RuntimeError("Build failed or exact tested sources changed")
        original = W / "asset-library-all-entries-native-audit-v2/target/debug/openrpg-locked-library-reader-integration-v1.exe"
        destination = ROOT / "library-audit.locked.immutable.v1.exe"
        if destination.exists():
            raise RuntimeError("Existing immutable executable preserved")
        shutil.copyfile(original, destination)
        if sha(original) != sha(destination):
            raise RuntimeError("Executable copy differs")
        result.update(status="tested_locked_reader_immutable_executable_ready", executable=str(destination),
                      executable_sha256=sha(destination), tested_source_pins_unchanged=True)
    except Exception as error:
        result.update(status="failed_or_unverified", error=str(error))
    result["finished_utc"] = datetime.datetime.now(datetime.timezone.utc).isoformat()
    save(result)
    print(json.dumps(result))
    if result["status"] == "failed_or_unverified":
        raise SystemExit(1)

if __name__ == "__main__":
    main()
