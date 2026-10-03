"""Add source-exact FishGeometry payloads to a new Tidewater bundle."""
import hashlib, json, os, sys
from pathlib import Path

COMMIT = "1438b1abfcaee3267092b75573014f4d9b4a983c"
def digest(data): return hashlib.sha256(data).hexdigest()
def record(path):
    data = path.read_bytes(); return {"bytes": len(data), "sha256": digest(data)}
def load(path): return json.loads(path.read_text(encoding="utf-8"))
def link_tree(source, target):
    for path in source.rglob("*"):
        out = target / path.relative_to(source)
        if path.is_dir(): out.mkdir(parents=True, exist_ok=True)
        elif path.is_file(): out.parent.mkdir(parents=True, exist_ok=True); os.link(path, out)
        else: raise ValueError(f"unexpected source member {path}")
def verify(root):
    index=load(root/"preview-scene.json"); bad=[]
    for name, entry in index["files"].items():
        path=root/name
        if not path.is_file() or record(path)!=entry: bad.append(name)
    fish=load(root/"fish-stand-fish"/"fish.json")
    if index["source_commit"]!=COMMIT or fish["source"]["commit"]!=COMMIT or len(fish["fish"])!=5: raise ValueError("fish identity differs")
    if bad: raise ValueError(f"manifest differs: {bad[:3]}")
    return {"files":len(index["files"]),"fish":len(fish["fish"]),"triangles":sum(x["geometry"]["triangles"] for x in fish["fish"]),"manifest_sha256":digest((root/"preview-scene.json").read_bytes())}
def prepare(source, fish_export, target):
    if not source.is_dir() or not fish_export.is_dir() or target.exists(): raise ValueError("existing inputs and new target required")
    if source.drive!=target.drive or fish_export.drive!=target.drive: raise ValueError("hard-link inputs must share target volume")
    source_index=(source/"preview-scene.json").read_bytes()
    fish=load(fish_export/"fish.json")
    if fish["source"]["commit"]!=COMMIT or len(fish["fish"])!=5: raise ValueError("fish export pin differs")
    link_tree(source,target); (target/"preview-scene.json").unlink(); link_tree(fish_export,target/"fish-stand-fish"); os.link(target/"fish-stand-content"/"source"/"public"/"models"/"characters"/"joe.glb",target/"scene"/"joe.glb")
    fixture_path=target/"scene"/"fixture.json"; fixture=load(source/"scene"/"fixture.json"); fixture_path.unlink()
    fixture["fish_stand_fish"]={"descriptor":"../fish-stand-fish/fish.json","status":"source-geometry-transposed-standard-material-not-source-parity"}
    fixture_bytes=(json.dumps(fixture,indent=2,ensure_ascii=False)+"\n").encode(); fixture_path.write_bytes(fixture_bytes)
    index=load(source/"preview-scene.json"); index["files"]["scene/fixture.json"]={"bytes":len(fixture_bytes),"sha256":digest(fixture_bytes)}
    for path in (target/"fish-stand-fish").rglob("*"):
        if path.is_file(): index["files"][path.relative_to(target).as_posix()]=record(path)
    index["files"]["scene/joe.glb"]=record(target/"scene"/"joe.glb")
    index_bytes=(json.dumps(index,indent=2,ensure_ascii=False)+"\n").encode(); (target/"preview-scene.json").write_bytes(index_bytes)
    if (source/"preview-scene.json").read_bytes()!=source_index: raise ValueError("input bundle was modified")
    return verify(target)
if __name__=="__main__":
    if len(sys.argv)==4: print(json.dumps(prepare(*(Path(x).resolve() for x in sys.argv[1:])),sort_keys=True))
    elif len(sys.argv)==2: print(json.dumps(verify(Path(sys.argv[1]).resolve()),sort_keys=True))
    else: raise SystemExit("Usage: prepare-fish-stand-fish-bundle.py BUNDLE006 FISH_EXPORT NEW_BUNDLE007 | BUNDLE007")
