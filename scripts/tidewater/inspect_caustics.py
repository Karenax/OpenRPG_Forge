"""Inspect raw RGBA16F photon maps from the native Tidewater proof receipt."""

from __future__ import annotations

import argparse
import hashlib
import json
import struct
import zlib
from pathlib import Path

import numpy as np


def write_gray_png(path: Path, pixels: np.ndarray) -> None:
    height, width = pixels.shape
    image = b"".join(b"\0" + row.tobytes() for row in pixels)

    def chunk(kind: bytes, payload: bytes) -> bytes:
        return (
            struct.pack(">I", len(payload))
            + kind
            + payload
            + struct.pack(">I", zlib.crc32(kind + payload))
        )

    path.write_bytes(
        b"\x89PNG\r\n\x1a\n"
        + chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 0, 0, 0, 0))
        + chunk(b"IDAT", zlib.compress(image, 9))
        + chunk(b"IEND", b"")
    )


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("receipt", type=Path)
    args = parser.parse_args()
    receipt = json.loads(args.receipt.read_text(encoding="utf-8"))
    if receipt["schema"] != "forge.tidewater.caustic-gpu-proof.v1":
        raise ValueError("unexpected caustic proof schema")

    result = {}
    for name in ("fine", "broad"):
        item = receipt[name]
        side = item["side"]
        raw = args.receipt.parent / item["raw"]
        data = raw.read_bytes()
        if len(data) != side * side * 8 or hashlib.sha256(data).hexdigest() != item["sha256"]:
            raise ValueError(f"invalid {name} caustic bytes")
        rgba = np.frombuffer(data, dtype="<f2").astype(np.float32).reshape(side, side, 4)
        channels = {}
        for index, channel in enumerate(("red", "green")):
            values = rgba[:, :, index]
            low, high = np.percentile(values, [1, 99])
            channels[channel] = {
                "min": float(values.min()),
                "max": float(values.max()),
                "mean": float(values.mean()),
                "std": float(values.std()),
                "p01": float(low),
                "p99": float(high),
            }
            scaled = np.uint8(np.clip((values - low) / max(high - low, 1e-6), 0, 1) ** 0.65 * 255)
            write_gray_png(args.receipt.with_name(f"{args.receipt.stem}-{name}-{channel}.png"), scaled)
        channels["red_green_mean_absolute_difference"] = float(
            np.abs(rgba[:, :, 0] - rgba[:, :, 1]).mean()
        )
        result[name] = channels
    print(json.dumps(result, indent=2))


if __name__ == "__main__":
    main()
