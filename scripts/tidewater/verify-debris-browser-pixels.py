"""Verify repeat acquisition, every decoded payload and its retained JPEG provenance.
Usage: SOURCE RETAINED FIRST SECOND NEW_RECEIPT
"""
import hashlib
import json
from pathlib import Path
import subprocess
import sys


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def main():
    source, retained_root, first, second, receipt = [Path(v).resolve() for v in sys.argv[1:]]
    assert not receipt.exists()
    git = lambda *args: subprocess.check_output(["git", "-c", f"safe.directory={source.as_posix()}", "-C", str(source), *args], text=True).strip()
    pin = "1438b1abfcaee3267092b75573014f4d9b4a983c"
    assert git("rev-parse", "HEAD") == pin and not git("status", "--porcelain", "--untracked-files=all")
    source_raw = (source / "src/world/debris/ScannedDebris.js").read_bytes()
    code = source_raw.decode().replace("\r\n", "\n")
    start = code.index("async function loadPixels( url ) {")
    end = code.index("\nexport class ScannedDebris", start)
    function = code[start:end].rstrip().encode()
    retained_raw = (retained_root / "scanned-source.json").read_bytes()
    retained = json.loads(retained_raw)
    expected = {ref["file"]: (asset["ordinal"], name, ref)
                for asset in retained["assets"] for name, ref in asset["images"].items()}
    assert len(expected) == 12
    assert retained["source_contract_sha256"] == sha(source_raw)
    first_files = {p.name for p in first.iterdir() if p.is_file()}
    second_files = {p.name for p in second.iterdir() if p.is_file()}
    assert first_files == second_files == {"browser-pixels.json", "source-load-pixels.mjs"} | {name + ".rgba" for name in expected}
    for name in first_files:
        assert (first / name).read_bytes() == (second / name).read_bytes(), name
    doc_raw = (first / "browser-pixels.json").read_bytes()
    doc = json.loads(doc_raw)
    assert doc["schema"] == "forge.tidewater.debris-browser-pixels" and doc["version"] == 1
    assert doc["source_commit"] == pin and doc["source_contract_sha256"] == sha(source_raw)
    assert doc["source_function_sha256"] == sha(function)
    assert doc["retained_descriptor_sha256"] == sha(retained_raw)
    assert (first / "source-load-pixels.mjs").read_bytes() == function + b"\nexport { loadPixels };\n"
    assert doc["browser"]["decoder_hook"] is False and doc["browser"]["user_agent"]
    assert set(doc["images"]) == set(expected)
    for name, (asset, map_name, jpeg_ref) in expected.items():
        jpeg = (retained_root / name).read_bytes()
        assert len(jpeg) == jpeg_ref["bytes"] and sha(jpeg) == jpeg_ref["sha256"]
        ref = doc["images"][name]
        assert ref["file"] == name + ".rgba" and (ref["width"], ref["height"], ref["bytes"]) == (1024, 1024, 4194304)
        assert ref["asset"] == asset and ref["map"] == map_name
        assert ref["source_sha256"] == jpeg_ref["sha256"] and ref["sampling"] == jpeg_ref["source_sampling"]
        assert ref["row_order"] == "top-first"
        raw = (first / ref["file"]).read_bytes()
        assert len(raw) == ref["bytes"] and sha(raw) == ref["sha256"]
        assert set(raw[3::4]) == {255}
        assert len(set(raw[0::4])) > 16  # Refuse accidental blank decoder output.
    result = {"status": "PASS", "images": 12, "rgba_bytes": 50331648,
              "descriptor_sha256": sha(doc_raw), "browser": doc["browser"],
              "source_function_sha256": sha(function), "verification_sha256": sha(Path(__file__).read_bytes()),
              "limits": "Browser source decode and repeatability only; atlas/mips/material draw and complete visual parity remain open."}
    receipt.write_text(json.dumps(result, indent=2), encoding="utf-8")
    print(json.dumps(result))


if __name__ == "__main__":
    main()
