"""Optimized safe-call regression, in separate processes without core dumps."""
import argparse
import json
import os
from pathlib import Path
import resource
import shutil
import subprocess
import sys

from run_qualification import extract, sha, utc, verify


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--source-sha256", required=True)
    args = parser.parse_args()
    if sys.platform != "linux" or not Path("/.dockerenv").is_file() or os.getuid() != 1000:
        raise ValueError("Unprivileged Linux qualification container required")
    resource.setrlimit(resource.RLIMIT_CORE, (0, 0))
    source = Path("/incoming/source.zip")
    if sha(source) != args.source_sha256:
        raise ValueError("Frozen probe sources differ")
    base = Path("/qualification")
    root = base / "sources/glib-probe-001"
    output = base / "results/glib-probe-001"
    output.mkdir()
    result = {"schema": "forge.gate7.glib-iterator-regression.v1", "started_utc": utc(),
              "source_sha256": args.source_sha256, "succeeded": False, "runs": [],
              "core_dump_limit_bytes": 0, "gate_closed": False}
    manifest = None
    try:
        manifest = extract(source, root)
        cargo_manifest = root / "tools/security/glib_variant_str_iter/Cargo.toml"
        for feature in ("baseline", "patched"):
            command = ["cargo", "build", "--release", "--locked", "--manifest-path", str(cargo_manifest),
                       "--no-default-features", "--features", feature]
            log = output / (feature + "-build.log")
            with log.open("x") as stream:
                built = subprocess.run(command, stdout=stream, stderr=subprocess.STDOUT, check=False)
            result[feature + "_build"] = {"command": command, "exit_code": built.returncode, "log_sha256": sha(log)}
            if built.returncode:
                raise RuntimeError(feature + " probe build failed")
            binary = output / (feature + "-probe")
            shutil.copyfile(base / "cargo-target/release/glib-variant-str-iter-probe", binary)
            binary.chmod(0o700)
            result[feature + "_binary_sha256"] = sha(binary)
            for method in ("next", "last", "nth", "next_back", "nth_back"):
                log = output / (feature + "-" + method + ".log")
                with log.open("x") as stream:
                    ran = subprocess.run([str(binary), method], stdout=stream, stderr=subprocess.STDOUT, check=False, timeout=15)
                check = {"feature": feature, "method": method, "exit_code": ran.returncode, "log_sha256": sha(log)}
                result["runs"].append(check)
                print(json.dumps(check), flush=True)
        baseline = [check for check in result["runs"] if check["feature"] == "baseline"]
        patched = [check for check in result["runs"] if check["feature"] == "patched"]
        result["baseline_sigsegv_methods"] = [check["method"] for check in baseline if check["exit_code"] == -11]
        result["baseline_fault_reproduced"] = len(result["baseline_sigsegv_methods"]) == 5
        result["succeeded"] = len(patched) == 5 and all(check["exit_code"] == 0 for check in patched)
    except Exception as error:
        result["error"] = str(error)
    finally:
        if manifest is not None:
            result["changed_sources"] = verify(root, manifest["files"])
            result["succeeded"] &= not result["changed_sources"]
        result["finished_utc"] = utc()
        with (output / "receipt.json").open("x") as stream:
            json.dump(result, stream, indent=2)
        print(json.dumps({"succeeded": result["succeeded"], "result": str(output / "receipt.json")}), flush=True)
    return 0 if result["succeeded"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
