"""Shared diagnostics for existing source/render verifiers, independent of a map.

These functions measure declared data and paired diagnostic passes. They never
repair inputs, choose artistic tolerances or qualify final rendering. NumPy is
loaded only by the numerical checks; manifest checks use the standard library.
"""
import math
from pathlib import Path, PurePosixPath


def reconcile_source_ids(source_ids, imported_ids, exclusions=None):
    """Account for every source identity, including explicit reasoned omissions.

    The caller selects the source scope/state. An exclusion is an audited reason,
    not proof that an object is invisible. No matching by count, name or proximity.
    """
    def unique(values, label):
        result = set()
        for value in values:
            if not isinstance(value, str) or not value.strip():
                raise ValueError(f"{label}: missing source identity")
            if value in result:
                raise ValueError(f"{label}: duplicate source identity {value}")
            result.add(value)
        return result

    source = unique(source_ids, "source")
    imported = unique(imported_ids, "imported")
    exclusions = {} if exclusions is None else exclusions
    omitted = unique(exclusions, "exclusions")
    for identity, reason in exclusions.items():
        if not isinstance(reason, str) or not reason.strip():
            raise ValueError(f"exclusion {identity}: an explicit reason is required")
    unexpected = (imported | omitted) - source
    overlap = imported & omitted
    missing = source - imported - omitted
    if unexpected or overlap or missing:
        raise ValueError(f"source coverage: missing={sorted(missing)}, "
                         f"unexpected={sorted(unexpected)}, imported_and_excluded={sorted(overlap)}")
    return {"source_ids": len(source), "imported_ids": len(imported),
            "excluded_ids": len(omitted), "exclusion_reasons": dict(sorted(exclusions.items())),
            "scope": "declared identities only; mesh/material/render presence not qualified"}


def verify_texture_mips(root, mips, manifest_paths, bytes_per_texel=4):
    """Verify the full declared uncompressed mip chain and its reference closure.

    Format, colour space, channel semantics and sampler come from the caller's
    source contract. Do not infer them from texture names or regenerate mips.
    Existing manifest verifiers remain responsible for payload hashes.
    """
    def integer(value, label, minimum=1):
        if type(value) is not int or value < minimum:
            raise ValueError(f"invalid {label}: {value!r}")
        return value

    integer(bytes_per_texel, "bytes per texel")
    if not isinstance(mips, list) or not mips:
        raise ValueError("texture has no mip chain")
    width = integer(mips[0]["width"], "mip width")
    height = integer(mips[0]["height"], "mip height")
    if len(mips) != max(width, height).bit_length():
        raise ValueError("incomplete or excessive texture mip chain")
    root = Path(root).resolve(strict=True)
    manifest_paths = set(manifest_paths)
    seen = set()
    total = 0
    for level, mip in enumerate(mips):
        if integer(mip["level"], "mip level", 0) != level:
            raise ValueError("duplicate, missing or reordered texture mip level")
        expected = (max(1, width >> level), max(1, height >> level))
        dimensions = (integer(mip["width"], "mip width"),
                      integer(mip["height"], "mip height"))
        if dimensions != expected:
            raise ValueError(f"mip {level}: dimensions {dimensions} differ from {expected}")
        name = mip["path"]
        if not isinstance(name, str) or not name or "\\" in name or ":" in name:
            raise ValueError("unsafe texture mip path")
        path = PurePosixPath(name)
        if path.is_absolute() or ".." in path.parts or path.as_posix() != name:
            raise ValueError("unsafe texture mip path")
        if name not in manifest_paths or name in seen:
            raise ValueError(f"mip {level}: unmanifested or reused texture payload {name}")
        full = (root / name).resolve(strict=True)
        if not full.is_relative_to(root) or not full.is_file():
            raise ValueError("texture mip escapes the package")
        size = expected[0] * expected[1] * bytes_per_texel
        if full.stat().st_size != size:
            raise ValueError(f"mip {level}: truncated or excessive texture payload {name}")
        seen.add(name)
        total += size
    return {"width": width, "height": height, "mips": len(mips), "bytes": total}


def verify_position_roundtrip(local_positions, source_world_positions, world_matrix,
                              tolerance_m, label="geometry"):
    """Compare composed affine placement to the source in physical metres.

    The source positions must be an independent oracle. Using the imported
    transform to generate both inputs would only verify internal consistency.
    """
    import numpy as np
    if isinstance(tolerance_m, bool) or not math.isfinite(tolerance_m) or tolerance_m < 0:
        raise ValueError("position tolerance must be finite and nonnegative")
    local = np.asarray(local_positions, dtype=np.float64)
    source = np.asarray(source_world_positions, dtype=np.float64)
    matrix = np.asarray(world_matrix, dtype=np.float64)
    if (local.ndim != 2 or local.shape[1] != 3 or len(local) == 0
            or source.shape != local.shape or matrix.shape != (4, 4)):
        raise ValueError(f"{label}: invalid position or matrix shape")
    if not all(np.isfinite(a).all() for a in (local, source, matrix)):
        raise ValueError(f"{label}: non-finite placement input")
    if not np.array_equal(matrix[3], [0, 0, 0, 1]):
        raise ValueError(f"{label}: expected affine world matrix")
    with np.errstate(over="ignore", invalid="ignore"):
        restored = local @ matrix[:3, :3].T + matrix[:3, 3]
        delta = restored - source
        distances = np.hypot(np.hypot(delta[:, 0], delta[:, 1]), delta[:, 2])
    if not np.isfinite(distances).all():
        raise ValueError(f"{label}: placement arithmetic overflow")
    worst = int(np.argmax(distances))
    maximum = float(distances[worst])
    if maximum > tolerance_m:
        raise ValueError(f"{label}: vertex {worst} moved {maximum:.9g} m "
                         f"(allowed {tolerance_m:.9g} m)")
    return {"vertices": len(local), "max_distance_m": maximum,
            "p95_distance_m": float(np.percentile(distances, 95)),
            "tolerance_m": tolerance_m}


def coverage_diagnostics(reference, candidate):
    """Separate missing and additional coverage; edge slack is diagnostic only."""
    import numpy as np
    reference, candidate = np.asarray(reference), np.asarray(candidate)
    if (reference.dtype != np.bool_ or candidate.dtype != np.bool_
            or reference.ndim != 2 or candidate.shape != reference.shape or not reference.size):
        raise ValueError("coverage requires two equally sized nonempty boolean masks")

    def dilate(mask):
        height, width = mask.shape
        padded = np.pad(mask, 1)
        result = np.zeros_like(mask)
        for y in range(3):
            for x in range(3):
                result |= padded[y:y + height, x:x + width]
        return result

    def region(mask):
        rows = np.flatnonzero(mask.any(axis=1))
        columns = np.flatnonzero(mask.any(axis=0))
        return {"pixels": int(mask.sum()), "bbox_xyxy_inclusive":
                [int(columns[0]), int(rows[0]), int(columns[-1]), int(rows[-1])]
                if len(rows) else None}

    missing, extra = reference & ~candidate, candidate & ~reference
    intersection = int((reference & candidate).sum())
    union = int((reference | candidate).sum())
    source_count, candidate_count = int(reference.sum()), int(candidate.sum())
    return {"status": "measured" if source_count else "empty_reference_no_coverage_evidence",
            "reference_pixels": source_count, "candidate_pixels": candidate_count,
            "intersection_pixels": intersection, "union_pixels": union,
            "intersection_over_union": intersection / union if union else None,
            "missing": region(missing), "additional": region(extra),
            "missing_fraction_of_reference": int(missing.sum()) / source_count if source_count else None,
            "additional_fraction_of_candidate": int(extra.sum()) / candidate_count if candidate_count else None,
            "missing_beyond_one_pixel": region(reference & ~dilate(candidate)),
            "additional_beyond_one_pixel": region(candidate & ~dilate(reference)),
            "render_quality_qualified": False}


def standard_depth_diagnostics(reference, candidate):
    """Measure standard 0..1 depth with clear=1; no reverse-Z inference."""
    import numpy as np
    reference = np.asarray(reference, dtype=np.float64)
    candidate = np.asarray(candidate, dtype=np.float64)
    if reference.ndim != 2 or candidate.shape != reference.shape or not reference.size:
        raise ValueError("depth requires two equally sized nonempty layers")
    if not all(np.isfinite(a).all() and (a >= 0).all() and (a <= 1).all()
               for a in (reference, candidate)):
        raise ValueError("standard depth must be finite and within 0..1")
    visible_reference, visible_candidate = reference < 1, candidate < 1
    result = coverage_diagnostics(visible_reference, visible_candidate)
    shared = visible_reference & visible_candidate
    differences = candidate[shared] - reference[shared]
    absolute = abs(differences)
    result["shared_depth"] = {
        "pixels": int(shared.sum()),
        "mean_absolute_error": float(absolute.mean()) if len(absolute) else None,
        "p95_absolute_error": float(np.percentile(absolute, 95)) if len(absolute) else None,
        "max_absolute_error": float(absolute.max()) if len(absolute) else None,
        "candidate_nearer_pixels": int((differences < 0).sum()),
        "candidate_farther_pixels": int((differences > 0).sum()),
        "units": "normalized_standard_depth_not_metres"}
    return result
