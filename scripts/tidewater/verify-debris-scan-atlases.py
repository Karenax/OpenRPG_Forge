"""Verify all retained atlas/mip bytes and independently reconstruct source level-zero tiles.
Usage: BROWSER_PIXELS RETAINED_SCANS FIRST SECOND NEW_RECEIPT
"""
import hashlib
import json
from pathlib import Path
import sys


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def main():
    pixels, retained, first, second, receipt = [Path(v).resolve() for v in sys.argv[1:]]
    assert not receipt.exists()
    pixel_raw = (pixels / "browser-pixels.json").read_bytes()
    browser = json.loads(pixel_raw)
    retained_raw = (retained / "scanned-source.json").read_bytes()
    source = json.loads(retained_raw)
    files = {p.name for p in first.iterdir() if p.is_file()}
    assert files == {p.name for p in second.iterdir() if p.is_file()}
    assert files == {"scanned-atlases.json", "LICENSE", "CREDITS.md"} | {f"scan-{name}.mips.rgba8.bin" for name in ("albedo", "normal", "arm")}
    for name in files:
        assert (first / name).read_bytes() == (second / name).read_bytes(), name
    doc_raw = (first / "scanned-atlases.json").read_bytes()
    doc = json.loads(doc_raw)
    assert doc["schema"] == "forge.tidewater.debris-scanned-atlases" and doc["version"] == 1
    assert doc["source_commit"] == browser["source_commit"] == source["source_commit"] == "1438b1abfcaee3267092b75573014f4d9b4a983c"
    assert doc["browser_pixels_sha256"] == sha(pixel_raw) and doc["retained_descriptor_sha256"] == sha(retained_raw)
    assert set(doc["textures"]) == {"albedo", "normal", "arm"}
    total = 0
    for map_name, ref in doc["textures"].items():
        assert ref["file"] == f"scan-{map_name}.mips.rgba8.bin"
        assert (ref["width"], ref["height"], ref["mips"]) == (2048, 2048, 12)
        assert ref["format"] == ("rgba8unorm-srgb" if map_name == "albedo" else "rgba8unorm")
        assert ref["sampler"] == {"address": "clamp-to-edge", "mag": "linear", "min": "linear", "mipmap": "linear", "anisotropy": 8}
        raw = (first / ref["file"]).read_bytes()
        assert len(raw) == ref["bytes"] == 22369620 and sha(raw) == ref["sha256"]
        assert doc["files"][ref["file"]] == {"bytes": len(raw), "sha256": sha(raw)}
        offset = 0
        for level in range(12):
            size = (2048 >> level) ** 2 * 4
            mip = raw[offset:offset + size]
            assert sha(mip) == ref["level_sha256"][level] and set(mip[3::4]) == {255}
            offset += size
        assert offset == len(raw)
        for asset in source["assets"]:
            image_ref = browser["images"][asset["images"][map_name]["file"]]
            tile = (pixels / image_ref["file"]).read_bytes()
            assert len(tile) == image_ref["bytes"] and sha(tile) == image_ref["sha256"]
            x, y = asset["ordinal"] % 2 * 1024, asset["ordinal"] // 2 * 1024
            for row in range(1024):
                dest = ((y + row) * 2048 + x) * 4
                assert raw[dest:dest + 4096] == tile[row * 4096:(row + 1) * 4096]
        total += len(raw)
    for name in ("LICENSE", "CREDITS.md"):
        raw = (first / name).read_bytes()
        assert raw == (retained / name).read_bytes()
        assert doc["files"][name] == {"bytes": len(raw), "sha256": sha(raw)}
    result = {"status": "PASS", "maps": 3, "mips": 36, "texture_bytes": total,
              "descriptor_sha256": sha(doc_raw), "verified_browser_pixels_sha256": sha(pixel_raw),
              "verification_sha256": sha(Path(__file__).read_bytes()),
              "limits": "Exact acquired browser tile placement, retained GPU mip bytes and repeatability only; Rust shader draws and visual parity remain open."}
    receipt.write_text(json.dumps(result, indent=2), encoding="utf-8")
    print(json.dumps(result))


if __name__ == "__main__":
    main()
