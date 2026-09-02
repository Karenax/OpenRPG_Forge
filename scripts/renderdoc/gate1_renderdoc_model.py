#!/usr/bin/env python3
"""Pure, fail-closed RenderDoc action accounting for Gate 1 route R1-037.

The module deliberately does not import RenderDoc. A future qrenderdoc adapter is
responsible for converting the capture into the strict normalized schema accepted
here. This keeps the accounting policy deterministic and testable without a GPU.
"""

from __future__ import annotations

import argparse
import json
import sys
from collections.abc import Mapping
from dataclasses import dataclass
from pathlib import Path
from typing import Any


INPUT_SCHEMA = "forge-gate1-renderdoc-actions/v1"
OUTPUT_SCHEMA = "forge-gate1-renderdoc-analysis/v1"
UINT64_MAX = (1 << 64) - 1

_ROOT_FIELDS = frozenset({"schema", "capture", "actions"})
_CAPTURE_FIELDS = frozenset(
    {
        "captured_frame_count",
        "present_count",
        "local_replay_support",
        "pipeline_type",
        "degraded",
    }
)
_ACTION_FIELDS = frozenset(
    {
        "event_id",
        "name",
        "flags",
        "topology",
        "num_indices",
        "num_instances",
        "indirect",
        "indirect_resolved",
        "active_stages",
        "children",
    }
)
_SUPPORTED_TOPOLOGIES = frozenset({"TriangleList", "LineList", "PointList"})
_SUPPORTED_DRAW_STAGES = frozenset({"Vertex", "Fragment"})


class ContractError(ValueError):
    """Stable fail-closed error returned for a malformed or unsupported capture."""

    def __init__(self, code: str, path: str, message: str) -> None:
        self.code = code
        self.path = path
        self.message = message
        super().__init__(f"{code} at {path}: {message}")

    def as_dict(self) -> dict[str, object]:
        return {
            "schema": OUTPUT_SCHEMA,
            "status": "FAIL",
            "error": {
                "code": self.code,
                "path": self.path,
                "message": self.message,
            },
        }


@dataclass
class _Counters:
    actions_visited: int = 0
    draws: int = 0
    instances: int = 0
    triangles: int = 0
    lines: int = 0
    points: int = 0
    triangle_list_draws: int = 0
    line_list_draws: int = 0
    point_list_draws: int = 0


def _expect_mapping(value: object, path: str) -> Mapping[str, object]:
    if not isinstance(value, Mapping):
        raise ContractError("invalid_type", path, "expected an object")
    if any(not isinstance(key, str) for key in value):
        raise ContractError("invalid_field_name", path, "all field names must be strings")
    return value


def _expect_exact_fields(
    value: Mapping[str, object], expected: frozenset[str], path: str
) -> None:
    actual = frozenset(value.keys())
    missing = sorted(expected - actual)
    unexpected = sorted(actual - expected)
    if missing or unexpected:
        details: list[str] = []
        if missing:
            details.append(f"missing={','.join(missing)}")
        if unexpected:
            details.append(f"unexpected={','.join(unexpected)}")
        raise ContractError("contract_shape", path, "; ".join(details))


def _expect_string(value: object, path: str) -> str:
    if not isinstance(value, str) or not value:
        raise ContractError("invalid_type", path, "expected a non-empty string")
    return value


def _expect_optional_string(value: object, path: str) -> str | None:
    if value is None:
        return None
    return _expect_string(value, path)


def _expect_bool(value: object, path: str) -> bool:
    if not isinstance(value, bool):
        raise ContractError("invalid_type", path, "expected a boolean")
    return value


def _expect_uint64(value: object, path: str) -> int:
    if isinstance(value, bool) or not isinstance(value, int):
        raise ContractError("invalid_type", path, "expected an unsigned 64-bit integer")
    if value < 0 or value > UINT64_MAX:
        raise ContractError("integer_out_of_range", path, "value is outside uint64")
    return value


def _expect_list(value: object, path: str) -> list[object]:
    if not isinstance(value, list):
        raise ContractError("invalid_type", path, "expected an array")
    return value


def _expect_unique_strings(value: object, path: str) -> list[str]:
    raw_items = _expect_list(value, path)
    items: list[str] = []
    seen: set[str] = set()
    for index, raw_item in enumerate(raw_items):
        item = _expect_string(raw_item, f"{path}[{index}]")
        if item in seen:
            raise ContractError("duplicate_value", f"{path}[{index}]", item)
        seen.add(item)
        items.append(item)
    return items


def _checked_add(left: int, right: int, path: str) -> int:
    if left > UINT64_MAX - right:
        raise ContractError("integer_overflow", path, "uint64 addition overflow")
    return left + right


def _checked_multiply(left: int, right: int, path: str) -> int:
    if left != 0 and right > UINT64_MAX // left:
        raise ContractError("integer_overflow", path, "uint64 multiplication overflow")
    return left * right


def _validate_capture(value: object) -> dict[str, object]:
    capture = _expect_mapping(value, "$.capture")
    _expect_exact_fields(capture, _CAPTURE_FIELDS, "$.capture")

    captured_frame_count = _expect_uint64(
        capture["captured_frame_count"], "$.capture.captured_frame_count"
    )
    if captured_frame_count != 1:
        raise ContractError(
            "captured_frame_count",
            "$.capture.captured_frame_count",
            "exactly one captured frame is required",
        )

    present_count = _expect_uint64(capture["present_count"], "$.capture.present_count")
    if present_count != 1:
        raise ContractError(
            "present_count",
            "$.capture.present_count",
            "exactly one Present is required",
        )

    replay_support = _expect_string(
        capture["local_replay_support"], "$.capture.local_replay_support"
    )
    if replay_support != "Supported":
        raise ContractError(
            "local_replay_not_supported",
            "$.capture.local_replay_support",
            "LocalReplaySupport must be exactly Supported",
        )

    pipeline_type = _expect_string(capture["pipeline_type"], "$.capture.pipeline_type")
    if pipeline_type != "Vulkan":
        raise ContractError(
            "pipeline_type_not_qualified",
            "$.capture.pipeline_type",
            "only the exact Vulkan pipeline is qualified",
        )

    degraded = _expect_bool(capture["degraded"], "$.capture.degraded")
    if degraded:
        raise ContractError(
            "degraded_replay",
            "$.capture.degraded",
            "degraded replay is not qualified",
        )

    return {
        "captured_frame_count": captured_frame_count,
        "present_count": present_count,
        "local_replay_support": replay_support,
        "pipeline_type": pipeline_type,
        "degraded": degraded,
    }


def _increment(counter: int, amount: int, path: str) -> int:
    return _checked_add(counter, amount, path)


def _walk_action(
    raw_action: object,
    path: str,
    counters: _Counters,
    seen_event_ids: set[int],
    active_objects: set[int],
) -> None:
    object_id = id(raw_action)
    if object_id in active_objects:
        raise ContractError("action_cycle", path, "action graph contains a cycle")

    action = _expect_mapping(raw_action, path)
    active_objects.add(object_id)
    try:
        _expect_exact_fields(action, _ACTION_FIELDS, path)
        event_id = _expect_uint64(action["event_id"], f"{path}.event_id")
        if event_id == 0:
            raise ContractError(
                "invalid_event_id", f"{path}.event_id", "event_id must be greater than zero"
            )
        if event_id in seen_event_ids:
            raise ContractError(
                "duplicate_event_id", f"{path}.event_id", f"event_id {event_id} is repeated"
            )
        seen_event_ids.add(event_id)

        _expect_string(action["name"], f"{path}.name")
        flags = _expect_unique_strings(action["flags"], f"{path}.flags")
        topology = _expect_optional_string(action["topology"], f"{path}.topology")
        num_indices = _expect_uint64(action["num_indices"], f"{path}.num_indices")
        raw_instances = _expect_uint64(action["num_instances"], f"{path}.num_instances")
        indirect = _expect_bool(action["indirect"], f"{path}.indirect")
        indirect_resolved = _expect_bool(
            action["indirect_resolved"], f"{path}.indirect_resolved"
        )
        active_stages = _expect_unique_strings(
            action["active_stages"], f"{path}.active_stages"
        )
        children = _expect_list(action["children"], f"{path}.children")

        counters.actions_visited = _increment(
            counters.actions_visited, 1, f"{path}.actions_visited"
        )

        if children:
            for index, child in enumerate(children):
                _walk_action(
                    child,
                    f"{path}.children[{index}]",
                    counters,
                    seen_event_ids,
                    active_objects,
                )
            return

        if "MultiAction" in flags:
            raise ContractError(
                "unexpanded_multiaction",
                f"{path}.flags",
                "a MultiAction leaf has no normalized child actions",
            )

        if "Drawcall" not in flags:
            return

        if topology not in _SUPPORTED_TOPOLOGIES:
            rendered_topology = "null" if topology is None else topology
            raise ContractError(
                "unsupported_topology",
                f"{path}.topology",
                f"unsupported or unknown topology: {rendered_topology}",
            )

        flag_marks_indirect = "Indirect" in flags
        if indirect != flag_marks_indirect:
            raise ContractError(
                "indirect_metadata_mismatch",
                f"{path}.indirect",
                "indirect boolean and Indirect action flag disagree",
            )
        if not indirect_resolved:
            raise ContractError(
                "unresolved_indirect",
                f"{path}.indirect_resolved",
                "exact indirect counts were not resolved",
            )

        unsupported_stages = sorted(set(active_stages) - _SUPPORTED_DRAW_STAGES)
        if unsupported_stages:
            raise ContractError(
                "unsupported_shader_stage",
                f"{path}.active_stages",
                f"unsupported stages: {','.join(unsupported_stages)}",
            )
        if "Vertex" not in active_stages:
            raise ContractError(
                "missing_vertex_stage",
                f"{path}.active_stages",
                "a qualified draw must expose the Vertex stage",
            )

        instances = raw_instances if raw_instances != 0 else 1
        primitive_path = f"{path}.primitive_count"

        if topology == "TriangleList":
            if num_indices % 3 != 0:
                raise ContractError(
                    "triangle_count_not_divisible",
                    f"{path}.num_indices",
                    "TriangleList num_indices must be divisible by three",
                )
            triangles = _checked_multiply(num_indices // 3, instances, primitive_path)
            counters.triangles = _increment(counters.triangles, triangles, primitive_path)
            counters.triangle_list_draws = _increment(
                counters.triangle_list_draws, 1, f"{path}.draws_by_topology"
            )
        elif topology == "LineList":
            if num_indices % 2 != 0:
                raise ContractError(
                    "line_count_not_divisible",
                    f"{path}.num_indices",
                    "LineList num_indices must be divisible by two",
                )
            lines = _checked_multiply(num_indices // 2, instances, primitive_path)
            counters.lines = _increment(counters.lines, lines, primitive_path)
            counters.line_list_draws = _increment(
                counters.line_list_draws, 1, f"{path}.draws_by_topology"
            )
        else:
            points = _checked_multiply(num_indices, instances, primitive_path)
            counters.points = _increment(counters.points, points, primitive_path)
            counters.point_list_draws = _increment(
                counters.point_list_draws, 1, f"{path}.draws_by_topology"
            )

        counters.draws = _increment(counters.draws, 1, f"{path}.draws")
        counters.instances = _increment(counters.instances, instances, f"{path}.instances")
    finally:
        active_objects.remove(object_id)


def analyze_document(document: Mapping[str, object]) -> dict[str, object]:
    """Validate and account one normalized RenderDoc capture.

    Raises ContractError on every malformed, ambiguous, unsupported, or
    overflowing input. A returned dictionary is therefore a PASS for the pure
    accounting contract only; it does not by itself close R1-037.
    """

    root = _expect_mapping(document, "$")
    _expect_exact_fields(root, _ROOT_FIELDS, "$")

    schema = _expect_string(root["schema"], "$.schema")
    if schema != INPUT_SCHEMA:
        raise ContractError(
            "unsupported_schema",
            "$.schema",
            f"expected {INPUT_SCHEMA}",
        )

    capture = _validate_capture(root["capture"])
    actions = _expect_list(root["actions"], "$.actions")
    if not actions:
        raise ContractError("empty_action_tree", "$.actions", "at least one action is required")

    counters = _Counters()
    seen_event_ids: set[int] = set()
    active_objects: set[int] = set()
    for index, action in enumerate(actions):
        _walk_action(
            action,
            f"$.actions[{index}]",
            counters,
            seen_event_ids,
            active_objects,
        )

    if counters.draws == 0:
        raise ContractError(
            "empty_draw_set", "$.actions", "the captured Present contains no drawcall leaf"
        )

    return {
        "schema": OUTPUT_SCHEMA,
        "status": "PASS",
        "capture": capture,
        "metrics": {
            "actions_visited": counters.actions_visited,
            "draws": counters.draws,
            "instances": counters.instances,
            "triangles": counters.triangles,
            "lines": counters.lines,
            "points": counters.points,
            "draws_by_topology": {
                "TriangleList": counters.triangle_list_draws,
                "LineList": counters.line_list_draws,
                "PointList": counters.point_list_draws,
            },
        },
    }


def _load_json(path: Path) -> Mapping[str, object]:
    try:
        with path.open("r", encoding="utf-8") as stream:
            value: Any = json.load(stream)
    except json.JSONDecodeError as error:
        raise ContractError(
            "invalid_json",
            "$",
            f"line {error.lineno}, column {error.colno}: {error.msg}",
        ) from error
    except OSError as error:
        raise ContractError("input_io_error", "$", str(error)) from error
    return _expect_mapping(value, "$")


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(
        description="Validate and account a normalized Gate 1 RenderDoc action tree."
    )
    parser.add_argument("input", type=Path, help="forge-gate1-renderdoc-actions/v1 JSON")
    args = parser.parse_args(argv)

    try:
        result = analyze_document(_load_json(args.input))
    except ContractError as error:
        print(json.dumps(error.as_dict(), indent=2, sort_keys=True), file=sys.stderr)
        return 2

    print(json.dumps(result, indent=2, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
