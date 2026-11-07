"""Exercise the installer backend on the SDK-free guest with a fixture or a pinned Studio.

The fixture uses the delivery CLI as its application, not a substitute Studio/game.
No product release, UI, aggregate-license or full-installer qualification is claimed.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import socket
import subprocess
import datetime
import re


def sha(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def exercise_studio(cli, output, installed, user, sentinel, sentinel_pin, package, package_pin):
    inspection = cli("inspect-studio", "inspect", package)
    descriptor = inspection["descriptor"]
    product = descriptor.get("product") or {}
    if (descriptor.get("platform") != "linux-x64"
            or descriptor.get("kind") != "applications"
            or descriptor.get("rights") != "private_local_only"
            or product.get("schema") != "forge.delivery.product.v3"
            or product.get("id") != "openrpg-forge"
            or set(product.get("apps", {})) != {"editor"}
            or product["apps"]["editor"]["executable"] != "bin/forge-studio"):
        raise ValueError("Pinned private Linux Studio product required")
    revision = inspection["manifest_sha256"]
    if not re.fullmatch(r"[0-9a-f]{64}", revision):
        raise ValueError("Invalid Studio manifest hash")
    editor_pin, = (item["sha256"] for item in descriptor["files"]
                   if item["path"] == "bin/forge-studio")

    plan = output / "studio-plan.json"
    cli("plan-studio", "plan-file", installed, plan, package)
    state = cli("apply-studio", "apply", plan)
    verified = cli("verify-studio", "verify", installed)
    core = {"id": descriptor["id"], "revision": revision, "kind": descriptor["kind"]}
    if (state != verified or verified["revision"] != 1
            or Path(verified["user_root"]) != user
            or verified["active"]["core"] != core):
        raise RuntimeError("Studio activation differs from the pinned plan")
    editor = installed / "packages" / descriptor["id"] / revision / "bin/forge-studio"
    if editor.resolve() != editor or sha(editor) != editor_pin or not os.access(editor, os.X_OK):
        raise RuntimeError("Installed Studio executable differs or cannot be run")

    cli("uninstall-studio", "uninstall-studio", installed)
    if (installed / "installation.json").exists() or editor.exists() or sha(sentinel) != sentinel_pin:
        raise RuntimeError("Studio removal or user-data preservation failed")
    if sha(package) != package_pin:
        raise RuntimeError("Studio input package changed")
    return {"succeeded": True, "studio_manifest_sha256": revision,
            "installed_editor_sha256": editor_pin, "studio_package_sha256": package_pin,
            "user_data_preserved": True, "packages_unchanged": True,
            "studio_install_uninstall_verified": True}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--binary", type=Path, required=True)
    parser.add_argument("--sha256", required=True)
    parser.add_argument("--run-id", required=True)
    parser.add_argument("--studio-package", type=Path, help="Pinned private Linux Studio forgepack")
    parser.add_argument("--studio-package-sha256")
    args = parser.parse_args()
    studio = args.studio_package is not None
    if studio != (args.studio_package_sha256 is not None):
        parser.error("--studio-package and --studio-package-sha256 are required together")
    if socket.gethostname() != "forge-g7-linux-clean" or os.getuid() != 1000:
        raise ValueError("Dedicated unprivileged clean guest required")
    if not args.binary.is_absolute() or args.binary.is_symlink() or sha(args.binary) != args.sha256:
        raise ValueError("Pinned installer backend required")
    tools = ("rustc", "cargo", "gcc", "clang", "node")
    if any(shutil.which(tool) for tool in tools):
        raise ValueError("No development SDK permitted")
    role = "studio" if studio else "delivery"
    if not re.fullmatch(rf"linux-clean-{role}-[0-9]{{3}}", args.run_id):
        raise ValueError(f"Dedicated fresh {role} run ID required")
    if studio and (not args.studio_package.is_absolute()
                   or args.studio_package.resolve() != args.studio_package
                   or sha(args.studio_package) != args.studio_package_sha256):
        raise ValueError("Pinned physical Studio package required")
    output = Path.home() / "qualification" / args.run_id
    output.mkdir()
    installed, user = output / "installation", output / "userdata"
    user.mkdir()
    sentinel = user / "user-owned-project.txt"
    sentinel.write_bytes(b"User data preserved through update, rollback and uninstall\n")
    sentinel_pin = sha(sentinel)
    env = {**os.environ, "FORGE_DELIVERY_USER_ROOT": str(user)}
    delivery = args.binary
    if studio:
        delivery = output / "forge-delivery"
        shutil.copyfile(args.binary, delivery)
        if sha(delivery) != args.sha256:
            raise ValueError("Staged delivery binary changed")
    delivery.chmod(0o755)
    result = {"schema": ("forge.gate7.clean-linux-studio-install.v1" if studio
                         else "forge.gate7.clean-linux-delivery-fixture.v1"),
              "started_utc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
              "binary_sha256": args.sha256, "checks": [], "succeeded": False,
              "fixture_only": not studio, "product_installer_qualified": False, "gate_closed": False}

    def cli(name, *arguments, expected=0, binary=None):
        command = [str(binary or delivery), *map(str, arguments)]
        ran = subprocess.run(command, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, env=env, timeout=90, check=False)
        log = output / (name + ".log")
        with log.open("xb") as stream:
            stream.write(ran.stdout)
        result["checks"].append({"name": name, "command": command, "exit_code": ran.returncode,
                                 "log_sha256": sha(log)})
        if ran.returncode != expected:
            raise RuntimeError(name + " returned " + str(ran.returncode))
        return json.loads(ran.stdout) if not expected else None

    def exercise_fixture():
        packages, source_pins = [], {}
        for version in ("1", "2"):
            source = output / ("source-" + version)
            (source / "bin").mkdir(parents=True)
            shutil.copyfile(args.binary, source / "bin/forge-delivery")
            (source / "version.txt").write_text(version)
            (source / "LICENSE").write_text("Private local qualification fixture; not a product distribution.\nBinary provenance: linux-delivery-001; SHA-256 " + args.sha256 + "\n")
            launch = {"executable": "bin/forge-delivery", "args": ["status", "{install}"], "env": {}, "clear_env_prefixes": []}
            descriptor = {"schema": "forge.delivery.package.v1", "id": "applications", "label": "Linux installer fixture",
                          "version": version, "kind": "applications", "platform": "linux-x64", "source_revision": "linux-delivery-001",
                          "rights": "private_local_only", "license_paths": ["LICENSE"], "files": [],
                          "product": {"schema": "forge.delivery.product.v1", "id": "gate7-linux-installer-fixture", "title": "Qualification interne Linux",
                                      "apps": {"game": launch, "editor": launch}, "required_packs": [], "optional_packs": [], "authoring_seed": None}}
            template = output / ("template-" + version + ".json")
            template.write_text(json.dumps(descriptor))
            package = output / ("fixture-" + version + ".forgepack")
            cli("pack-" + version, "pack", source, template, package)
            packages.append(package)
            for relative in ("bin/forge-delivery", "version.txt", "LICENSE"):
                path = source / relative
                source_pins[path] = sha(path)
        package_pins = {path: sha(path) for path in packages}
        for index, package in enumerate(packages, 1):
            plan = output / ("plan-" + str(index) + ".json")
            cli("plan-" + str(index), "plan-file", installed, plan, package)
            state = cli("apply-" + str(index), "apply", plan)
            assert state["revision"] == index
            cli("verify-" + str(index), "verify", installed)
        rollback = cli("rollback-plan", "rollback-plan", installed)
        rollback_path = output / "rollback-plan.json"
        rollback_path.write_text(json.dumps(rollback))
        state = cli("rollback-apply", "apply", rollback_path)
        assert state["revision"] == 3
        core = state["active"]["core"]
        # Let the backend define the installed executable location via launch-plan.
        command = cli("launch-plan", "launch-plan", installed, "game")
        result["launch_plan"] = command
        active = Path(command["executable"])
        assert active.is_relative_to(installed) and core["revision"] in active.parts
        native = cli("installed-native-status", "status", installed, binary=active)
        assert native["revision"] == 3
        assert sha(active) == args.sha256
        cli("launch-native", "launch", installed, "game")
        corrupt = output / "corrupt.forgepack"
        data = bytearray(packages[0].read_bytes())
        data[-1] ^= 1
        corrupt.write_bytes(data)
        rejected = output / "must-not-activate"
        corrupt_plan = output / "corrupt-plan.json"
        # Preview pins the archive and descriptor; payloads are verified during
        # extraction. A failed Apply may retain its staging, never an active head.
        cli("corrupt-package-plan", "plan-file", rejected, corrupt_plan, corrupt)
        cli("corrupt-package-apply-refused", "apply", corrupt_plan, expected=1)
        assert not (rejected / "installation.json").exists()
        unchanged = cli("verify-after-corrupt-refusal", "verify", installed)
        assert unchanged["revision"] == 3
        foreign = installed / "foreign-user-file.txt"
        foreign.write_bytes(b"Do not remove\n")
        cli("uninstall", "uninstall", installed)
        assert not (installed / "installation.json").exists()
        assert foreign.read_bytes() == b"Do not remove\n"
        assert sha(sentinel) == sentinel_pin
        assert all(sha(path) == pin for path, pin in source_pins.items())
        assert all(sha(path) == pin for path, pin in package_pins.items())
        result.update(succeeded=True, user_data_preserved=True, foreign_file_preserved=True,
                      corrupt_package_refused_before_activation=True,
                      sources_unchanged=True, packages_unchanged=True,
                      package_sha256={path.name: pin for path, pin in package_pins.items()})
    try:
        if studio:
            result.update(exercise_studio(cli, output, installed, user, sentinel, sentinel_pin,
                                          args.studio_package, args.studio_package_sha256))
        else:
            exercise_fixture()
    except Exception as error:
        result["error"] = str(error)
    finally:
        result["tools_after"] = {tool: shutil.which(tool) for tool in tools}
        result["succeeded"] &= not any(result["tools_after"].values()) and sha(args.binary) == args.sha256
        result["finished_utc"] = datetime.datetime.now(datetime.timezone.utc).isoformat()
        with (output / "receipt.json").open("x") as stream:
            json.dump(result, stream, indent=2)
        print(json.dumps(result), flush=True)
    return 0 if result["succeeded"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
