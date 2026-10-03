#!/usr/bin/env python3
"""Create an additive Tidewater bundle with Joe's embedded JPEGs converted to PNG.

This is deliberately a binary GLB repacker.  It does not import/export through
Blender, so node, skin, mesh, accessor, animation, and scene JSON stay intact.
It needs an already-installed ffmpeg and an already-cached/installed Khronos
``gltf-validator`` module; it never downloads or installs either one.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import struct
import subprocess
import sys
import tempfile
from typing import Any


GLB_MAGIC = b"glTF"
JSON_CHUNK = b"JSON"
BIN_CHUNK = b"BIN\x00"
EXPECTED_SOURCE_SHA256 = "da3efce5a452d3c17f40b8d33bf449f565c0a6bef93602dc3e74364fb64081ca"


def sha256_bytes(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest()


def sha256_file(path: Path) -> str:
    return sha256_bytes(path.read_bytes())


def canonical_json(value: Any) -> bytes:
    return json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=True).encode("ascii")


def pad4(value: bytes, fill: bytes) -> bytes:
    return value + fill * ((-len(value)) % 4)


def read_glb(path: Path) -> tuple[dict[str, Any], bytes]:
    data = path.read_bytes()
    if len(data) < 20:
        raise ValueError("GLB is shorter than its header")
    magic, version, total_length = struct.unpack_from("<4sII", data, 0)
    if magic != GLB_MAGIC or version != 2 or total_length != len(data):
        raise ValueError("input is not a complete glTF 2.0 binary")
    json_length, json_kind = struct.unpack_from("<I4s", data, 12)
    json_end = 20 + json_length
    if json_kind != JSON_CHUNK or json_end + 8 > len(data):
        raise ValueError("GLB JSON chunk is invalid")
    bin_length, bin_kind = struct.unpack_from("<I4s", data, json_end)
    bin_start = json_end + 8
    if bin_kind != BIN_CHUNK or bin_start + bin_length != len(data):
        raise ValueError("GLB BIN chunk is invalid or not final")
    return json.loads(data[20:json_end].decode("utf-8")), data[bin_start:]


def write_glb(path: Path, document: dict[str, Any], binary: bytes) -> None:
    json_chunk = pad4(canonical_json(document), b" ")
    bin_chunk = pad4(binary, b"\x00")
    body = struct.pack("<I4s", len(json_chunk), JSON_CHUNK) + json_chunk
    body += struct.pack("<I4s", len(bin_chunk), BIN_CHUNK) + bin_chunk
    path.write_bytes(struct.pack("<4sII", GLB_MAGIC, 2, 12 + len(body)) + body)


def run(command: list[str], *, text: bool = False) -> subprocess.CompletedProcess[Any]:
    return subprocess.run(command, check=True, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=text)


def ffmpeg_rgba(ffmpeg: str, ffprobe: str, image: Path) -> tuple[int, int, bytes]:
    probe = run([ffprobe, "-v", "error", "-select_streams", "v:0", "-show_entries", "stream=width,height", "-of", "json", str(image)], text=True)
    streams = json.loads(probe.stdout).get("streams", [])
    if len(streams) != 1:
        raise ValueError(f"could not identify one image stream in {image}")
    width, height = int(streams[0]["width"]), int(streams[0]["height"])
    decoded = run([ffmpeg, "-v", "error", "-i", str(image), "-frames:v", "1", "-f", "rawvideo", "-pix_fmt", "rgba", "pipe:1"]).stdout
    if len(decoded) != width * height * 4:
        raise ValueError(f"decoded RGBA byte count is invalid for {image}")
    return width, height, decoded


def rgba_digest(width: int, height: int, pixels: bytes) -> str:
    return sha256_bytes(struct.pack("<II", width, height) + pixels)


def find_validator_module(explicit: str | None) -> Path:
    if explicit:
        result = Path(explicit)
        if (result / "index.js").is_file():
            return result
        raise FileNotFoundError(f"Khronos gltf-validator module is not present: {result}")
    candidate_roots = [Path(os.environ.get("LOCALAPPDATA", "")) / "npm-cache" / "_npx", Path.home() / ".npm" / "_npx"]
    matches: list[Path] = []
    for root in candidate_roots:
        if root.is_dir():
            matches.extend(root.glob("*/node_modules/gltf-validator"))
    matches = [item for item in matches if (item / "index.js").is_file()]
    if not matches:
        raise FileNotFoundError("Khronos gltf-validator is not installed/cached; provide --validator-module without installing it")
    return sorted(matches, key=lambda item: str(item).lower())[0]


def validate_with_khronos(node: str, module: Path, glb: Path, report: Path) -> dict[str, Any]:
    program = """const fs=require('fs');const validator=require(process.argv[1]);const input=fs.readFileSync(process.argv[2]);validator.validateBytes(new Uint8Array(input),{uri:process.argv[2],format:'glb',maxIssues:0,writeTimestamp:false}).then(r=>{fs.writeFileSync(process.argv[3],JSON.stringify(r,null,2)+'\\n');if(r.issues.numErrors)process.exitCode=1;}).catch(e=>{console.error(e);process.exitCode=2;});"""
    result = subprocess.run([node, "-e", program, str(module), str(glb), str(report)], stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)
    if result.returncode > 1 or not report.is_file():
        raise RuntimeError(f"Khronos glTF Validator could not produce a report: {result.stderr.strip()}")
    report_value = json.loads(report.read_text(encoding="utf-8"))
    if report_value["issues"]["numErrors"]:
        raise ValueError(f"Khronos glTF Validator found {report_value['issues']['numErrors']} error(s)")
    return report_value


def semantic_hashes(document: dict[str, Any]) -> dict[str, str]:
    keys = ("scene", "scenes", "nodes", "meshes", "skins", "animations", "accessors", "materials", "textures", "samplers", "cameras")
    return {key: sha256_bytes(canonical_json(document.get(key))) for key in keys if key in document}


def convert(source: Path, destination: Path, ffmpeg: str, ffprobe: str) -> tuple[dict[str, Any], dict[str, str]]:
    document, binary = read_glb(source)
    before_semantics = semantic_hashes(document)
    images = document.get("images", [])
    views = document.get("bufferViews", [])
    jpeg_entries: list[tuple[int, int, int, int, bytes, dict[str, Any]]] = []
    # Keep decoded images beside the E: staging output, not in the system temp.
    with tempfile.TemporaryDirectory(prefix="joe-jpeg-png-", dir=destination.parent) as temporary:
        temp = Path(temporary)
        for image_index, image in enumerate(images):
            if image.get("mimeType") != "image/jpeg":
                continue
            if "bufferView" not in image or "uri" in image:
                raise ValueError(f"image {image_index} is not an embedded JPEG bufferView")
            view_index = int(image["bufferView"])
            view = views[view_index]
            if int(view.get("buffer", 0)) != 0:
                raise ValueError(f"image {image_index} uses a non-primary buffer")
            start = int(view.get("byteOffset", 0))
            end = start + int(view["byteLength"])
            encoded = binary[start:end]
            if not encoded.startswith(b"\xff\xd8\xff"):
                raise ValueError(f"image {image_index} claims JPEG but does not have JPEG bytes")
            source_jpeg = temp / f"image-{image_index}.jpg"
            converted_png = temp / f"image-{image_index}.png"
            source_jpeg.write_bytes(encoded)
            width, height, before_pixels = ffmpeg_rgba(ffmpeg, ffprobe, source_jpeg)
            run([ffmpeg, "-v", "error", "-y", "-i", str(source_jpeg), "-frames:v", "1", "-c:v", "png", "-pix_fmt", "rgba", "-compression_level", "100", str(converted_png)])
            png_width, png_height, after_pixels = ffmpeg_rgba(ffmpeg, ffprobe, converted_png)
            before_digest = rgba_digest(width, height, before_pixels)
            after_digest = rgba_digest(png_width, png_height, after_pixels)
            if (width, height, before_digest) != (png_width, png_height, after_digest):
                raise ValueError(f"decoded pixels changed for image {image_index}")
            png = converted_png.read_bytes()
            if not png.startswith(b"\x89PNG\r\n\x1a\n"):
                raise ValueError(f"ffmpeg did not write PNG for image {image_index}")
            jpeg_entries.append((image_index, view_index, start, end, png, {
                "image_index": image_index,
                "name": image.get("name"),
                "dimensions": [width, height],
                "source_mime_type": "image/jpeg",
                "derived_mime_type": "image/png",
                "source_encoded_sha256": sha256_bytes(encoded),
                "derived_encoded_sha256": sha256_bytes(png),
                "decoded_rgba_sha256": before_digest,
                "pixel_identity_verified": True,
            }))

    if not jpeg_entries:
        raise ValueError("input contains no embedded JPEG images")
    jpeg_entries.sort(key=lambda item: item[2])
    last_end = 0
    for _, _, start, end, _, _ in jpeg_entries:
        if start < last_end:
            raise ValueError("JPEG image bufferViews overlap")
        last_end = (end + 3) & ~3

    # Rebuild the single buffer while removing every JPEG payload.  All original
    # non-image bytes (including gaps) are copied verbatim; offsets are shifted
    # only by prior replacement sizes and 4-byte padding.
    rebuilt = bytearray()
    cursor = 0
    deltas: list[tuple[int, int]] = []
    for image_index, view_index, start, end, png, record in jpeg_entries:
        rebuilt.extend(binary[cursor:start])
        replacement_start = len(rebuilt)
        rebuilt.extend(png)
        rebuilt.extend(b"\x00" * ((-len(rebuilt)) % 4))
        padded_length = len(rebuilt) - replacement_start
        views[view_index]["byteOffset"] = replacement_start
        views[view_index]["byteLength"] = len(png)
        images[image_index]["mimeType"] = "image/png"
        record["derived_buffer_view"] = view_index
        record["derived_byte_offset"] = replacement_start
        record["derived_byte_length"] = len(png)
        original_padded_end = (end + 3) & ~3
        deltas.append((original_padded_end, padded_length - (original_padded_end - start)))
        cursor = original_padded_end
    rebuilt.extend(binary[cursor:])

    image_view_indices = {entry[1] for entry in jpeg_entries}
    for view_index, view in enumerate(views):
        if view_index in image_view_indices:
            continue
        original_offset = int(view.get("byteOffset", 0))
        old_offset = original_offset
        for replaced_end, delta in deltas:
            if original_offset >= replaced_end:
                old_offset += delta
        view["byteOffset"] = old_offset
    document["buffers"][0]["byteLength"] = len(rebuilt)
    after_semantics = semantic_hashes(document)
    if before_semantics != after_semantics:
        raise ValueError("scene, mesh, skin, animation, or accessor semantics changed")
    if any(image.get("mimeType") == "image/jpeg" for image in images):
        raise ValueError("a JPEG image reference remains after conversion")
    write_glb(destination, document, bytes(rebuilt))
    return {"converted_images": [entry[5] for entry in jpeg_entries], "semantic_hashes": before_semantics}, before_semantics


def manifest_files(bundle: Path) -> dict[str, dict[str, Any]]:
    files: dict[str, dict[str, Any]] = {}
    for path in sorted(bundle.rglob("*")):
        if path.is_file() and path.name != "preview-scene.json":
            relative = path.relative_to(bundle).as_posix()
            files[relative] = {"bytes": path.stat().st_size, "sha256": sha256_file(path)}
    return files


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    # This development script lives in the Rust checkout; the immutable bundle
    # store is the shared workspace sibling, not the checkout itself.
    parser.add_argument("--integration-root", type=Path,
                        default=Path(__file__).resolve().parents[3] / ".tidewater-integration")
    parser.add_argument("--source", type=Path)
    parser.add_argument("--output-bundle", type=Path)
    parser.add_argument("--ffmpeg", default=shutil.which("ffmpeg") or "ffmpeg")
    parser.add_argument("--ffprobe", default=shutil.which("ffprobe") or "ffprobe")
    parser.add_argument("--node", default=shutil.which("node") or "node")
    parser.add_argument("--validator-module", help="existing directory containing Khronos gltf-validator index.js")
    arguments = parser.parse_args()
    integration_root = arguments.integration_root.resolve()
    source = (arguments.source or integration_root / "preview-scene-bundle-fish-stand-content-fish-009" / "scene" / "joe.glb").resolve()
    bundle = (arguments.output_bundle or integration_root / "preview-scene-bundle-fish-stand-content-fish-010").resolve()
    if not source.is_file():
        raise FileNotFoundError(f"source GLB does not exist: {source}")
    source_hash = sha256_file(source)
    if source_hash != EXPECTED_SOURCE_SHA256:
        raise ValueError(f"source SHA-256 differs from the pinned Joe source: {source_hash}")
    if bundle.exists():
        raise FileExistsError(f"refusing to mutate an existing bundle: {bundle}")
    validator_module = find_validator_module(arguments.validator_module)
    ffmpeg_version = run([arguments.ffmpeg, "-version"], text=True).stdout.splitlines()[0]
    validator_package = json.loads((validator_module / "package.json").read_text(encoding="utf-8"))
    staging = bundle.with_name(bundle.name + ".partial")
    if staging.exists():
        raise FileExistsError(f"refusing to reuse an incomplete staging directory: {staging}")
    try:
        converted = staging / "scene" / "joe.glb"
        original = staging / "fish-stand-content" / "source" / "public" / "models" / "characters" / "joe.glb"
        license_source = source.parent.parent / "fish-stand-content" / "source" / "public" / "models" / "characters" / "LICENSE-Rocketbox.md"
        converted.parent.mkdir(parents=True)
        original.parent.mkdir(parents=True)
        shutil.copyfile(source, original)  # A copy, never a hardlink to bundle 009.
        if sha256_file(original) != source_hash:
            raise ValueError("preserved source copy hash mismatch")
        if license_source.is_file():
            shutil.copyfile(license_source, original.parent / license_source.name)
        conversion, semantic_hash = convert(source, converted, arguments.ffmpeg, arguments.ffprobe)
        report_path = staging / "validation" / "joe.gltf-validator.json"
        report_path.parent.mkdir(parents=True)
        validator_report = validate_with_khronos(arguments.node, validator_module, converted, report_path)
        provenance = {
            "schema": "forge.tidewater.embedded-jpeg-to-png.v1",
            "source": {"path": "bundle-009/scene/joe.glb", "sha256": source_hash, "license_file": "fish-stand-content/source/public/models/characters/LICENSE-Rocketbox.md"},
            "derived": {"path": "scene/joe.glb", "sha256": sha256_file(converted), "conversion": "embedded image/jpeg bufferViews only -> embedded image/png; scene, nodes, meshes, skins, animations, accessors, materials and texture references preserved"},
            "tools": {"ffmpeg": ffmpeg_version, "khronos_gltf_validator": validator_package.get("version"), "validator_module": "pre-existing local cache"},
            "converted_images": conversion["converted_images"],
            "semantic_hashes_verified_equal_before_after": semantic_hash,
            "validator": {"report": "validation/joe.gltf-validator.json", "num_errors": validator_report["issues"]["numErrors"], "num_warnings": validator_report["issues"]["numWarnings"]},
            "limitations": ["This is an additive asset bundle only; it does not prove Bevy runtime loading, Studio availability, or a playable placement."],
        }
        provenance_path = staging / "provenance" / "joe-jpeg-to-png.json"
        provenance_path.parent.mkdir(parents=True)
        provenance_path.write_bytes(canonical_json(provenance) + b"\n")
        manifest = {
            "schema": "forge.tidewater.preview-scene",
            "version": 1,
            "scope": "Additive Joe PNG fallback for the native fixed-view prototype; not a distributable module or complete Studio map",
            "source_bundle": "preview-scene-bundle-fish-stand-content-fish-009",
            "source_commit": "1438b1abfcaee3267092b75573014f4d9b4a983c",
            "files": manifest_files(staging),
        }
        (staging / "preview-scene.json").write_bytes(canonical_json(manifest) + b"\n")
        staging.rename(bundle)
    except Exception:
        if staging.exists():
            shutil.rmtree(staging)
        raise
    print(json.dumps({"bundle": str(bundle), "manifest": str(bundle / "preview-scene.json"), "source_sha256": source_hash, "derived_sha256": sha256_file(bundle / "scene" / "joe.glb")}, indent=2))
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (FileNotFoundError, ValueError, subprocess.CalledProcessError, OSError) as error:
        print(f"ERROR: {error}", file=sys.stderr)
        raise SystemExit(1)
