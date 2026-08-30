#!/usr/bin/env python3
"""Convert a governed asset inventory CSV into an Asset Intelligence v1 overlay."""

from __future__ import annotations

import argparse
import csv
import hashlib
import json
from pathlib import Path, PurePosixPath
import re
from urllib.parse import unquote


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def dependency_closure(model: Path) -> tuple[str, str]:
    model_hash = sha256(model)
    entries = [(model.name, model_hash)]
    if model.suffix.lower() == ".gltf":
        payload = json.loads(model.read_text(encoding="utf-8"))
        for collection in ("buffers", "images"):
            for item in payload.get(collection, []):
                uri = item.get("uri")
                if not uri or uri.lower().startswith("data:"):
                    continue
                decoded = unquote(uri)
                relative = PurePosixPath(decoded)
                if relative.is_absolute() or any(part in {"", ".", ".."} for part in relative.parts):
                    raise ValueError(f"Unsafe glTF dependency URI: {uri}")
                dependency = (model.parent / Path(*relative.parts)).resolve()
                if not dependency.is_file() or model.parent.resolve() not in dependency.parents:
                    raise FileNotFoundError(f"Missing or escaped glTF dependency: {uri}")
                entries.append((relative.as_posix(), sha256(dependency)))
    closure = hashlib.sha256(
        "\n".join(f"{name}:{digest}" for name, digest in sorted(entries)).encode("utf-8")
    ).hexdigest()
    return model_hash, closure


def split_names(value: str) -> list[str]:
    return sorted({item.strip().lower() for item in re.split(r"[|;,]", value or "") if item.strip()})


def classification(row: dict[str, str]) -> tuple[str, str, str, str, list[str]]:
    role = row["assembly_role"].split(",", 1)[0].strip().lower()
    family = row["family"].strip().lower()
    if role == "interactive-leaf":
        semantic, surface, snap = "door", "opening", "hinge"
        uses = ["interaction", "navigation-blocker", "opening"]
    elif role == "animation-source":
        semantic, surface, snap = "animation", "rig", "skeleton"
        uses = ["animation-library", "retargeting"]
    elif "character" in role or family.startswith("characters/"):
        semantic, surface, snap = "character", "character", "rig"
        uses = ["actor", "character-assembly", "creature-blueprint"]
    elif family.startswith("architecture/"):
        semantic, surface, snap = "architecture", "ground", "modular"
        uses = ["environment-construction", "level-design", "modular-building"]
    elif family.startswith("nature/"):
        semantic = "vegetation" if any(token in family for token in (
            "tree", "plant", "grass", "flower", "fern", "bush", "clover", "mushroom"
        )) else "natural-prop"
        surface, snap = "ground", "scatter" if "scatter" in role or "vegetation" in role else "grid"
        uses = ["biome-dressing", "environment", "terrain-scatter"]
    else:
        semantic, surface, snap = "prop", "ground", "grid"
        uses = ["placeable", "scene-dressing"]
    if "vine" in row["name"].lower() or "banner" in family:
        surface, snap = "wall", "surface"
        uses = sorted(set(uses + ["surface-decoration"]))
    return semantic, role, surface, snap, sorted(uses)


def pivot_kind(role: str, surface: str) -> str:
    if role == "interactive-leaf":
        return "hinge-side"
    if role.startswith("modular-"):
        return "modular-snap"
    if "character" in role:
        return "rig-origin"
    if surface == "wall":
        return "surface-attach"
    return "source-origin"


def number(row: dict[str, str], key: str) -> float:
    return float(row[key])


def integer(row: dict[str, str], key: str) -> int:
    return int(row[key] or 0)


def profile(row: dict[str, str], vault: Path | None) -> dict:
    semantic, role, surface, snap, uses = classification(row)
    source_min = [number(row, f"bounds_min_{axis}_m") for axis in "xyz"]
    source_max = [number(row, f"bounds_max_{axis}_m") for axis in "xyz"]
    bounds_min = [source_min[0], source_min[2], -source_max[1]]
    bounds_max = [source_max[0], source_max[2], -source_min[1]]
    dimensions = [
        bounds_max[0] - bounds_min[0],
        bounds_max[1] - bounds_min[1],
        bounds_max[2] - bounds_min[2],
    ]
    model_hash = closure_hash = ""
    if vault is not None:
        model = (vault / Path(*PurePosixPath(row["model_relative_path"]).parts)).resolve()
        if not model.is_file() or vault.resolve() not in model.parents:
            raise FileNotFoundError(f"Governed inventory model is missing: {row['model_relative_path']}")
        model_hash, closure_hash = dependency_closure(model)
    family = row["family"].strip().lower()
    description_fr = (
        f"{row['name']} est un asset {semantic} de la famille {row['family']}, "
        f"prévu pour le rôle {role}. Son origine et ses bornes ont été mesurées dans Blender ; "
        "le pivot reste à confirmer dans son assemblage réel."
    )
    description_en = (
        f"{row['name']} is a {semantic} asset in the {row['family']} family, intended for the "
        f"{role} role. Its origin and bounds were measured in Blender; the pivot still requires "
        "confirmation in a real assembly."
    )
    return {
        "schema_version": 1,
        "external_id": row["asset_id"],
        "model_content_sha256": model_hash,
        "dependency_closure_sha256": closure_hash,
        "descriptions": {"en": description_en, "fr": description_fr},
        "semantic_type": semantic,
        "family": family,
        "assembly_role": role,
        "use_cases": uses,
        "geometry": {
            "bounds": {
                "minimum": {"x": bounds_min[0], "y": bounds_min[1], "z": bounds_min[2]},
                "maximum": {"x": bounds_max[0], "y": bounds_max[1], "z": bounds_max[2]},
            },
            "dimensions_meters": {"x": dimensions[0], "y": dimensions[1], "z": dimensions[2]},
            "source_origin_meters": {"x": 0.0, "y": 0.0, "z": 0.0},
            "meters_per_unit": 1.0,
            "up_axis": "y",
            "forward_axis": "-z",
            "pivot_class": row["pivot_class_heuristic"].strip().lower(),
        },
        "placement": {
            "surface": surface,
            "snap_mode": snap,
            "suggested_pivot": {
                "name": "placement-pivot",
                "kind": pivot_kind(role, surface),
                "position_meters": {"x": 0.0, "y": 0.0, "z": 0.0},
                "rotation_degrees": {"x": 0.0, "y": 0.0, "z": 0.0},
                "confidence": 0.92,
                "evidence_source": "medieval-inventory-blender",
                "requires_review": True,
            },
            "anchors": [],
        },
        "technical": {
            "mesh_count": integer(row, "mesh_object_count"),
            "vertex_count": integer(row, "vertices"),
            "triangle_count": integer(row, "triangles"),
            "material_count": integer(row, "material_count"),
            "texture_count": None,
            "armature_count": integer(row, "armature_count"),
            "bone_count": integer(row, "bone_count"),
            "animation_count": integer(row, "animation_count"),
            "materials": [],
            "animations": split_names(row["animation_names"]),
        },
        "readiness": "placement-suggested",
        "review_status": "generated-review-required",
        "generated_by": "openrpg-medieval-inventory/1",
        "confidence": 0.92,
        "evidence": [
            {
                "field": "geometry-and-placement",
                "source": "medieval-inventory-blender",
                "confidence": 0.92,
                "detail": (
                    "Bounds, dimensions, source origin and pivot class come from the governed Blender "
                    "inventory and reviewed contact sheets; typed placement still requires an assembly review."
                ),
            }
        ],
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("inventory", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--vault", type=Path)
    args = parser.parse_args()
    inventory = args.inventory.resolve()
    output = args.output.resolve()
    vault = args.vault.resolve() if args.vault else None
    with inventory.open("r", encoding="utf-8-sig", newline="") as stream:
        profiles = [profile(row, vault) for row in csv.DictReader(stream)]
    profiles.sort(key=lambda item: item["external_id"])
    if len({item["external_id"].lower() for item in profiles}) != len(profiles):
        raise ValueError("Asset intelligence external IDs must be unique.")
    output.parent.mkdir(parents=True, exist_ok=True)
    temporary = output.with_name(f".{output.name}.tmp")
    temporary.write_text(
        json.dumps({"schema_version": 1, "profiles": profiles}, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )
    temporary.replace(output)
    print(f"OPENRPG_FORGE_ASSET_INTELLIGENCE_OVERLAY_PASS profiles={len(profiles)} output={output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
