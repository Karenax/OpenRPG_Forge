"""Create an additive FishStand content bundle without mutating bundle 005."""
import hashlib
import json
import os
import shutil
import sys
from pathlib import Path

COMMIT = "1438b1abfcaee3267092b75573014f4d9b4a983c"

def digest(data): return hashlib.sha256(data).hexdigest()
def record(path):
    data = path.read_bytes()
    return {"bytes": len(data), "sha256": digest(data)}
def load(path): return json.loads(path.read_text(encoding="utf-8"))

def link_tree(source, target):
    for path in source.rglob("*"):
        out = target / path.relative_to(source)
        if path.is_dir(): out.mkdir(parents=True, exist_ok=True)
        elif path.is_file():
            out.parent.mkdir(parents=True, exist_ok=True)
            os.link(path, out)
        else: raise ValueError(f"Unexpected source member {path}")

def verify(root, expected=None):
    index_path = root / "preview-scene.json"
    index = load(index_path)
    if expected and digest(index_path.read_bytes()) != expected: raise ValueError("preview manifest pin mismatch")
    if index["source_commit"] != COMMIT: raise ValueError("source commit differs")
    for name, entry in index["files"].items():
        if record(root / name) != entry: raise ValueError(f"payload differs: {name}")
    content = load(root / "fish-stand-content" / "content.json")
    if content["source"]["commit"] != COMMIT or len(content["display"]["fish_on_ice"]) != 5:
        raise ValueError("FishStand content identity differs")
    if content["vendor"]["character"]["license"] != "MIT": raise ValueError("Joe licence differs")
    return {"files": len(index["files"]), "joe_bytes": content["source_files"]["source/public/models/characters/joe.glb"]["bytes"], "fish_on_ice": 5, "manifest_sha256": digest(index_path.read_bytes())}

def prepare(source, content_export, target):
    if not source.is_dir() or not content_export.is_dir() or target.exists(): raise ValueError("existing inputs and a new target required")
    if source.drive != target.drive or content_export.drive != target.drive: raise ValueError("hard-link inputs must share target volume")
    source_index = (source / "preview-scene.json").read_bytes()
    content = load(content_export / "content.json")
    if content["source"]["commit"] != COMMIT: raise ValueError("content export pin differs")
    link_tree(source, target)
    (target / "preview-scene.json").unlink()
    content_target = target / "fish-stand-content"
    link_tree(content_export, content_target)
    fixture_path = target / "scene" / "fixture.json"
    # `link_tree` uses hard links for immutable inherited bytes.  Read from
    # the source and unlink the target before replacing its manifest entry.
    fixture = load(source / "scene" / "fixture.json")
    fixture_path.unlink()
    fixture["fish_stand_content"] = {"descriptor": "../fish-stand-content/content.json", "status": "source-acquired-not-transposed"}
    fixture_bytes = (json.dumps(fixture, indent=2, ensure_ascii=False) + "\n").encode()
    fixture_path.write_bytes(fixture_bytes)
    index = load(source / "preview-scene.json")
    index["files"]["scene/fixture.json"] = {"bytes": len(fixture_bytes), "sha256": digest(fixture_bytes)}
    for path in content_target.rglob("*"):
        if path.is_file(): index["files"][path.relative_to(target).as_posix()] = record(path)
    index_bytes = (json.dumps(index, indent=2, ensure_ascii=False) + "\n").encode()
    (target / "preview-scene.json").write_bytes(index_bytes)
    if (source / "preview-scene.json").read_bytes() != source_index: raise ValueError("bundle 005 was modified")
    return verify(target, digest(index_bytes))

if __name__ == "__main__":
    if len(sys.argv) == 4: print(json.dumps(prepare(*(Path(value).resolve() for value in sys.argv[1:])), sort_keys=True))
    elif len(sys.argv) == 2: print(json.dumps(verify(Path(sys.argv[1]).resolve()), sort_keys=True))
    else: raise SystemExit("Usage: prepare-fish-stand-content-bundle.py BUNDLE005 CONTENT_EXPORT NEW_BUNDLE006 | BUNDLE006")
