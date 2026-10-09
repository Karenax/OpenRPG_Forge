"""Record the exact candidate receipt and live process identities in lot L01."""
import ctypes
from ctypes import wintypes
import datetime
import hashlib
import json
import os
from pathlib import Path

WORK = Path(r"F:\OpenRPG_ImportWork")
ROOT = WORK / "source-graph-scene-geometry-adapter-v2"
TRACKING = WORK / "SUIVI_LOTS_HIVEMIND"

def live_identity(pid, expected_utc):
    kernel = ctypes.WinDLL("kernel32", use_last_error=True)
    kernel.OpenProcess.argtypes = [wintypes.DWORD, wintypes.BOOL, wintypes.DWORD]
    kernel.OpenProcess.restype = wintypes.HANDLE
    kernel.CloseHandle.argtypes = [wintypes.HANDLE]
    kernel.GetExitCodeProcess.argtypes = [wintypes.HANDLE, ctypes.POINTER(wintypes.DWORD)]
    kernel.GetProcessTimes.argtypes = [wintypes.HANDLE, *[ctypes.POINTER(wintypes.FILETIME)] * 4]
    handle = kernel.OpenProcess(0x1000, False, pid)
    if not handle:
        raise RuntimeError(f"process {pid} not readable; do not infer termination")
    try:
        exit_code = wintypes.DWORD()
        if not kernel.GetExitCodeProcess(handle, ctypes.byref(exit_code)):
            raise ctypes.WinError(ctypes.get_last_error())
        times = [wintypes.FILETIME() for _ in range(4)]
        if not kernel.GetProcessTimes(handle, *[ctypes.byref(t) for t in times]):
            raise ctypes.WinError(ctypes.get_last_error())
        start = times[0].dwHighDateTime << 32 | times[0].dwLowDateTime
        epoch = datetime.datetime(1601, 1, 1, tzinfo=datetime.timezone.utc)
        expected = datetime.datetime.fromisoformat(expected_utc)
        expected_ticks = int((expected - epoch).total_seconds() * 10_000_000)
        if abs(start - expected_ticks) > 20_000_000:
            raise RuntimeError("process birth does not match the exact candidate job")
        return {"pid": pid, "creation_filetime_ticks": start, "active": exit_code.value == 259,
                "exit_code_or_still_active": exit_code.value}
    finally:
        kernel.CloseHandle(handle)

def main():
    result_path = ROOT / "RUN-RESULT.v1.json"
    result_bytes = result_path.read_bytes()
    result = json.loads(result_bytes)
    job = json.loads((ROOT / "JOB.v1.json").read_bytes())
    observed = datetime.datetime.now(datetime.timezone.utc).isoformat()
    observation = {"observed_utc": observed, "receipt": str(result_path),
                   "receipt_sha256": hashlib.sha256(result_bytes).hexdigest(),
                   "status": result["status"], "canonical_installed": False,
                   "gpu_rendered": False, "studio_verified": False, "new_qualified_prefabs": 0}
    if result["status"] == "running_native_scene_geometry_checks":
        native = json.loads((ROOT / "NATIVE-JOB.v1.json").read_bytes())
        observation["controller"] = live_identity(job["controller_pid"], job["created_utc"])
        observation["native_job"] = live_identity(native["pid"], native["created_utc"])
    elif result["status"] == "native_scene_geometry_candidate_verified":
        if result.get("actual_tests") != 6 or result.get("native_exit_code") != 0:
            raise RuntimeError("candidate success lacks six actual native tests")
        if result.get("changed_inputs") != [] or result["scene_summary"].get("actual_scenes") != 57:
            raise RuntimeError("candidate success lacks exact unchanged scene inputs")
        for name in ["stdout", "stderr"]:
            if hashlib.sha256((ROOT / f"native.{name}.log").read_bytes()).hexdigest() != result[f"{name}_sha256"]:
                raise RuntimeError("native log changed")
        observation.update(actual_tests=6, actual_exit_code=0, scene_summary=result["scene_summary"])
    elif result["status"] != "refused":
        raise RuntimeError("unexpected candidate receipt state")
    else:
        observation["error"] = result["error"]
    state_path = TRACKING / "ETAT.v1.json"
    original = state_path.read_bytes()
    state = json.loads(original)
    lot = next(batch for batch in state["batches"] if batch["id"] == "L01")
    lot["scene_geometry_candidate_previous_attempt"] = {"status": "refused", "native_exit_code": 101, "reason": "E0514: Rust 1.99 used with dependencies compiled by 1.98", "receipt": str(WORK / "source-graph-scene-geometry-adapter-v1" / "RUN-RESULT.v1.json")}
    lot["scene_geometry_candidate"] = observation
    state["updated_utc"] = observed
    temporary = state_path.with_suffix(f".json.{os.getpid()}.tmp")
    temporary.write_text(json.dumps(state, ensure_ascii=False, indent=2), encoding="utf-8")
    if state_path.read_bytes() != original:
        temporary.unlink()
        raise RuntimeError("tracking changed concurrently; retry without overwriting")
    os.replace(temporary, state_path)
    entry = (f"\n## L01.4 — assemblage candidat — {observed}\n"
             f"État : {result['status']}. Reçu : {result_path}. "
             "Contrôle des 57 scènes existantes, primitives partagées, matériaux mixtes et octets des sommets. "
             "Aucune installation dans Studio, aucun rendu GPU et aucun nouvel asset livré revendiqués.\n")
    for target in [TRACKING / "JOURNAL.md", WORK / "RUN_STATUS.md"]:
        with target.open("a", encoding="utf-8") as output:
            output.write(entry)
    print(json.dumps(observation, ensure_ascii=False))

if __name__ == "__main__":
    main()
