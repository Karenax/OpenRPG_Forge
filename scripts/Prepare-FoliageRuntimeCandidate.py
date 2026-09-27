"""Assemble an external, unpromoted runtime fixture from verified foliage outputs."""
import argparse
import copy
import hashlib
import json
from pathlib import Path
import shutil


def sha(path):
    return hashlib.file_digest(path.open("rb"), "sha256").hexdigest()


def main():
    parser = argparse.ArgumentParser()
    for name in ("base-build", "candidate-dir", "reimport-proof", "preview", "output-dir"):
        parser.add_argument("--" + name, type=Path, required=True)
    parser.add_argument("--candidate-revision", type=int, default=1)
    args = parser.parse_args()
    if args.candidate_revision < 1:
        parser.error("candidate revision must be positive")
    base = json.loads((args.base_build / "asset-kit-build.v1.json").read_text(encoding="utf-8"))
    candidate_path = args.candidate_dir / "candidate-proof.json"
    candidate = json.loads(candidate_path.read_text(encoding="utf-8"))
    proof = json.loads(args.reimport_proof.read_text(encoding="utf-8"))
    assert proof["status"] == "PASS" and proof["candidate_report_sha256"] == sha(candidate_path)
    assert base["qualification"]["technical_export_validated"] is True
    assert all(sha(Path(path)) == expected for path, expected in candidate["source_files_unchanged"].items())
    replacements = {asset["id"]: asset for asset in candidate["assets"]}
    assert set(replacements) == {asset["id"] for asset in proof["assets"]}
    assert all(asset["material_signatures_identical_to_base"] for asset in proof["assets"])
    assert not set(replacements) & set(base.get("authored_source", {}).get("assets", []))
    preview = json.loads(args.preview.read_text(encoding="utf-8"))
    assert preview["asset_kit_id"] == base["asset_kit_id"]
    assert preview["asset_kit_revision"] == base["asset_kit_revision"]
    if "authored_source" in base:
        assert preview["authored_source_sha256"] == base["authored_source"]["descriptor_sha256"]
    output = args.output_dir.resolve()
    output.mkdir(parents=True, exist_ok=False)
    (output / "assets").mkdir()
    report = {key: copy.deepcopy(base[key]) for key in ("schema", "schema_version", "asset_kit_id", "asset_kit_revision", "generator", "provenance", "authored_source") if key in base}
    report["asset_kit_id"] += "-foliage-candidate"
    report["asset_kit_revision"] = args.candidate_revision
    report["generator"] = {"input_manifest_sha256": base["generator"]["input_manifest_sha256"], "assembly_script_sha256": sha(Path(__file__))}
    report["qualification"] = {"technical_export_validated": True, "human_art_review": "pending", "publication_approved": False, "r1_063_complete": False}
    report["candidate_evidence"] = {"base_report_sha256": sha(args.base_build / "asset-kit-build.v1.json"), "candidate_report_sha256": sha(candidate_path), "reimport_proof_sha256": sha(args.reimport_proof), "scope": "external runtime fixture, not promoted and not a full Blender kit rebuild"}
    report["assets"] = []
    for asset in base["assets"]:
        if asset["id"] in replacements:
            source = args.candidate_dir / f"{asset['id']}.glb"
            expected = replacements[asset["id"]]["glb_sha256"]
        else:
            source = args.base_build / asset["glb"]["path"]
            expected = asset["glb"]["sha256"]
        assert sha(source) == expected
        destination = output / "assets" / f"{asset['id']}.glb"
        shutil.copy2(source, destination)
        assert sha(destination) == expected
        report["assets"].append({"id": asset["id"], "kind": asset["kind"], "glb": {"path": destination.relative_to(output).as_posix(), "sha256": expected}})
    original_placements = copy.deepcopy(preview["placements"])
    preview["asset_kit_id"] = report["asset_kit_id"]
    preview["asset_kit_revision"] = report["asset_kit_revision"]
    assert preview["placements"] == original_placements
    for name, document in [("asset-kit-build.v1.json", report), ("runtime-preview.v1.json", preview)]:
        (output / name).write_text(json.dumps(document, indent=2) + "\n", encoding="utf-8")
    print(f"FORGE_FOLIAGE_RUNTIME_FIXTURE_READY assets={len(report['assets'])} placements={len(original_placements)} output={output}")


if __name__ == "__main__":
    main()
