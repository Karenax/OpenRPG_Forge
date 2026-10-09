"""Wait on the exact conversion process, then launch its native geometry checks once."""
import ctypes
from ctypes import wintypes
import datetime
import json
from pathlib import Path
import subprocess
import sys

W = Path(r"F:\OpenRPG_ImportWork")
CONVERSION = W / "source-graph-scene-normal-bound-glb-v1"
CHECK = Path(r"E:\OpenRPG_Forge\OpenRPG_Forge-Rust\scripts\hivemind\verify_scene_geometry_candidate_v3.py")

def run():
    job = json.loads((CONVERSION / "JOB.v1.json").read_text("utf-8"))
    kernel = ctypes.WinDLL("kernel32", use_last_error=True)
    kernel.OpenProcess.argtypes = [wintypes.DWORD, wintypes.BOOL, wintypes.DWORD]
    kernel.OpenProcess.restype = wintypes.HANDLE
    kernel.GetProcessTimes.argtypes = [wintypes.HANDLE, *[ctypes.POINTER(wintypes.FILETIME)] * 4]
    kernel.WaitForSingleObject.argtypes = [wintypes.HANDLE, wintypes.DWORD]
    kernel.CloseHandle.argtypes = [wintypes.HANDLE]
    handle = kernel.OpenProcess(0x101000, False, job["pid"])
    if handle:
        try:
            times = [wintypes.FILETIME() for _ in range(4)]
            if not kernel.GetProcessTimes(handle, *[ctypes.byref(item) for item in times]):
                raise ctypes.WinError(ctypes.get_last_error())
            born = times[0].dwHighDateTime << 32 | times[0].dwLowDateTime
            expected = datetime.datetime.fromisoformat(job["created_utc"])
            epoch = datetime.datetime(1601, 1, 1, tzinfo=datetime.timezone.utc)
            if abs(born - int((expected - epoch).total_seconds() * 10_000_000)) > 20_000_000:
                raise RuntimeError("Conversion PID recycled; do not wait on another process")
            while True:
                result = kernel.WaitForSingleObject(handle, 45000)
                if result == 0:
                    break
                if result != 258:
                    raise ctypes.WinError(ctypes.get_last_error())
        finally:
            kernel.CloseHandle(handle)
    elif ctypes.get_last_error() != 87:
        raise ctypes.WinError(ctypes.get_last_error())
    state = json.loads((CONVERSION / "RUN-RESULT.v1.json").read_text("utf-8"))
    if state["status"] != "39_derivative_glb_normal_bindings_reopened_18_builtin_cases_deferred" or len(state["converted_derivatives"]) != 39:
        raise RuntimeError("Native validation withheld: complete expected conversion absent")
    subprocess.run([sys.executable, str(CHECK), "prepare"], check=True, creationflags=subprocess.CREATE_NO_WINDOW)

def prepare():
    with (CONVERSION / "geometry-queue.log").open("xb") as log:
        child = subprocess.Popen([sys.executable, str(Path(__file__)), "run"], cwd=CONVERSION, stdin=subprocess.DEVNULL, stdout=log, stderr=log, creationflags=subprocess.CREATE_NO_WINDOW)
    receipt = {"created_utc": datetime.datetime.now(datetime.timezone.utc).isoformat(), "controller_pid": child.pid, "waits_for_exact_conversion_pid": json.loads((CONVERSION / "JOB.v1.json").read_text("utf-8"))["pid"], "native_check": str(CHECK), "native_check_started": False, "full_import": False}
    with (CONVERSION / "GEOMETRY-QUEUE.v1.json").open("x", encoding="utf-8") as output:
        json.dump(receipt, output, indent=2)
    print(json.dumps(receipt))

if __name__ == "__main__":
    {"prepare": prepare, "run": run}[sys.argv[1]]()
