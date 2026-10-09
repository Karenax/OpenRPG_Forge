"""Validate bounded Windows read locks on F: without starting another full audit."""
import datetime
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess

W = Path(r"F:\OpenRPG_ImportWork")
ROOT = W / "library-locked-integrity-batch-candidate-v1"
CANDIDATE = Path(__file__).parent / "candidates/locked_integrity_batch.rs"
TOOLCHAIN = Path(r"C:\Users\karen\.rustup\toolchains\1.98.0-x86_64-pc-windows-msvc\bin")

def sha(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()

def save(name, data):
    path = ROOT / name
    temporary = path.with_name(path.name + f".{os.getpid()}.tmp")
    temporary.write_text(json.dumps(data, indent=2, ensure_ascii=False), "utf-8")
    os.replace(temporary, path)

def main():
    if ROOT.exists():
        raise RuntimeError("Existing candidate retained; inspect its exact job")
    (ROOT / "src").mkdir(parents=True)
    (ROOT / "temp").mkdir()
    (ROOT / "src/lib.rs").write_bytes(CANDIDATE.read_bytes())
    (ROOT / "Cargo.toml").write_text('''[package]
name="openrpg-locked-integrity-batch-controls"
version="0.1.0"
edition="2024"
[workspace]
[dependencies]
sha2="0.10"
serde_json="1"
''', "utf-8")
    pins = {str(p): sha(p) for p in [CANDIDATE, Path(__file__).resolve(), ROOT / "src/lib.rs", ROOT / "Cargo.toml"]}
    save("PREPARED.v1.json", {"source_pins": pins, "canonical_installed": False,
                            "existing_full_audit_stopped": False, "full_import": False})
    result = {"status": "testing_windows_locked_hash_batch", "started_utc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
              "canonical_installed": False, "full_library_audit_executed": False, "full_import": False}
    save("RUN-RESULT.v1.json", result)
    environment = os.environ.copy()
    environment.update(RUSTC=str(TOOLCHAIN / "rustc.exe"), RUSTDOC=str(TOOLCHAIN / "rustdoc.exe"),
                       RUSTUP_TOOLCHAIN="1.98.0-x86_64-pc-windows-msvc", CARGO_INCREMENTAL="0",
                       CARGO_TARGET_DIR=str(W / "asset-library-all-entries-native-audit-v2/target"),
                       TEMP=str(ROOT / "temp"), TMP=str(ROOT / "temp"))
    environment["PATH"] = str(TOOLCHAIN) + os.pathsep + environment.get("PATH", "")
    try:
        for stage, arguments in [("lock", ["generate-lockfile", "--offline"]),
                                  ("controls", ["test", "--locked", "--offline", "-j1", "tests::", "--", "--nocapture"])]:
            with (ROOT / f"{stage}.stdout.log").open("wb") as stdout, (ROOT / f"{stage}.stderr.log").open("wb") as stderr:
                process = subprocess.Popen([str(TOOLCHAIN / "cargo.exe"), *arguments], cwd=ROOT,
                    env=environment, stdout=stdout, stderr=stderr, stdin=subprocess.DEVNULL,
                    creationflags=subprocess.CREATE_NO_WINDOW)
                save("JOB.v1.json", {"pid": process.pid, "stage": stage,
                                   "observed_utc": datetime.datetime.now(datetime.timezone.utc).isoformat()})
                code = process.wait()
            result[f"{stage}_exit_code"] = code
            if code:
                raise RuntimeError(f"{stage} refused, inspect retained logs")
        output = (ROOT / "controls.stdout.log").read_text("utf-8-sig")
        if not re.search(r"test result: ok\. 5 passed; 0 failed", output):
            raise RuntimeError("Five actual Windows tests absent")
        if any(sha(Path(p)) != expected for p, expected in pins.items()):
            raise RuntimeError("Candidate changed during tests")
        result.update(status="five_windows_locked_hash_batch_controls_verified", actual_tests_passed=5,
                      source_pins_unchanged=True, controls_stdout_sha256=sha(ROOT / "controls.stdout.log"),
                      controls_stderr_sha256=sha(ROOT / "controls.stderr.log"))
    except Exception as error:
        result.update(status="refused_or_unverified", error=str(error))
    result["finished_utc"] = datetime.datetime.now(datetime.timezone.utc).isoformat()
    save("RUN-RESULT.v1.json", result)
    print(json.dumps(result))
    if result["status"] == "refused_or_unverified":
        raise SystemExit(1)

if __name__ == "__main__":
    main()
