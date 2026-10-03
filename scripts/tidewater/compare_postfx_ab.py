"""Measure a fixed-camera PNG A/B without inferring source visual parity."""

from __future__ import annotations

import argparse
import hashlib
import json
import struct
import subprocess
from pathlib import Path

import numpy as np


def decode(path: Path) -> tuple[np.ndarray, str]:
    encoded = path.read_bytes()
    if not encoded.startswith(b"\x89PNG\r\n\x1a\n"):
        raise ValueError(f"Not a PNG: {path}")
    width, height = struct.unpack(">II", encoded[16:24])
    if (width, height) != (1920, 1080):
        raise ValueError(f"Unexpected dimensions {width}x{height}: {path}")
    pixels = subprocess.check_output(
        ["ffmpeg", "-v", "error", "-i", str(path), "-f", "rawvideo", "-pix_fmt", "rgb24", "-"],
        timeout=30,
    )
    return (
        np.frombuffer(pixels, dtype=np.uint8).reshape(height, width, 3),
        hashlib.sha256(encoded).hexdigest(),
    )


def metrics(off: np.ndarray, on: np.ndarray) -> dict[str, float]:
    delta = np.abs(on.astype(np.int16) - off.astype(np.int16))
    weights = np.array([0.2126, 0.7152, 0.0722])
    off_luma = off @ weights
    on_luma = on @ weights
    return {
        "off_mean_luminance": round(float(off_luma.mean()), 3),
        "on_mean_luminance": round(float(on_luma.mean()), 3),
        "mean_abs_luminance_delta": round(float(np.abs(on_luma - off_luma).mean()), 3),
        "mean_abs_rgb_channel_delta": round(float(delta.mean()), 3),
        "fraction_pixels_any_channel_delta_gt_2": round(float(np.mean(np.any(delta > 2, axis=2))), 5),
    }


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("off", type=Path)
    parser.add_argument("on", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    off, off_hash = decode(args.off)
    on, on_hash = decode(args.on)
    receipt = {
        "scope": "fixed-camera native post-processing A/B; not source parity",
        "off": {"path": str(args.off.resolve()), "sha256": off_hash},
        "on": {"path": str(args.on.resolve()), "sha256": on_hash},
        "dimensions": [1920, 1080],
        "full": metrics(off, on),
        "center_half": metrics(off[270:810, 480:1440], on[270:810, 480:1440]),
    }
    args.output.write_text(json.dumps(receipt, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(json.dumps(receipt, ensure_ascii=False))


if __name__ == "__main__":
    main()
