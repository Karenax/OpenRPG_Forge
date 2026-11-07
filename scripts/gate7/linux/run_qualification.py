"""Build an immutable common-checkout transport; never mount the host workspace.

This SDK container is build/test evidence, not a clean desktop or graphics verdict.
The only writable source tree is a unique qualification snapshot inside the Linux
cache volume. Authored source files are checked again on success and on failure.
"""
import argparse
import datetime
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import re
import shutil
import stat
import subprocess
import sys
import zipfile


def sha(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def utc():
    return datetime.datetime.now(datetime.timezone.utc).isoformat()


def verify(root, files):
    changed = []
    for name, expected in files.items():
        path = root / name
        if path.is_symlink() or not path.is_file() or path.stat().st_size != expected["bytes"] or sha(path) != expected["sha256"]:
            changed.append(name)
    return changed


def extract(archive, destination):
    with zipfile.ZipFile(archive) as bundle:
        manifest = json.loads(bundle.read("__forge_vm_source_manifest__.json"))
        files = manifest["files"]
        names = bundle.namelist()
        if (len(files) > 50_000 or sum(item["bytes"] for item in files.values()) > 20 * 1024**3
                or len(names) != len(set(names)) or set(names) != {*files, "__forge_vm_source_manifest__.json"}):
            raise ValueError("Unexpected or oversized source transport inventory")
        # Validate every member before writing any source file.
        for name, expected in files.items():
            relative = PurePosixPath(name)
            info = bundle.getinfo(name)
            if (relative.is_absolute() or ".." in relative.parts or "\\" in name or ":" in name
                    or relative.as_posix() != name or stat.S_ISLNK(info.external_attr >> 16)
                    or info.file_size != expected["bytes"]):
                raise ValueError("Unsafe source transport member: " + name)
        destination.mkdir()
        for name, expected in files.items():
            data = bundle.read(name)
            if hashlib.sha256(data).hexdigest() != expected["sha256"]:
                raise ValueError("Source transport digest differs: " + name)
            path = destination / name
            path.parent.mkdir(parents=True, exist_ok=True)
            with path.open("xb") as stream:
                stream.write(data)
    return manifest


def retain_source_manifest(archive, output, manifest):
    """Preserve the exact source inventory after the temporary ZIP is retired."""
    with zipfile.ZipFile(archive) as bundle:
        data = bundle.read("__forge_vm_source_manifest__.json")
    if json.loads(data) != manifest:
        raise ValueError("Source transport manifest changed after extraction")
    path = output / "source-manifest.json"
    with path.open("xb") as stream:
        stream.write(data)
    return sha(path)


def resume(base, run_id, source, source_sha256, steps):
    """Reuse only an unchanged failed snapshot and verified successful prefix."""
    if not re.fullmatch(r"[a-z0-9-]{1,48}", run_id):
        raise ValueError("Bounded prior run ID required")
    previous = base / "results" / run_id
    if previous.is_symlink():
        raise ValueError("Physical prior results required")
    receipt = json.loads((previous / "receipt.json").read_text())
    if receipt["succeeded"] or not receipt["source_unchanged"] or receipt["source_sha256"] != source_sha256:
        raise ValueError("Only the same unchanged failed snapshot can be resumed")
    # Follow the original source if a resource retry itself failed.
    original = receipt.get("source_run_id", run_id)
    if not re.fullmatch(r"[a-z0-9-]{1,48}", original):
        raise ValueError("Bounded original source ID required")
    root = base / "sources" / original
    with zipfile.ZipFile(source) as bundle:
        manifest = json.loads(bundle.read("__forge_vm_source_manifest__.json"))
    if root.is_symlink() or verify(root, manifest["files"]):
        raise ValueError("Prior qualification sources changed")
    checks = receipt.get("inherited_checks", []) + receipt["checks"]
    inherited = []
    for index, check in enumerate(checks):
        if index >= len(steps):
            raise ValueError("Unexpected prior check inventory")
        name, command, directory = steps[index]
        if (check["name"], check["command"], check["cwd"]) != (name, command, directory):
            raise ValueError("Prior command differs from current protocol")
        if check["exit_code"]:
            break
        log_run = check.get("inherited_from", run_id)
        if not re.fullmatch(r"[a-z0-9-]{1,48}", log_run) or Path(check["log"]).name != check["log"]:
            raise ValueError("Unsafe prior check log")
        if sha(base / "results" / log_run / check["log"]) != check["sha256"]:
            raise ValueError("Prior successful check log changed")
        inherited.append({**check, "inherited_from": log_run})
    if len(inherited) == len(steps):
        raise ValueError("No failed command to resume")
    return root, original, manifest, inherited


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--run-id", required=True)
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--source-sha256", required=True)
    parser.add_argument("--resume-from", help="Unchanged failed snapshot; retain prior receipts and reuse its cache")
    parser.add_argument("--suite", choices=("full", "delivery", "shells"), default="full")
    args = parser.parse_args()
    if not re.fullmatch(r"[a-z0-9-]{1,48}", args.run_id) or not re.fullmatch(r"[0-9a-f]{64}", args.source_sha256):
        raise ValueError("Bounded run ID and SHA-256 required")
    if sys.platform != "linux" or not Path("/.dockerenv").is_file() or os.getuid() != 1000:
        raise ValueError("Unprivileged qualification Linux container required")
    if args.source.stat().st_size > 2 * 1024**3 or sha(args.source) != args.source_sha256:
        raise ValueError("Frozen source transport differs")
    base = Path("/qualification")
    for name in ("tmp", "sources", "results", "cargo-home", "cargo-target"):
        path = base / name
        if path.is_symlink():
            raise ValueError("Physical qualification cache required")
        path.mkdir(exist_ok=True)
    root, output = base / "sources" / args.run_id, base / "results" / args.run_id
    if root.exists() or output.exists() or args.resume_from == args.run_id:
        raise ValueError("Existing qualification run preserved")
    output.mkdir()
    result = {"schema": "forge.gate7.linux-container-build.v1", "started_utc": utc(),
              "run_id": args.run_id, "suite": args.suite, "source_sha256": args.source_sha256,
              "source_unchanged": False, "checks": [], "succeeded": False,
              "clean_os_qualified": False, "graphics_qualified": False, "gate_closed": False}
    manifest = None
    steps = [
        ("world-tests", ["cargo", "test", "--locked", "-p", "forge_world", "--lib"], "."),
        ("engine-tests", ["cargo", "test", "--locked", "-p", "forge_engine", "--lib"], "."),
        ("engine-clippy", ["cargo", "clippy", "--locked", "-p", "forge_world", "-p", "forge_engine", "--lib", "--", "-D", "warnings"], "."),
        ("game-clippy", ["cargo", "clippy", "--locked", "-p", "forge_tidewater", "--example", "tidewater_plant_probe", "--", "-D", "warnings"], "."),
        ("game-build", ["cargo", "build", "--locked", "--release", "-p", "forge_tidewater", "--example", "tidewater_plant_probe"], "."),
        ("walk-build", ["cargo", "build", "--locked", "--release", "-p", "forge_runtime", "--example", "cell_streaming_region"], "."),
        ("launcher-frontend", ["node", "scripts/build-aube.mjs"], "apps/forge_launcher"),
        ("launcher-clippy", ["cargo", "clippy", "--locked", "--features", "production", "--bin", "forge-launcher-prototype", "--", "-D", "warnings"], "apps/forge_launcher"),
        ("launcher-build", ["cargo", "build", "--locked", "--release", "--features", "production", "--bin", "forge-launcher-prototype"], "apps/forge_launcher"),
        ("launcher-distribution-admission", [str(base / "cargo-target/release/forge-launcher-prototype"), "--verify-distributable"], "."),
    ]
    binary_names = ("examples/tidewater_plant_probe", "examples/cell_streaming_region", "forge-launcher-prototype")
    if args.suite == "delivery":
        cargo_manifest = "tools/forge_delivery/Cargo.toml"
        steps = [
            ("delivery-tests", ["cargo", "test", "--locked", "--manifest-path", cargo_manifest], "."),
            ("delivery-clippy", ["cargo", "clippy", "--locked", "--manifest-path", cargo_manifest, "--all-targets", "--", "-D", "warnings"], "."),
            ("delivery-build", ["cargo", "build", "--locked", "--release", "--manifest-path", cargo_manifest], "."),
        ]
        binary_names = ("forge_delivery",)
    elif args.suite == "shells":
        steps = [
            ("launcher-frontend", ["node", "scripts/build-aube.mjs"], "apps/forge_launcher"),
            ("launcher-tests", ["cargo", "test", "--locked", "--features", "production"], "apps/forge_launcher"),
            ("launcher-clippy", ["cargo", "clippy", "--locked", "--features", "production", "--all-targets", "--", "-D", "warnings"], "apps/forge_launcher"),
            ("launcher-build", ["cargo", "build", "--locked", "--release", "--features", "production", "--bin", "forge-launcher-prototype"], "apps/forge_launcher"),
            ("launcher-distribution-admission", [str(base / "cargo-target/release/forge-launcher-prototype"), "--verify-distributable"], "."),
        ]
        binary_names = ("forge-launcher-prototype",)
    try:
        inherited = []
        if args.resume_from:
            root, original, manifest, inherited = resume(base, args.resume_from, args.source, args.source_sha256, steps)
            result.update(source_run_id=original, resumed_from=args.resume_from, inherited_checks=inherited)
        else:
            manifest = extract(args.source, root)
        result["source_entries"] = len(manifest["files"])
        result["source_manifest_sha256"] = retain_source_manifest(args.source, output, manifest)
        result["cargo_build_jobs"] = os.environ.get("CARGO_BUILD_JOBS")
        result["memory_limit_bytes"] = Path("/sys/fs/cgroup/memory.max").read_text().strip()
        with (output / "environment.log").open("x", encoding="utf-8") as log:
            for command in (["uname", "-a"], ["rustc", "--version", "--verbose"], ["cargo", "--version"],
                            ["node", "--version"], ["dpkg-query", "-W"]):
                subprocess.run(command, stdout=log, stderr=subprocess.STDOUT, check=True)
        for index, (name, command, directory) in enumerate(steps, 1):
            if index <= len(inherited):
                continue
            check = {"name": name, "command": command, "cwd": directory, "started_utc": utc()}
            result["checks"].append(check)
            print(json.dumps({"check_started": name, "utc": utc()}), flush=True)
            log_path = output / f"{index:02}-{name}.log"
            with log_path.open("x", encoding="utf-8") as log:
                process = subprocess.run(command, cwd=root / directory, stdout=log, stderr=subprocess.STDOUT, check=False)
            check.update(exit_code=process.returncode, finished_utc=utc(), log=log_path.name, sha256=sha(log_path))
            print(json.dumps(check), flush=True)
            if process.returncode:
                raise RuntimeError("Linux qualification failed: " + name)
        binaries = []
        for name in binary_names:
            path = base / "cargo-target/release" / name
            destination = output / path.name
            shutil.copyfile(path, destination)
            if sha(path) != sha(destination):
                raise ValueError("Binary copy differs: " + name)
            binaries.append({"name": destination.name, "sha256": sha(destination), "bytes": destination.stat().st_size})
            with (output / (path.name + ".ldd.log")).open("x", encoding="utf-8") as log:
                subprocess.run(["ldd", str(path)], stdout=log, stderr=subprocess.STDOUT, check=True)
        result["binaries"] = binaries
        result["succeeded"] = True
    except Exception as error:
        result["error"] = str(error)
    finally:
        if manifest is not None:
            result["changed_sources"] = verify(root, manifest["files"])
            result["source_unchanged"] = not result["changed_sources"]
            try:
                result["source_transport_unchanged"] = sha(args.source) == args.source_sha256
            except OSError as error:
                result["source_transport_unchanged"] = False
                result["source_transport_error"] = str(error)
            result["succeeded"] &= result["source_unchanged"] and result["source_transport_unchanged"]
        result["finished_utc"] = utc()
        with (output / "receipt.json").open("x", encoding="utf-8") as stream:
            json.dump(result, stream, indent=2)
        print(json.dumps({"result": str(output / "receipt.json"), "succeeded": result["succeeded"]}), flush=True)
    return 0 if result["succeeded"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
