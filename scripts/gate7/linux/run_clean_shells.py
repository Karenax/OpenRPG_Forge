"""Observe pinned native shells on the SDK-free guest; no product qualification claim."""
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
import time


def sha(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--run-id", required=True)
    parser.add_argument("--launcher", type=Path, required=True)
    parser.add_argument("--launcher-sha256", required=True)
    parser.add_argument("--hub", type=Path, required=True)
    parser.add_argument("--hub-sha256", required=True)
    args = parser.parse_args()
    if socket.gethostname() != "forge-g7-linux-clean" or os.getuid() != 1000:
        raise ValueError("Dedicated unprivileged clean guest required")
    if not re.fullmatch(r"[a-z0-9-]{1,48}", args.run_id):
        raise ValueError("Bounded fresh run ID required")
    tools = {name: shutil.which(name) for name in ("rustc", "cargo", "gcc", "clang", "node")}
    if any(tools.values()) or not os.environ.get("DISPLAY") or not os.environ.get("DBUS_SESSION_BUS_ADDRESS"):
        raise ValueError("SDK-free X11 and D-Bus session required")
    output = Path.home() / "qualification" / args.run_id
    output.parent.mkdir(exist_ok=True)
    output.mkdir()
    runtime = output / "runtime"
    runtime.mkdir(mode=0o700)
    result = {"schema": "forge.gate7.clean-linux-shell-observation.v1", "started_utc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
              "tools_before": tools, "observations": [], "product_qualified": False, "gate_closed": False}
    env = {**os.environ, "XDG_RUNTIME_DIR": str(runtime), "XDG_DATA_HOME": str(output / "data"),
           "XDG_CONFIG_HOME": str(output / "config"), "XDG_CACHE_HOME": str(output / "cache")}
    try:
        for name, binary, pin in [("aube", args.launcher, args.launcher_sha256), ("hub", args.hub, args.hub_sha256)]:
            if not re.fullmatch(r"[0-9a-f]{64}", pin) or not binary.is_absolute() or binary.is_symlink() or sha(binary) != pin:
                raise ValueError("Pinned physical binary required")
            binary.chmod(0o755)
            observation = {"name": name, "binary_sha256": pin, "captures": []}
            result["observations"].append(observation)
            with (output / (name + ".ldd.log")).open("x") as stream:
                subprocess.run(["ldd", str(binary)], stdout=stream, stderr=subprocess.STDOUT, check=True)
            if "not found" in (output / (name + ".ldd.log")).read_text():
                raise RuntimeError("Runtime prerequisite missing")
            command = [str(binary)]
            if name == "hub":
                command += ["--root", str(output / "installation")]
            child = None
            try:
                with (output / (name + ".log")).open("x") as stream:
                    child = subprocess.Popen(command, stdout=stream, stderr=subprocess.STDOUT, env=env, start_new_session=True)
                    observation["pid"] = child.pid
                    for index in range(2):
                        time.sleep(8)
                        capture = output / (name + "-" + str(index) + ".png")
                        subprocess.run(["scrot", str(capture)], check=True, timeout=20)
                        observation["captures"].append({"name": capture.name, "sha256": sha(capture), "alive": child.poll() is None})
                        if child.poll() is not None:
                            break
                    observation["alive_at_observation_end"] = child.poll() is None
                    observation["exit_before_cleanup"] = child.poll()
            finally:
                if child is not None:
                    try:
                        os.killpg(child.pid, signal.SIGTERM)
                    except ProcessLookupError:
                        pass
                    try:
                        child.wait(timeout=10)
                    except subprocess.TimeoutExpired:
                        os.killpg(child.pid, signal.SIGKILL)
                        child.wait(timeout=10)
                    observation["exit_after_cleanup"] = child.returncode
                    observation["process_stopped"] = child.poll() is not None
            observation["binary_unchanged"] = sha(binary) == pin
    except Exception as error:
        result["error"] = str(error)
    finally:
        result["tools_after"] = {name: shutil.which(name) for name in tools}
        result["all_processes_stopped"] = all(o.get("process_stopped", False) for o in result["observations"])
        result["visual_review_required"] = True
        result["finished_utc"] = datetime.datetime.now(datetime.timezone.utc).isoformat()
        (output / "receipt.json").write_text(json.dumps(result, indent=2) + "\n")
        print(json.dumps(result), flush=True)
    return int("error" in result or not result["all_processes_stopped"])


if __name__ == "__main__":
    raise SystemExit(main())
