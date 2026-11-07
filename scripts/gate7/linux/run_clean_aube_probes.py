"""Run the real embedded Aube/IPC diagnostics on the dedicated SDK-free guest."""
import argparse
import datetime
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import signal
import socket
import subprocess


def sha(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--run-id", required=True)
    parser.add_argument("--binary", type=Path, required=True)
    parser.add_argument("--binary-sha256", required=True)
    args = parser.parse_args()
    if socket.gethostname() != "forge-g7-linux-clean" or os.getuid() != 1000:
        raise ValueError("Dedicated unprivileged clean guest required")
    if not re.fullmatch(r"[a-z0-9-]{1,48}", args.run_id):
        raise ValueError("Bounded fresh run ID required")
    if (not re.fullmatch(r"[0-9a-f]{64}", args.binary_sha256) or not args.binary.is_absolute()
            or args.binary.is_symlink() or sha(args.binary) != args.binary_sha256):
        raise ValueError("Pinned physical binary required")
    tools = {name: shutil.which(name) for name in ("rustc", "cargo", "gcc", "clang", "node")}
    if any(tools.values()):
        raise ValueError("No development SDK permitted")
    output = Path.home() / "qualification" / args.run_id
    output.mkdir()
    runtime = output / "runtime"
    runtime.mkdir(mode=0o700)
    result = {"schema": "forge.gate7.clean-linux-aube-probes.v1", "started_utc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
              "binary_sha256": args.binary_sha256, "tools_before": tools, "checks": [],
              "gate_closed": False, "installed_product_qualified": False, "succeeded": False}
    env = {**os.environ, "XDG_RUNTIME_DIR": str(runtime), "XDG_DATA_HOME": str(output / "data"),
           "XDG_CACHE_HOME": str(output / "cache"), "XDG_CONFIG_HOME": str(output / "config")}
    args.binary.chmod(0o755)
    try:
        for mode in ("webview", "exit-menu"):
            directory = output / mode
            directory.mkdir()
            report = directory / "probe.json"
            command = ["dbus-run-session", "--", "xvfb-run", "-a", "-s", "-screen 0 1920x1080x24 -nolisten tcp",
                       str(args.binary), "--verify-" + mode, str(report)]
            with (output / (mode + ".log")).open("x") as log:
                child = subprocess.Popen(command, stdout=log, stderr=subprocess.STDOUT, env=env, start_new_session=True)
                try:
                    child.wait(timeout=45)
                finally:
                    try:
                        os.killpg(child.pid, signal.SIGTERM)
                    except ProcessLookupError:
                        pass
                    try:
                        child.wait(timeout=10)
                    except subprocess.TimeoutExpired:
                        os.killpg(child.pid, signal.SIGKILL)
                        child.wait(timeout=10)
            observed = json.loads(report.read_text()) if report.exists() else None
            result["checks"].append({"mode": mode, "exit_code": child.returncode, "report": observed})
        # Receipt verdicts are mandatory: a zero process status alone is insufficient.
        result["succeeded"] = all(check["exit_code"] == 0 and check["report"] and check["report"]["pass"]
                                  for check in result["checks"])
    except Exception as error:
        result["error"] = str(error)
    finally:
        result["binary_unchanged"] = sha(args.binary) == args.binary_sha256
        result["tools_after"] = {name: shutil.which(name) for name in tools}
        result["succeeded"] &= result["binary_unchanged"] and not any(result["tools_after"].values())
        result["finished_utc"] = datetime.datetime.now(datetime.timezone.utc).isoformat()
        (output / "receipt.json").write_text(json.dumps(result, indent=2) + "\n")
        print(json.dumps(result), flush=True)
    return int(not result["succeeded"])


if __name__ == "__main__":
    raise SystemExit(main())
