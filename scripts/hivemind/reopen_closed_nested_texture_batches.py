"""Reopen only the two terminal texture sub-batches, without rerunning Unity."""
import hashlib
import json
from pathlib import Path
import runpy

ROOT = Path(r"F:\OpenRPG_ImportWork\unity-nested-material-texture-storage-v2")
OUT = Path(r"F:\OpenRPG_ImportWork\nested-texture-first-two-receipts-v1")
EXPECTED = {
    "hivemind.modular-cave": "9060cf1f2ec61e312f9ef354736fc92c060720a82532b05b0d1e32e63f0fc5c8",
    "hivemind.modular-dungeon-gothic": "5d2a7922d9261ac65f6eb7f6f4c41bdf87ee76be76479a8980907e1e8480bddd",
}


def read(path):
    return json.loads(Path(path).read_text("utf-8-sig"))


def sha(path):
    with Path(path).open("rb") as file:
        return hashlib.file_digest(file, "sha256").hexdigest()


def main():
    if OUT.exists():
        raise ValueError("Preserve previous closed-batch reopening")
    state = read(ROOT / "RUN-RESULT.v1.json")
    closed = {row["pack_id"]: row for row in state["completed_packs"]}
    pins = {str(Path(__file__).resolve()): sha(__file__)}
    summaries = []
    asset_ids = set()
    for pack, expected in EXPECTED.items():
        row = closed[pack]
        if row["textures"] != 6 or row["unity_exit_code"] != 0 or row["verifier_exit_code"] != 0 or row["receipt_sha256"] != expected:
            raise ValueError("Terminal texture sub-batch proof differs")
        receipt = Path(row["receipt"])
        if sha(receipt) != expected:
            raise ValueError("Changed closed receipt")
        pins[str(receipt)] = expected
        root = receipt.parent
        prepared_path = root / "PREPARED.v1.json"
        pins[str(prepared_path)] = sha(prepared_path)
        prepared = read(prepared_path)
        verifier = root / "verify_texture_export.py"
        if sha(verifier) != prepared["verifier_sha256"]:
            raise ValueError("Actual verifier changed")
        pins[str(verifier)] = prepared["verifier_sha256"]
        manifest_path = root / "manifest.json"
        pins[str(manifest_path)] = prepared["manifest_sha256"]
        pins[prepared["program_manifest"]] = prepared["program_manifest_sha256"]
        pins.update(prepared["material_pins"])
        for copy in prepared["copies"]:
            for field, key in [("original", "original_sha256"), ("original_meta", "original_meta_sha256"), ("project_copy", "project_copy_sha256")]:
                pins[copy[field]] = copy[key]
        for path, value in pins.items():
            if sha(path) != value:
                raise ValueError("Changed source/material input: " + path)
        manifest = read(manifest_path)
        directory = Path(manifest["output_root"])
        if directory.resolve().parent != root.resolve():
            raise ValueError("Export escapes isolated root")
        native_path = directory / "RESULT.json"
        original_receipt = read(receipt)
        pins[str(native_path)] = original_receipt["unity_result_sha256"]
        if sha(native_path) != pins[str(native_path)]:
            raise ValueError("Changed native capture")
        native = read(native_path)
        if native["matched"] != 6 or native["failed"] != 0 or len(native["cases"]) != 6 or len(manifest["cases"]) != 6:
            raise ValueError("Native capture coverage incomplete")
        namespace = runpy.run_path(str(verifier))
        reopened = []
        for index, (request, case) in enumerate(zip(manifest["cases"], native["cases"])):
            if request["asset_id"] in asset_ids:
                raise ValueError("Duplicate logical texture ID")
            asset_ids.add(request["asset_id"])
            reopened.append(namespace["check_case"](request, case, directory / f"{index:03}"))
        if reopened != original_receipt["rows"]:
            raise ValueError("Physical storage reopening differs from terminal receipt")
        for path in directory.rglob("*"):
            if path.is_file():
                pins[str(path)] = sha(path)
        summaries.append({"pack_id": pack, "textures_verified": 6, "mips_verified": sum(case["mip_count"] for case in native["cases"]), "receipt": str(receipt), "receipt_sha256": expected})
    for path, expected in pins.items():
        if sha(path) != expected:
            raise ValueError("Input/storage changed during reopening")
    report = {"status": "12_closed_native_texture_storage_captures_reopened", "textures_verified": len(asset_ids), "packs": summaries, "input_pins": pins, "native_capture_rerun": False, "normal_decode_qualified": False, "render_parity_qualified": False, "studio_verified": False, "full_import": False}
    OUT.mkdir()
    with (OUT / "RESULT.v1.json").open("x", encoding="utf-8") as file:
        json.dump(report, file, indent=2)
    print(json.dumps({key: value for key, value in report.items() if key != "input_pins"}))


if __name__ == "__main__":
    main()
