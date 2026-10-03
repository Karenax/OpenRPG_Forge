"""Compare fixed-camera Tidewater caustic on/off PNGs and ocean receipts.

Requires NumPy and ffmpeg. The JSON output is deliberately local evidence,
not a visual-parity verdict against the Tidewater reference.
"""

import hashlib
import json
import re
import struct
import subprocess
import sys
from pathlib import Path

import numpy as np


def decode(path: Path) -> tuple[np.ndarray, str]:
    encoded = path.read_bytes()
    if encoded[:8] != b"\x89PNG\r\n\x1a\n":
        raise ValueError(f"not a PNG: {path}")
    width, height = struct.unpack(">II", encoded[16:24])
    if (width, height) != (1920, 1080):
        raise ValueError(f"unexpected capture dimensions: {width}x{height}")
    raw = subprocess.check_output(
        ["ffmpeg", "-v", "error", "-i", str(path), "-f", "rawvideo", "-pix_fmt", "rgb24", "-"],
        timeout=30,
    )
    if len(raw) != width * height * 3:
        raise ValueError(f"incomplete decoded capture: {path}")
    return np.frombuffer(raw, dtype=np.uint8).reshape(height, width, 3), hashlib.sha256(encoded).hexdigest()


def sources(log: Path) -> dict[str, str]:
    text = log.read_text(encoding="utf-8")
    result = {}
    for name in ("displacement-m0.rgba16.bin", "derivatives-m0.rgba16.bin", "frame-0120-shore.rgba16.bin"):
        match = re.search(rf"^{re.escape(name)} sha256 ([0-9a-f]{{64}})$", text, re.MULTILINE)
        if match is None:
            raise ValueError(f"missing GPU source receipt {name}: {log}")
        result[name] = match.group(1)
    return result


def main() -> None:
    if len(sys.argv) != 6:
        raise SystemExit("usage: compare_caustic_ab.py ON.png OFF.png ON.log OFF.log RECEIPT.json")
    on_path, off_path, on_log, off_log, output = map(Path, sys.argv[1:])
    on_sources, off_sources = sources(on_log), sources(off_log)
    if on_sources != off_sources:
        raise ValueError("the two captures do not share identical FFT and shore outputs")
    on, on_hash = decode(on_path)
    off, off_hash = decode(off_path)
    delta = np.abs(on.astype(np.int16) - off.astype(np.int16))
    upper = delta[:320]
    seabed = delta[500:1000, 200:1800]
    receipt = {
        "scope": "fixed-camera native Tidewater terrain caustics A/B; no source-parity claim",
        "camera_m": [40.0, -1.4, 29.5],
        "dimensions": [1920, 1080],
        "on": {"path": str(on_path), "sha256": on_hash},
        "off": {"path": str(off_path), "sha256": off_hash},
        "matching_gpu_sources": on_sources,
        "upper_0_320_exact_equal": bool(not np.any(upper)),
        "seabed_rect_xyxy": [200, 500, 1800, 1000],
        "seabed_mean_abs_channel_difference": float(seabed.mean()),
        "seabed_fraction_pixels_delta_gt_2": float(np.mean(np.any(seabed > 2, axis=2))),
    }
    receipt["pass"] = receipt["upper_0_320_exact_equal"] and receipt["seabed_fraction_pixels_delta_gt_2"] > 0.1
    output.write_text(json.dumps(receipt, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(json.dumps(receipt, ensure_ascii=False))
    if not receipt["pass"]:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
