"""Prepare isolated texture captures for the existing 16-material WGSL batch."""
import hashlib
import json
from pathlib import Path
import shutil

W = Path(r"F:\OpenRPG_ImportWork")
ROOT = W / "unity-nested-material-texture-storage-v2"
TEMPLATE = W / "unity-dungeon-texture-storage-v1"

def sha(p):
    return hashlib.sha256(Path(p).read_bytes()).hexdigest()

def read(p):
    return json.loads(Path(p).read_text("utf-8-sig"))

def main():
    if ROOT.exists():
        raise RuntimeError("Existing texture preparation preserved")
    batch_path = W / "nested-prefab-material-wgsl-v1/RESULT.v1.json"
    batch = read(batch_path)
    if batch["status"] != "source_material_wgsl_kernels_naga_validated" or batch["naga_validated_materials"] != 16:
        raise RuntimeError("Closed WGSL batch required")
    exporter = TEMPLATE / "project/Assets/Editor/TextureSourceSnapshot.cs"
    verifier = TEMPLATE / "verify_texture_export.py"
    api_path = TEMPLATE / "API-COMPILE.v1.json"
    api = read(api_path)
    if api["actual_api_compile_exit_code"] != 0 or api["helper_sha256"] != sha(exporter):
        raise RuntimeError("Exact reused Unity API compile proof absent")
    pins = {str(p): sha(p) for p in [batch_path, exporter, verifier, api_path, Path(__file__).resolve()]}
    groups = {}
    for row in batch["rows"]:
        textures = groups.setdefault(row["pack_id"], {})
        for texture in row["source_texture_bindings"]:
            guid = texture["guid"]
            if guid in textures and textures[guid] != texture:
                raise RuntimeError("Conflicting texture identity within pack")
            textures[guid] = texture
    ROOT.mkdir()
    packs = []
    for pack, textures in sorted(groups.items()):
        root = ROOT / pack.removeprefix("hivemind.")
        project = root / "project"
        for directory in [project / "Assets/Editor", project / "ProjectSettings", project / "Packages", root / "temp"]:
            directory.mkdir(parents=True)
        exporter_copy = project / "Assets/Editor/TextureSourceSnapshot.cs"
        verifier_copy = root / "verify_texture_export.py"
        shutil.copyfile(exporter, exporter_copy)
        shutil.copyfile(verifier, verifier_copy)
        settings = TEMPLATE / "project/ProjectSettings/ProjectSettings.asset"
        version = TEMPLATE / "project/ProjectSettings/ProjectVersion.txt"
        if "m_ActiveColorSpace: 1" not in settings.read_text("utf-8-sig") or "6000.5.8f1" not in version.read_text("utf-8-sig"):
            raise RuntimeError("Explicit Linear Unity 6000.5.8f1 template required")
        for source in [settings, version]:
            pins[str(source)] = sha(source)
            shutil.copyfile(source, project / "ProjectSettings" / source.name)
        (project / "Packages/manifest.json").write_text('{"dependencies":{}}', "utf-8")
        cases, copies = [], []
        for guid, texture in sorted(textures.items()):
            originals = [Path(f["path"]) for f in texture["source_files"]]
            image = next(p for p in originals if not p.name.endswith(".meta"))
            meta = Path(str(image) + ".meta")
            for item in texture["source_files"]:
                if sha(item["path"]) != item["sha256"]:
                    raise RuntimeError("Original texture source pin changed")
                pins[item["path"]] = item["sha256"]
            if f"guid: {guid}" not in meta.read_text("utf-8-sig"):
                raise RuntimeError("Texture source metadata GUID differs")
            destination = project / "Assets/SourceTextures" / guid / image.name
            destination.parent.mkdir(parents=True)
            shutil.copyfile(image, destination)
            shutil.copyfile(meta, Path(str(destination) + ".meta"))
            copies.append({"original": str(image), "original_sha256": sha(image), "original_meta": str(meta),
                           "original_meta_sha256": sha(meta), "project_copy": str(destination),
                           "project_copy_sha256": sha(destination), "project_meta_copy": str(destination) + ".meta",
                           "project_meta_copy_sha256": sha(str(destination) + ".meta")})
            cases.append({"asset_id": f"{pack}.texture-{guid}", "asset_path": destination.relative_to(project).as_posix(),
                          "expected_guid": guid, "expected_file_id": "2800000", "expected_name": image.stem})
        manifest = {"schema_version": 1, "output_root": str(root / "export"), "cases": cases}
        manifest_path = root / "manifest.json"
        manifest_path.write_text(json.dumps(manifest, indent=2), "utf-8")
        source_manifest = W / "hivemind-run" / pack.removeprefix("hivemind.") / "source.v1.json"
        prepared = {"schema": "openrpg.unity-texture-probe-prepared.v1", "pack_id": pack, "project": str(project),
                    "manifest_sha256": sha(manifest_path), "program_manifest": str(source_manifest),
                    "program_manifest_sha256": sha(source_manifest), "material_pins": {str(batch_path): pins[str(batch_path)]},
                    "copies": copies, "exporter_sha256": sha(exporter_copy), "verifier_sha256": sha(verifier_copy),
                    "initial_player_settings_sha256": sha(project / "ProjectSettings/ProjectSettings.asset"),
                    "initial_project_version_sha256": sha(project / "ProjectSettings/ProjectVersion.txt"),
                    "original_project_color_space_qualified": False, "native_capture_launched": False,
                    "scope": "Imported storage in isolated Linear project, original texture/meta preserved; original project parity unqualified",
                    "full_import": False}
        prepared_path = root / "PREPARED.v1.json"
        prepared_path.write_text(json.dumps(prepared, indent=2), "utf-8")
        pins[str(prepared_path)] = sha(prepared_path)
        packs.append({"pack_id": pack, "root": str(root), "requested_textures": len(cases)})
    for path, expected in pins.items():
        if sha(path) != expected:
            raise RuntimeError("Preparation source changed")
    result = {"status": "isolated_native_texture_capture_projects_prepared", "packs": packs,
              "requested_textures": sum(p["requested_textures"] for p in packs), "source_pins": pins,
              "reused_exporter_unchanged": True, "reused_verifier_unchanged": True,
              "native_capture_launched": False, "full_import": False}
    (ROOT / "PREPARED.v1.json").write_text(json.dumps(result, indent=2), "utf-8")
    print(json.dumps({k: v for k, v in result.items() if k != "source_pins"}))

if __name__ == "__main__":
    main()
