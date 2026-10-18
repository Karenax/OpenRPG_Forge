"""Reproducible static inventory; occurrence counts are not genericity proofs."""
import argparse
import json
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[1]


def inventory():
    tracked = subprocess.check_output(
        ["git", "ls-files", "--cached", "--others", "--exclude-standard", "-z",
         "crates/forge_engine", "apps", "crates/forge_project", "tools"],
        cwd=ROOT,
    ).decode("utf-8").split("\0")
    # Include newly extracted product sources before their first commit.
    sources = {p for p in (ROOT / "crates/forge_engine/src").rglob("*.rs")}
    sources.update(ROOT / name for name in tracked if name.endswith(".rs") and (ROOT / name).is_file())
    modules = []
    links = []
    for path in sorted(sources):
        text = path.read_text(encoding="utf-8")
        rel = path.relative_to(ROOT).as_posix()
        if rel.startswith("crates/forge_engine/src/"):
            hits = len(re.findall("tidewater", text, re.IGNORECASE))
            if hits:
                modules.append({"path": rel, "occurrences_including_provenance": hits})
        for target in re.findall(r'(?:include(?:_str|_bytes)?!\s*\(\s*|#\[path\s*=\s*)"([^"]+)"', text):
            if "hivemind" in target.replace("\\", "/").split("/"):
                links.append({"path": rel, "target": target})
    examples = list((ROOT / "crates/forge_tidewater/examples").rglob("*.rs"))
    return {
        "schema": "forge.engine-architecture-inventory", "version": 1,
        "head": subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=ROOT, text=True).strip(),
        "scope": "Current readable Rust product sources; src counts include provenance comments. No GPU or runtime qualification.",
        "engine_named_map_occurrences": sum(row["occurrences_including_provenance"] for row in modules),
        "engine_named_map_files": modules,
        "product_rust_includes_from_orchestration": links,
        "example_rust_files": len(examples),
        "example_rust_lines": sum(len(p.read_bytes().splitlines()) for p in examples),
        "genericity_qualified": False,
    }


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    result = json.dumps(inventory(), indent=2, ensure_ascii=False) + "\n"
    if args.output:
        args.output.write_text(result, encoding="utf-8")
    else:
        print(result, end="")
