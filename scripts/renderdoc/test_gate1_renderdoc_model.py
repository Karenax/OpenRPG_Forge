#!/usr/bin/env python3
"""Pure synthetic tests for the R1-037 RenderDoc accounting model."""

from __future__ import annotations

import copy
import json
import unittest
from pathlib import Path

from gate1_renderdoc_model import (
    INPUT_SCHEMA,
    OUTPUT_SCHEMA,
    UINT64_MAX,
    ContractError,
    analyze_document,
)


_HERE = Path(__file__).resolve().parent
_FIXTURES = _HERE / "fixtures"

_PASS_CAPTURE: dict[str, object] = {
    "captured_frame_count": 1,
    "present_count": 1,
    "local_replay_support": "Supported",
    "pipeline_type": "Vulkan",
    "degraded": False,
}

_PASS_ACTION: dict[str, object] = {
    "event_id": 1,
    "name": "Synthetic draw",
    "flags": ["Drawcall"],
    "topology": "TriangleList",
    "num_indices": 3,
    "num_instances": 1,
    "indirect": False,
    "indirect_resolved": True,
    "active_stages": ["Vertex", "Fragment"],
    "children": [],
}


def _load_fixture(name: str) -> dict[str, object]:
    with (_FIXTURES / name).open("r", encoding="utf-8") as stream:
        value = json.load(stream)
    if not isinstance(value, dict):
        raise AssertionError(f"fixture {name} must contain an object")
    return value


def _document(
    actions: list[dict[str, object]] | None = None,
    capture: dict[str, object] | None = None,
) -> dict[str, object]:
    return {
        "schema": INPUT_SCHEMA,
        "capture": copy.deepcopy(_PASS_CAPTURE if capture is None else capture),
        "actions": copy.deepcopy([_PASS_ACTION] if actions is None else actions),
    }


def _assert_contract_error(
    test: unittest.TestCase, expected_code: str, document: dict[str, object]
) -> ContractError:
    with test.assertRaises(ContractError) as context:
        analyze_document(document)
    test.assertEqual(expected_code, context.exception.code)
    test.assertTrue(context.exception.path.startswith("$"))
    test.assertTrue(context.exception.message)
    return context.exception


class Gate1RenderDocModelTests(unittest.TestCase):
    def test_manifest_matches_model_contract_and_keeps_route_open(self) -> None:
        with (_HERE.parent / "gate1-renderdoc.v1.json").open("r", encoding="utf-8") as stream:
            manifest = json.load(stream)

        self.assertEqual("forge-gate1-renderdoc-lock/v1", manifest["schema"])
        self.assertEqual("R1-037", manifest["route"])
        self.assertEqual(INPUT_SCHEMA, manifest["normalized_input"]["schema"])
        self.assertEqual(
            "scripts/renderdoc/gate1_renderdoc_model.py",
            manifest["normalized_input"]["model"],
        )
        self.assertEqual(
            ["TriangleList", "LineList", "PointList"],
            manifest["analysis"]["accepted_topologies"],
        )
        self.assertFalse(manifest["analysis"]["fps_from_renderdoc"])
        self.assertEqual("NOT_RUN", manifest["qualification_boundary"]["captures_status"])
        self.assertFalse(manifest["qualification_boundary"]["closes_r1_037"])
        self.assertFalse(manifest["qualification_boundary"]["closes_gate1"])
        self.assertFalse(manifest["qualification_boundary"]["authorizes_gate2"])

    def test_nominal_nested_fixture_counts_only_drawcall_leaves(self) -> None:
        fixture = _load_fixture("nominal_nested_actions.json")

        result = analyze_document(fixture)

        self.assertEqual(OUTPUT_SCHEMA, result["schema"])
        self.assertEqual("PASS", result["status"])
        self.assertEqual(
            {
                "actions_visited": 8,
                "draws": 4,
                "instances": 7,
                "triangles": 5,
                "lines": 2,
                "points": 15,
                "draws_by_topology": {
                    "TriangleList": 2,
                    "LineList": 1,
                    "PointList": 1,
                },
            },
            result["metrics"],
        )
        self.assertNotIn("fps", json.dumps(result).lower())

    def test_every_action_rejection_fixture_fails_closed(self) -> None:
        fixture = _load_fixture("action_rejections.json")
        self.assertEqual("forge-gate1-renderdoc-action-rejections/v1", fixture["schema"])
        cases = fixture["cases"]
        self.assertIsInstance(cases, list)
        names: set[str] = set()

        for raw_case in cases:
            self.assertIsInstance(raw_case, dict)
            name = raw_case["name"]
            self.assertIsInstance(name, str)
            self.assertNotIn(name, names)
            names.add(name)
            action = copy.deepcopy(_PASS_ACTION)
            action.update(copy.deepcopy(raw_case["overrides"]))
            with self.subTest(case=name):
                _assert_contract_error(self, raw_case["expected_code"], _document([action]))

        self.assertGreaterEqual(len(names), 20)

    def test_every_capture_rejection_fixture_fails_closed(self) -> None:
        fixture = _load_fixture("capture_rejections.json")
        self.assertEqual("forge-gate1-renderdoc-capture-rejections/v1", fixture["schema"])
        cases = fixture["cases"]
        self.assertIsInstance(cases, list)
        names: set[str] = set()

        for raw_case in cases:
            self.assertIsInstance(raw_case, dict)
            name = raw_case["name"]
            self.assertIsInstance(name, str)
            self.assertNotIn(name, names)
            names.add(name)
            capture = copy.deepcopy(_PASS_CAPTURE)
            capture.update(copy.deepcopy(raw_case["overrides"]))
            with self.subTest(case=name):
                _assert_contract_error(
                    self, raw_case["expected_code"], _document(capture=capture)
                )

        self.assertEqual(9, len(names))

    def test_duplicate_event_id_is_rejected(self) -> None:
        first = copy.deepcopy(_PASS_ACTION)
        second = copy.deepcopy(_PASS_ACTION)
        second["name"] = "Duplicate event"

        _assert_contract_error(
            self, "duplicate_event_id", _document([first, second])
        )

    def test_cyclic_in_memory_action_graph_is_rejected(self) -> None:
        action = copy.deepcopy(_PASS_ACTION)
        action["children"] = [action]
        document: dict[str, object] = {
            "schema": INPUT_SCHEMA,
            "capture": copy.deepcopy(_PASS_CAPTURE),
            "actions": [action],
        }

        _assert_contract_error(self, "action_cycle", document)

    def test_instance_total_overflow_is_rejected(self) -> None:
        first = copy.deepcopy(_PASS_ACTION)
        first.update(
            {
                "event_id": 1,
                "topology": "PointList",
                "num_indices": 0,
                "num_instances": UINT64_MAX,
            }
        )
        second = copy.deepcopy(_PASS_ACTION)
        second.update(
            {
                "event_id": 2,
                "topology": "PointList",
                "num_indices": 0,
                "num_instances": 1,
            }
        )

        _assert_contract_error(self, "integer_overflow", _document([first, second]))

    def test_primitive_total_overflow_is_rejected(self) -> None:
        first = copy.deepcopy(_PASS_ACTION)
        first.update(
            {
                "event_id": 1,
                "topology": "PointList",
                "num_indices": UINT64_MAX,
                "num_instances": 1,
            }
        )
        second = copy.deepcopy(_PASS_ACTION)
        second.update(
            {
                "event_id": 2,
                "topology": "PointList",
                "num_indices": 1,
                "num_instances": 1,
            }
        )

        _assert_contract_error(self, "integer_overflow", _document([first, second]))

    def test_empty_draw_set_is_rejected(self) -> None:
        marker = copy.deepcopy(_PASS_ACTION)
        marker.update(
            {
                "name": "Marker only",
                "flags": ["SetMarker"],
                "topology": None,
                "num_indices": 0,
                "active_stages": [],
            }
        )

        _assert_contract_error(self, "empty_draw_set", _document([marker]))

    def test_root_capture_and_action_shapes_are_exact(self) -> None:
        root_extra = _document()
        root_extra["fps"] = 60
        _assert_contract_error(self, "contract_shape", root_extra)

        capture_extra = _document()
        capture_extra["capture"]["adapter"] = "unexpected"
        _assert_contract_error(self, "contract_shape", capture_extra)

        action_extra = _document()
        action_extra["actions"][0]["estimated_triangles"] = 1
        _assert_contract_error(self, "contract_shape", action_extra)

    def test_schema_and_non_empty_tree_are_required(self) -> None:
        wrong_schema = _document()
        wrong_schema["schema"] = "forge-gate1-renderdoc-actions/v2"
        _assert_contract_error(self, "unsupported_schema", wrong_schema)

        _assert_contract_error(self, "empty_action_tree", _document([]))

    def test_error_payload_is_stable_and_contains_no_partial_metrics(self) -> None:
        error = _assert_contract_error(
            self,
            "pipeline_type_not_qualified",
            _document(
                capture={
                    **_PASS_CAPTURE,
                    "pipeline_type": "D3D12",
                }
            ),
        )

        payload = error.as_dict()
        self.assertEqual(OUTPUT_SCHEMA, payload["schema"])
        self.assertEqual("FAIL", payload["status"])
        self.assertNotIn("metrics", payload)
        self.assertEqual("pipeline_type_not_qualified", payload["error"]["code"])


if __name__ == "__main__":
    unittest.main()
