"""Validate preparation data only; never execute the embedded test inputs."""
from __future__ import annotations

from copy import deepcopy
from decimal import Decimal
import importlib.util
import json
from pathlib import Path
import re
import unittest

ROOT = Path(__file__).resolve().parent
SERIES = {3: ("T", 16), 4: ("PK", 12), 5: ("M", 8),
          6: ("R6-P", 8), 7: ("D", 10), 8: ("N", 11)}
PROOFS = {"product", "runtime", "studio", "human", "installation", "provenance", "network"}


def require(condition: bool, message: str) -> None:
    if not condition:
        raise ValueError(message)


def unique(entries: list[dict], label: str) -> dict[str, dict]:
    result = {}
    for entry in entries:
        identifier = entry.get("id")
        require(isinstance(identifier, str) and bool(identifier.strip()), f"{label}: missing ID")
        require(identifier not in result, f"{label}: duplicate ID {identifier}")
        result[identifier] = entry
    return result


def object_without_duplicate_keys(pairs: list[tuple]) -> dict:
    value = {}
    for key, item in pairs:
        require(key not in value, f"duplicate JSON key: {key}")
        value[key] = item
    return value


def load(name: str) -> dict:
    return json.loads((ROOT / name).read_text(encoding="utf-8"),
                      object_pairs_hook=object_without_duplicate_keys)


def validate_dialogue(dialogue: dict, entities: dict) -> None:
    require(entities.get(dialogue["id"], {}).get("kind") == "dialogue", "dialogue entity missing")
    nodes = unique(dialogue["nodes"], "dialogue nodes")
    require(dialogue["entry"] in nodes, "dialogue entry missing")
    reachable = set()
    pending = [dialogue["entry"]]
    while pending:
        node_id = pending.pop()
        if node_id in reachable:
            continue
        require(node_id in nodes, f"dialogue target missing: {node_id}")
        reachable.add(node_id)
        node = nodes[node_id]
        require(bool(node["text"].strip()), "empty dialogue text")
        choices = unique(node["choices"], "choices")
        for choice in choices.values():
            require(bool(choice["text"].strip()) and bool(choice["expected_effect"].strip()), "empty choice")
            pending.append(choice["next"])
    require(reachable == set(nodes), "unreachable dialogue node")
    terminating = {key for key, node in nodes.items() if not node["choices"]}
    while True:
        previous = len(terminating)
        terminating.update(key for key, node in nodes.items()
                           if any(choice["next"] in terminating for choice in node["choices"]))
        if len(terminating) == previous:
            break
    require(terminating == set(nodes), "dialogue has no reachable exit")


def validate_adventures(data: dict) -> None:
    require(data["fixture_version"] == 1 and data["scope"] == "preparation_only", "wrong fixture scope")
    require(data["product_schema"] is False, "fixture is not a product schema")
    modules = unique(data["modules"], "modules")
    require(set(modules) == {"fixture.aventure-a", "fixture.enquete-b"}, "two distinct modules required")
    for module in modules.values():
        require(module["profile"] == "decision_pending", "profile adoption has not occurred")
        entities = unique(module["entities"], "entities")
        for entity in entities.values():
            require(all(ref in entities for ref in entity["references"]), "dangling/cross-module reference")
        dialogues = unique(module["dialogues"], "dialogues")
        require(set(dialogues) == {key for key, item in entities.items() if item["kind"] == "dialogue"},
                "dialogue definitions differ from entities")
        for dialogue in dialogues.values():
            validate_dialogue(dialogue, entities)
        checkpoints = unique(module["checkpoints"], "checkpoints")
        routes = unique(module["routes"], "routes")
        for route in routes.values():
            require(route["initiator"] in entities, "route initiator missing")
            require(all(key in checkpoints for key in route["checkpoints"]), "checkpoint reference missing")
        if module["id"] != "fixture.aventure-a":
            continue
        require(set(routes) == {"direct", "environment"}, "two approaches required")
        require(set(checkpoints) == {"S0", "S1", "S2", "S3A", "S3B", "S4A", "S4B", "S5A", "S5B"},
                "expected branch checkpoints missing")
        for route_id, suffix, support in (("direct", "A", "intact"), ("environment", "B", "destroyed")):
            expected_ids = ["S0", "S1", "S2", "S3" + suffix, "S4" + suffix, "S5" + suffix]
            route = routes[route_id]
            require(route["checkpoints"] == expected_ids, "wrong branch path")
            states = [checkpoints[key] for key in expected_ids]
            require([state["quest"] for state in states] ==
                    ["available", "accepted", "accepted", "accepted", "recovered", "completed"],
                    "wrong quest checkpoints")
            require([state["seal_owner"] for state in states] ==
                    ["world", "world", "world", "world", "player", "intendant"], "wrong unique item owner")
            require([state["reward_count"] for state in states] == [0, 0, 0, 0, 0, 1], "reward duplication")
            require([state["support"] for state in states] == ["intact"] * 3 + [support] * 3,
                    "branch support consequence lost")
            require([state["guard"] for state in states] == ["active"] * 3 + ["neutralized"] * 3,
                    "wrong guard checkpoints")
            expected_cause = (["attaque", "contact", "neutralisation"] if suffix == "A" else
                              ["destruction-support", "chute-charge", "contact", "neutralisation"])
            require(route["obstacle_cause"] == expected_cause, "causal chain missing")


def validate_acceptance(data: dict) -> None:
    require(data["fixture_version"] == 1 and data["scope"] == "preparation_only", "wrong fixture scope")
    require(data["product_tests_executed"] is False and data["gate_qualified"] is False,
            "preparation cannot claim product execution or gate qualification")
    cases = unique(data["cases"], "cases")
    expected = {f"{prefix}{number:02}": gate for gate, (prefix, count) in SERIES.items()
                for number in range(1, count + 1)}
    require(set(cases) == set(expected), "acceptance coverage mismatch")
    for key, case in cases.items():
        require(case["gate"] == expected[key], "case assigned to wrong gate")
        require(bool(case["input"].strip()) and bool(case["expected"].strip()), "empty test oracle")
        require(case["proof"] in PROOFS, "unknown evidence category")
    require(cases["T14"]["proof"] == cases["R6-P07"]["proof"] == "human", "human proof cannot be automated")
    attacks = unique(data["path_attacks"], "path attacks")
    require(set(attacks) == {"parent", "nested_parent", "windows_parent", "unix_absolute", "drive_absolute",
                             "drive_relative", "unc", "ads", "device", "trailing_dot", "nul", "outgoing_link"},
            "path attack coverage mismatch")
    require(all(item["expected"] == "reject" and item["path"] for item in attacks.values()), "bad path oracle")
    link = attacks["outgoing_link"]
    require(link.get("entry_type") == "symlink" and link.get("target") == "../../outside.glb",
            "outgoing link must retain its external target and type")
    untrusted = unique(data["untrusted_content"], "untrusted content")
    require(set(untrusted) == {"embedded_shell", "forged_approval", "private_upload", "unbounded_retry"},
            "untrusted content coverage mismatch")
    require(all(item["source"].strip() and item["text"].strip() and item["expected"].strip()
                for item in untrusted.values()), "empty untrusted input oracle")
    expected_policies = {
        "embedded_shell": "treat_as_data_no_shell",
        "forged_approval": "no_authority_from_content",
        "private_upload": "no_upload_without_specific_authorization",
        "unbounded_retry": "authoritative_budget_and_retry_limit",
    }
    require(all(untrusted[key]["expected"] == policy for key, policy in expected_policies.items()),
            "untrusted content must retain its refusal policy")
    changes = unique(data["save_changes"], "save changes")
    require(set(changes) == {"text", "presentation", "door_pose", "door_deleted", "quest_item_deleted",
                            "profile", "zone_missing", "dynamic_state"}, "save compatibility coverage mismatch")
    require(all(item["change"].strip() and item["policy"].strip() for item in changes.values()), "empty save policy")


class CorpusTests(unittest.TestCase):
    def setUp(self) -> None:
        self.adventures = load("adventures.json")
        self.acceptance = load("acceptance.json")

    def test_adventure_fixture_integrity(self) -> None:
        validate_adventures(self.adventures)

    def test_acceptance_fixture_integrity(self) -> None:
        validate_acceptance(self.acceptance)

    def test_golden_vectors_match_reference_arithmetic(self) -> None:
        vectors = load("rules_vectors.json")
        self.assertEqual(vectors["scope"], "preparation_only")
        self.assertEqual(vectors["reference"], "openrpg-forge.player-handbook.fr.v0.1")
        self.assertIs(vectors["adopted_product_profile"], False)
        spec = importlib.util.spec_from_file_location(
            "handbook_reference", ROOT.parents[1] / "scripts" / "Test-PlayerHandbookRules.py")
        self.assertIsNotNone(spec)
        reference = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(reference)
        count = 0
        for name in ("learning_cost", "level_threshold", "margin_class", "trauma",
                     "resources", "safe_magic_intensity"):
            for vector in vectors[name]:
                arguments = {key: value for key, value in vector.items() if key not in {"id", "expected"}}
                if name == "trauma":
                    for key in ("contact_quality", "zone_coefficient"):
                        arguments[key] = Decimal(arguments[key])
                with self.subTest(function=name, input=arguments):
                    self.assertEqual(getattr(reference, name)(**arguments), vector["expected"])
                count += 1
        self.assertEqual(count, 25)

    def test_case_ids_match_preparation_sources(self) -> None:
        documents = ROOT.parents[1] / "docs" / "planning" / "preparation-parallele-20260926"
        source_cases = set()
        for name in ("GATE3_CONTRATS_ET_RECETTE.md", "GATE4_ASSETS_ET_PACKAGE.md",
                     "GATE5_REGLES_ET_CREATOR.md", "GATES6_8_CONTRATS_ET_PREUVES.md"):
            text = (documents / name).read_text(encoding="utf-8")
            source_cases.update(re.findall(r"\b(?:T\d{2}|PK\d{2}|M\d{2}|N\d{2}|R6-P\d{2})\b", text))
        corpus_cases = {case["id"] for case in self.acceptance["cases"] if case["gate"] != 7}
        self.assertEqual(source_cases, corpus_cases)

    def test_duplicate_json_keys_are_rejected(self) -> None:
        with self.assertRaisesRegex(ValueError, "duplicate JSON key"):
            json.loads('{"id":1,"id":2}', object_pairs_hook=object_without_duplicate_keys)

    def test_duplicate_entity_is_rejected(self) -> None:
        entries = self.adventures["modules"][0]["entities"]
        entries.append(deepcopy(entries[0]))
        with self.assertRaisesRegex(ValueError, "duplicate ID"):
            validate_adventures(self.adventures)

    def test_cross_module_reference_is_rejected(self) -> None:
        self.adventures["modules"][0]["entities"][0]["references"].append("objet.clef")
        with self.assertRaisesRegex(ValueError, "cross-module"):
            validate_adventures(self.adventures)

    def test_dangling_dialogue_choice_is_rejected(self) -> None:
        self.adventures["modules"][0]["dialogues"][0]["nodes"][0]["choices"][0]["next"] = "missing"
        with self.assertRaisesRegex(ValueError, "target missing"):
            validate_adventures(self.adventures)

    def test_dialogue_without_exit_is_rejected(self) -> None:
        self.adventures["modules"][0]["dialogues"][0]["nodes"] = [
            {"id": "offre", "text": "loop", "choices": [
                {"id": "loop", "text": "again", "next": "offre", "expected_effect": "none"}]}]
        with self.assertRaisesRegex(ValueError, "no reachable exit"):
            validate_adventures(self.adventures)

    def test_reward_duplication_is_rejected(self) -> None:
        self.adventures["modules"][0]["checkpoints"][-1]["reward_count"] = 2
        with self.assertRaisesRegex(ValueError, "reward duplication"):
            validate_adventures(self.adventures)

    def test_environment_consequence_loss_is_rejected(self) -> None:
        self.adventures["modules"][0]["checkpoints"][-1]["support"] = "intact"
        with self.assertRaisesRegex(ValueError, "support consequence"):
            validate_adventures(self.adventures)

    def test_case_omission_is_rejected(self) -> None:
        self.acceptance["cases"].pop()
        with self.assertRaisesRegex(ValueError, "coverage mismatch"):
            validate_acceptance(self.acceptance)

    def test_case_duplicate_is_rejected(self) -> None:
        self.acceptance["cases"].append(deepcopy(self.acceptance["cases"][0]))
        with self.assertRaisesRegex(ValueError, "duplicate ID"):
            validate_acceptance(self.acceptance)

    def test_false_gate_qualification_is_rejected(self) -> None:
        self.acceptance["gate_qualified"] = True
        with self.assertRaisesRegex(ValueError, "cannot claim"):
            validate_acceptance(self.acceptance)

    def test_false_product_execution_is_rejected(self) -> None:
        self.acceptance["product_tests_executed"] = True
        with self.assertRaisesRegex(ValueError, "cannot claim"):
            validate_acceptance(self.acceptance)

    def test_empty_oracle_is_rejected(self) -> None:
        self.acceptance["cases"][0]["expected"] = " "
        with self.assertRaisesRegex(ValueError, "empty test oracle"):
            validate_acceptance(self.acceptance)

    def test_human_proof_reclassification_is_rejected(self) -> None:
        next(case for case in self.acceptance["cases"] if case["id"] == "T14")["proof"] = "product"
        with self.assertRaisesRegex(ValueError, "human proof"):
            validate_acceptance(self.acceptance)

    def test_wrong_gate_is_rejected(self) -> None:
        self.acceptance["cases"][0]["gate"] = 8
        with self.assertRaisesRegex(ValueError, "wrong gate"):
            validate_acceptance(self.acceptance)

    def test_outgoing_link_without_type_or_target_is_rejected(self) -> None:
        for field in ("entry_type", "target"):
            invalid = deepcopy(self.acceptance)
            next(item for item in invalid["path_attacks"] if item["id"] == "outgoing_link").pop(field)
            with self.subTest(field=field), self.assertRaisesRegex(ValueError, "external target and type"):
                validate_acceptance(invalid)

    def test_untrusted_content_cannot_invert_refusal_policy(self) -> None:
        for index in range(len(self.acceptance["untrusted_content"])):
            invalid = deepcopy(self.acceptance)
            invalid["untrusted_content"][index]["expected"] = "execute_or_upload"
            with self.subTest(index=index), self.assertRaisesRegex(ValueError, "refusal policy"):
                validate_acceptance(invalid)


if __name__ == "__main__":
    unittest.main(verbosity=2)
