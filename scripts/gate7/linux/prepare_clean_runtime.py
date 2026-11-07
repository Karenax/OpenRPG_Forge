"""Record explicit runtime prerequisites on the fresh Ubuntu qualification guest.

This prepares a display/runtime test environment; it is not a product installer.
No compiler, Rust, Node or development library is requested.
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


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--run-id", default="runtime-prerequisites-001")
    parser.add_argument("--ui-observation-tools", action="store_true")
    args = parser.parse_args()
    if not re.fullmatch(r"[a-z0-9-]{1,48}", args.run_id):
        raise ValueError("Bounded fresh run ID required")
    baseline_path = Path("/var/lib/forge-qualification/initial.json")
    baseline = json.loads(baseline_path.read_text())
    if (os.geteuid() != 0 or socket.gethostname() != "forge-g7-linux-clean"
            or baseline["source_image_sha256"] != "04e8f2461a16a44e78a370284471f29616cdd9baf1893df25807b71f985b6c94"
            or baseline["product_installed"] or baseline["sdk_installed_by_qualification"]):
        raise ValueError("Fresh dedicated Ubuntu qualification guest required")
    tools = ("rustc", "cargo", "gcc", "clang", "node")
    if any(shutil.which(name) for name in tools):
        raise ValueError("No development SDK permitted on this guest")
    output = baseline_path.parent / args.run_id
    output.mkdir()
    packages = ["libwebkit2gtk-4.1-0", "libayatana-appindicator3-1", "libxdo3",
                "libasound2t64", "libudev1", "libx11-6", "libxi6", "libxcursor1",
                "libxrandr2", "libxinerama1", "libwayland-client0", "libxkbcommon0",
                "libxkbcommon-x11-0", "libvulkan1", "mesa-vulkan-drivers", "xvfb", "xauth", "scrot"]
    if args.ui_observation_tools:
        packages += ["xdotool", "at-spi2-core", "python3-pyatspi"]
    result = {"schema": "forge.gate7.clean-linux-runtime-prerequisites.v1",
              "started_utc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
              "baseline_sha256": hashlib.sha256(baseline_path.read_bytes()).hexdigest(),
              "requested_packages": packages, "product_installed": False,
              "ui_observation_tools_requested": args.ui_observation_tools,
              "sdk_requested": False, "product_qualified": False, "succeeded": False}
    try:
        env = {**os.environ, "DEBIAN_FRONTEND": "noninteractive"}
        for name, command in [("apt-plan.log", ["apt-get", "--simulate", "install", "--no-install-recommends", *packages]),
                              ("apt-install.log", ["apt-get", "install", "-y", "--no-install-recommends", *packages]),
                              ("packages.log", ["dpkg-query", "-W"])]:
            with (output / name).open("x") as log:
                subprocess.run(command, stdout=log, stderr=subprocess.STDOUT, env=env, check=True)
        result["tools_after"] = {name: shutil.which(name) for name in tools}
        result["succeeded"] = not any(result["tools_after"].values())
    except Exception as error:
        result["error"] = str(error)
    finally:
        result["finished_utc"] = datetime.datetime.now(datetime.timezone.utc).isoformat()
        with (output / "receipt.json").open("x") as stream:
            json.dump(result, stream, indent=2)
        print(json.dumps(result))
    return 0 if result["succeeded"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
