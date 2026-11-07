"""Run a pinned renderer diagnostic on the SDK-free Ubuntu qualification guest.

This is an OS/render smoke test; it does not qualify the installed product or GPU performance.
"""
import argparse
import datetime
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import socket
import subprocess
import time


def sha(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--run-id", required=True)
    parser.add_argument("--binary", type=Path, required=True)
    parser.add_argument("--binary-sha256", required=True)
    parser.add_argument("--source-sha256", required=True)
    args = parser.parse_args()
    if socket.gethostname() != "forge-g7-linux-clean" or os.getuid() != 1000:
        raise ValueError("Unprivileged dedicated clean guest required")
    if not re.fullmatch(r"[a-z0-9-]{1,48}", args.run_id):
        raise ValueError("Bounded new run ID required")
    for pin in (args.binary_sha256, args.source_sha256):
        if not re.fullmatch(r"[0-9a-f]{64}", pin):
            raise ValueError("SHA-256 pins required")
    if not args.binary.is_absolute() or args.binary.is_symlink() or sha(args.binary) != args.binary_sha256:
        raise ValueError("Pinned physical binary required")
    tools = {name: shutil.which(name) for name in ("rustc", "cargo", "gcc", "clang", "node")}
    if any(tools.values()):
        raise ValueError("Development SDK appeared on the clean guest")
    output = Path.home() / "qualification" / args.run_id
    output.parent.mkdir(exist_ok=True)
    output.mkdir()
    runtime = output / "runtime"
    runtime.mkdir(mode=0o700)
    result = {"schema": "forge.gate7.clean-linux-render.v1", "started_utc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
              "binary_sha256": args.binary_sha256, "source_snapshot_sha256": args.source_sha256,
              "tools_before": tools, "product_installation_qualified": False, "gpu_performance_qualified": False,
              "gate_closed": False, "succeeded": False}
    started = time.monotonic()
    try:
        with (output / "ldd.log").open("x") as stream:
            subprocess.run(["ldd", str(args.binary)], stdout=stream, stderr=subprocess.STDOUT, check=True)
        if "not found" in (output / "ldd.log").read_text():
            raise RuntimeError("Runtime prerequisite missing; see ldd.log")
        args.binary.chmod(0o755)
        command = ["xvfb-run", "--auto-servernum", "--server-args=-screen 0 1920x1080x24 -nolisten tcp",
                   str(args.binary), str(output / "render"), "--timeout-seconds", "900"]
        result["command"] = command
        env = {**os.environ, "XDG_RUNTIME_DIR": str(runtime), "XDG_CACHE_HOME": str(output / "cache"), "WGPU_BACKEND": "vulkan"}
        with (output / "render.log").open("x") as stream:
            ran = subprocess.run(command, stdout=stream, stderr=subprocess.STDOUT, env=env, timeout=960, check=False)
        result["exit_code"] = ran.returncode
        result["succeeded"] = ran.returncode == 0 and (output / "render/report.json").is_file()
    except Exception as error:
        result["error"] = str(error)
    finally:
        result["duration_seconds"] = time.monotonic() - started
        result["binary_unchanged"] = sha(args.binary) == args.binary_sha256
        result["tools_after"] = {name: shutil.which(name) for name in tools}
        result["succeeded"] &= result["binary_unchanged"] and not any(result["tools_after"].values())
        result["finished_utc"] = datetime.datetime.now(datetime.timezone.utc).isoformat()
        with (output / "receipt.json").open("x") as stream:
            json.dump(result, stream, indent=2)
        print(json.dumps(result), flush=True)
    return 0 if result["succeeded"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
