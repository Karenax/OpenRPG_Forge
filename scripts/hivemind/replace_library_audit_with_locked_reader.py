"""Replace only the exact owned slow audit after its complete successor is tested."""
import ctypes
from ctypes import wintypes
import datetime
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys

W = Path(r"F:\OpenRPG_ImportWork")
ROOT = W / "library-locked-integrity-reader-integration-v1"
OLD = W / "asset-library-all-entries-native-audit-v2"
OLD_PID = 19700
OLD_BORN = "2026-10-06T04:33:55.736667+00:00"
OLD_SHA = "6cbc352c13e3ffe01ee213ba619c9b8c187c7b568b97992febd889cc625d6574"

def now():
    return datetime.datetime.now(datetime.timezone.utc).isoformat()

def read(p):
    return json.loads(p.read_text("utf-8-sig"))

def sha(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()

def save(name, value):
    path = ROOT / name
    temporary = path.with_name(path.name + f".{os.getpid()}.tmp")
    temporary.write_text(json.dumps(value, indent=2), "utf-8")
    os.replace(temporary, path)

def replace():
    if (ROOT / "AUDIT-REPLACEMENT.v1.json").exists():
        raise RuntimeError("Existing replacement preserved; inspect its exact audit handle")
    if (OLD / "READER-RESULT.v1.json").exists():
        raise RuntimeError("Prior audit completed; reopen its evidence instead of repeating")
    build_path = ROOT / "BUILD-RESULT.v1.json"
    build = read(build_path)
    tests_path = ROOT / "RUN-RESULT.v1.json"
    tests = read(tests_path)
    if build["status"] != "tested_locked_reader_immutable_executable_ready" or build["actual_build_exit_code"] != 0:
        raise RuntimeError("Ready immutable successor absent")
    if tests["actual_tests_passed"] != 50 or sha(tests_path) != build["parent_tests_sha256"]:
        raise RuntimeError("Exact fifty-test parent changed")
    executable = Path(build["executable"])
    if sha(executable) != build["executable_sha256"]:
        raise RuntimeError("Immutable successor changed")
    pins = read(ROOT / "PREPARED.v1.json")["source_pins"]
    if any(sha(Path(p)) != expected for p, expected in pins.items()):
        raise RuntimeError("Tested reader inputs changed")
    old_executable = OLD / "library-audit.immutable.v1.exe"
    if sha(old_executable) != OLD_SHA:
        raise RuntimeError("Prior immutable executable identity changed")
    kernel = ctypes.WinDLL("kernel32", use_last_error=True)
    kernel.OpenProcess.argtypes = [wintypes.DWORD, wintypes.BOOL, wintypes.DWORD]
    kernel.OpenProcess.restype = wintypes.HANDLE
    kernel.GetProcessTimes.argtypes = [wintypes.HANDLE, *[ctypes.POINTER(wintypes.FILETIME)] * 4]
    kernel.QueryFullProcessImageNameW.argtypes = [wintypes.HANDLE, wintypes.DWORD, wintypes.LPWSTR, ctypes.POINTER(wintypes.DWORD)]
    kernel.TerminateProcess.argtypes = [wintypes.HANDLE, wintypes.UINT]
    kernel.WaitForSingleObject.argtypes = [wintypes.HANDLE, wintypes.DWORD]
    kernel.CloseHandle.argtypes = [wintypes.HANDLE]
    handle = kernel.OpenProcess(0x101001, False, OLD_PID)
    if not handle:
        raise RuntimeError("Prior audit handle missing; inspect its terminal state before replacement")
    try:
        times = [wintypes.FILETIME() for _ in range(4)]
        if not kernel.GetProcessTimes(handle, *[ctypes.byref(t) for t in times]):
            raise ctypes.WinError(ctypes.get_last_error())
        born = times[0].dwHighDateTime << 32 | times[0].dwLowDateTime
        epoch = datetime.datetime(1601, 1, 1, tzinfo=datetime.timezone.utc)
        expected = int((datetime.datetime.fromisoformat(OLD_BORN) - epoch).total_seconds() * 10000000)
        if abs(born - expected) > 10000000:
            raise RuntimeError("Prior PID reused; no process stopped")
        size = wintypes.DWORD(32768)
        buffer = ctypes.create_unicode_buffer(size.value)
        if not kernel.QueryFullProcessImageNameW(handle, 0, buffer, ctypes.byref(size)):
            raise ctypes.WinError(ctypes.get_last_error())
        if Path(buffer.value).resolve() != old_executable.resolve():
            raise RuntimeError("Different prior executable; no process stopped")
        if (OLD / "READER-RESULT.v1.json").exists():
            raise RuntimeError("Prior audit finished during preparation; preserved")
        replacement = {"status": "successor_ready_replacing_owned_audit", "started_utc": now(),
                       "prior_pid": OLD_PID, "prior_process_created_utc": OLD_BORN,
                       "prior_executable_sha256": OLD_SHA, "prior_run_receipt": read(OLD / "RUN-RESULT.v1.json"),
                       "successor_executable": str(executable), "successor_sha256": build["executable_sha256"],
                       "successor_build_receipt_sha256": sha(build_path), "actual_tests_passed": 50,
                       "reason": "Same full native audit scope; avoid repeated source/model SHA reads using Windows read locks",
                       "prior_outputs_preserved": True, "full_import": False}
        save("AUDIT-REPLACEMENT.v1.json", replacement)
        if not kernel.TerminateProcess(handle, 995):
            raise ctypes.WinError(ctypes.get_last_error())
        if kernel.WaitForSingleObject(handle, 10000) != 0:
            raise RuntimeError("Prior termination not confirmed; successor withheld")
        replacement.update(status="owned_prior_audit_superseded", prior_process_termination_confirmed=True)
        save("AUDIT-REPLACEMENT.v1.json", replacement)
    finally:
        kernel.CloseHandle(handle)
    with (ROOT / "audit-controller.log").open("xb") as log:
        child = subprocess.Popen([sys.executable, str(Path(__file__).resolve()), "watch"], cwd=ROOT,
            stdin=subprocess.DEVNULL, stdout=log, stderr=log, creationflags=subprocess.CREATE_NO_WINDOW)
    save("AUDIT-CONTROLLER-JOB.v1.json", {"pid": child.pid, "created_utc": now()})
    print(json.dumps({"prior_owned_audit_superseded": True, "successor_controller_pid": child.pid,
                      "scope_reduced": False, "full_import": False}))

def watch():
    replacement = read(ROOT / "AUDIT-REPLACEMENT.v1.json")
    executable = Path(replacement["successor_executable"])
    output = ROOT / "READER-RESULT.v1.json"
    if output.exists() or (ROOT / "LOCKED-SESSION.v1.json").exists():
        raise RuntimeError("Existing audit output preserved")
    if sha(executable) != replacement["successor_sha256"]:
        raise RuntimeError("Successor executable changed")
    result = {"status": "checking_all_current_library_entries_with_read_locks", "started_utc": now(),
              "executable_sha256": replacement["successor_sha256"], "scope_reduced": False,
              "actual_ui_visual_review": False, "full_import": False}
    save("AUDIT-RUN.v1.json", result)
    with (ROOT / "audit.stdout.log").open("xb") as stdout, (ROOT / "audit.stderr.log").open("xb") as stderr:
        child = subprocess.Popen([str(executable), str(output)], cwd=ROOT,
            stdin=subprocess.DEVNULL, stdout=stdout, stderr=stderr, creationflags=subprocess.CREATE_NO_WINDOW)
        save("AUDIT-JOB.v1.json", {"pid": child.pid, "created_utc": now(), "executable": str(executable)})
        code = child.wait()
    result["actual_exit_code"] = code
    if code == 0 and output.exists() and (ROOT / "LOCKED-SESSION.v1.json").exists():
        report = read(output)
        session = read(ROOT / "LOCKED-SESSION.v1.json")
        result.update(status="all_indexed_library_entries_native_locked_audit_completed",
                      entries_checked=report["entries_checked"], actual_locked_session=session,
                      reader_receipt_sha256=sha(output), locked_session_receipt_sha256=sha(ROOT / "LOCKED-SESSION.v1.json"))
    else:
        result.update(status="failed_or_unverified", error="Inspect retained stderr; no full-library success inferred")
    result["finished_utc"] = now()
    save("AUDIT-RUN.v1.json", result)
    print(json.dumps(result))

if __name__ == "__main__":
    {"replace": replace, "watch": watch}[sys.argv[1]]()
