"""Supervise the frozen Linux qualification mirror; never build or modify its sources."""
import argparse
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import socket
import subprocess


def digest(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    home = Path("/home/forge")
    if socket.gethostname() != "forge-gate7-linux" or Path.home() != home:
        raise ValueError("Run only inside the Linux qualification guest")
    if args.output.parent != home or not args.output.name.startswith("native-region-linux-"):
        raise ValueError("A fresh bounded output under /home/forge is required")
    root = home / "source-001"
    manifest_path = root / "__forge_vm_source_manifest__.json"
    overlay_path = root / "__forge_vm_source_overlay_002__.json"
    manifest = json.loads(manifest_path.read_text())
    overlay = json.loads(overlay_path.read_text())
    if overlay["base_archive_sha256"] != "f909d1708d8ed53c29573dcabc65409f52eb979994eae04a0e32ad9ef3cb62a2":
        raise ValueError("Unexpected snapshot base")
    expected_overlay = {
        "apps/forge_runtime/examples/cell_streaming_region.rs": {
            "sha256": "9259f7a3f777a9d9d54045ad77203f4011e0f79cf08dc980ac4aba0070d759ff",
            "bytes": 12332,
        }
    }
    if overlay["files"] != expected_overlay:
        raise ValueError("Unexpected source overlay")
    files = manifest["files"] | expected_overlay

    def verify_sources():
        for name, expected in files.items():
            relative = Path(name)
            if relative.is_absolute() or ".." in relative.parts:
                raise ValueError("Invalid manifest path")
            path = root / relative
            if path.is_symlink() or path.resolve() != path or not path.is_file():
                raise ValueError(f"Nonphysical source: {name}")
            if path.stat().st_size != expected["bytes"] or digest(path) != expected["sha256"]:
                raise ValueError(f"Changed source: {name}")

    verify_sources()
    executable = home / "qualification-build/cargo-target/debug/examples/cell_streaming_region"
    executable_hash = digest(executable)
    if args.output.exists():
        raise ValueError("Existing diagnostic output preserved")
    environment = os.environ | {
        "TMPDIR": str(home / "qualification-build/tmp"),
        "XDG_RUNTIME_DIR": str(home / "qualification-build/runtime"),
        "WGPU_BACKEND": "vulkan", "RUST_LOG": "info",
    }
    command = ["xvfb-run", "-a", "-s", "-screen 0 1920x1080x24 -nolisten tcp",
               str(executable), str(args.output), "--timeout-seconds", "600"]
    started = datetime.now(timezone.utc).isoformat()
    log_path = args.output.with_suffix(".process.log")
    with log_path.open("xb") as log:
        try:
            completed = subprocess.run(command, cwd=root, env=environment, stdout=log,
                                       stderr=subprocess.STDOUT, timeout=630, check=False)
            exit_code = completed.returncode
        except subprocess.TimeoutExpired:
            exit_code = 124
    args.output.mkdir(exist_ok=True)
    log_path.rename(args.output / "process.log")
    verify_sources()
    if digest(executable) != executable_hash:
        raise ValueError("Diagnostic executable changed during replay")
    report_path = args.output / "report.json"
    report = json.loads(report_path.read_text()) if report_path.exists() else None
    stages = report["stages"] if report else []
    valid = exit_code == 0 and len(stages) == 3
    valid = valid and all(s["distribution"]["samples"] == 300
                          and s["resident_cells"] == 81 and s["full_cells"] == 9
                          and s["colliders_backend_synchronized"] for s in stages)
    valid = valid and stages[0]["logical_cells"] == stages[2]["logical_cells"]
    artifacts = {}
    for name in ["process.log", "report.json", "stage-0.png", "stage-1.png", "stage-2.png"]:
        path = args.output / name
        if path.is_file():
            artifacts[name] = {"bytes": path.stat().st_size, "sha256": digest(path)}
    valid = valid and len(artifacts) == 5
    receipt = {"schema": "forge.gate7.linux-cell-process.v1", "started_utc": started,
               "finished_utc": datetime.now(timezone.utc).isoformat(),
               "command": command, "exit_code": exit_code, "smoke_passed": valid,
               "source_files_verified_before_and_after": len(files),
               "source_manifest_sha256": digest(manifest_path),
               "source_overlay_sha256": digest(overlay_path),
               "executable_sha256": executable_hash, "artifacts": artifacts,
               "scope": "software_vm_synthetic_cell_smoke", "clean_machine_qualified": False,
               "performance_qualified": False, "gate_closed": False}
    with (args.output / "process.receipt.json").open("x") as stream:
        json.dump(receipt, stream, indent=2)
        stream.flush()
        os.fsync(stream.fileno())
    os.sync()
    print(json.dumps(receipt), flush=True)
    raise SystemExit(0 if valid else 1)


if __name__ == "__main__":
    main()
