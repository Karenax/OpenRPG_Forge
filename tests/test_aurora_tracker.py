from __future__ import annotations

import csv
import hashlib
import re
import shutil
import subprocess
import unittest
from collections import Counter
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SNAPSHOT = ROOT / "Divers" / "OpenRPG_Forge_Aurora_Feature_Dossier"
PROJECT_TRACKER = ROOT / "tracking" / "project" / "AURORA_PARITY_COVERAGE.csv"
GATE_MATRIX = ROOT / "tracking" / "project" / "AURORA_GATE_MATRIX.csv"
CORE_AUDIT = ROOT / "tracking" / "project" / "AURORA_CORE_P0_AUDIT.csv"
FEATURE_AUDIT = ROOT / "tracking" / "project" / "AURORA_FEATURE_IMPLEMENTATION_AUDIT.csv"


def read_csv(path: Path) -> list[dict[str, str]]:
    with path.open(encoding="utf-8-sig", newline="") as stream:
        return list(csv.DictReader(stream, delimiter=";"))


class AuroraParityTrackerTests(unittest.TestCase):
    def test_snapshot_manifest_is_complete_and_untouched(self) -> None:
        manifest = SNAPSHOT / "MANIFEST_SHA256.txt"
        entries = []
        for line in manifest.read_text(encoding="utf-8").splitlines():
            match = re.fullmatch(r"([0-9a-f]{64})\s+\*?(.+)", line)
            self.assertIsNotNone(match, line)
            assert match is not None
            entries.append((match.group(1), match.group(2)))

        self.assertEqual(74, len(entries))
        for expected, relative in entries:
            source = SNAPSHOT / Path(relative)
            self.assertTrue(source.is_file(), relative)
            self.assertEqual(expected, hashlib.sha256(source.read_bytes()).hexdigest(), relative)

    def test_project_tracker_covers_catalog_and_has_test_evidence(self) -> None:
        catalog = read_csv(SNAPSHOT / "feature_catalog.csv")
        tracker = read_csv(PROJECT_TRACKER)
        self.assertEqual(717, len(catalog))
        self.assertEqual([row["id"] for row in catalog], [row["feature_id"] for row in tracker])
        self.assertEqual(717, len({row["feature_id"] for row in tracker}))

        allowed = {
            "NotStarted",
            "Researched",
            "Designed",
            "InProgress",
            "Implemented",
            "Tested",
            "Deferred",
            "Rejected",
        }
        self.assertFalse({row["status"] for row in tracker} - allowed)
        for row in tracker:
            self.assertTrue(row["design_evidence"], row["feature_id"])
            self.assertTrue(row["target_epic"], row["feature_id"])
            if row["status"] == "Tested":
                self.assertTrue(row["test_evidence"], row["feature_id"])

    def test_core_p0_audit_is_complete_and_conservative(self) -> None:
        catalog = {row["id"]: row for row in read_csv(SNAPSHOT / "feature_catalog.csv")}
        audit = read_csv(CORE_AUDIT)
        self.assertEqual(76, len(audit))
        self.assertEqual(76, len({row["feature_id"] for row in audit}))
        self.assertTrue(all(None not in row for row in audit), "Core audit rows must have exactly five fields")

        required_domains = {"SHELL", "PROJECT", "AREA", "PALETTE", "BUILD", "QA"}
        expected_ids = {
            feature_id
            for feature_id, feature in catalog.items()
            if feature["priority"] == "P0" and feature["domain"] in required_domains
        }
        self.assertEqual(expected_ids, {row["feature_id"] for row in audit})
        self.assertEqual(
            Counter({"Tested": 76}),
            Counter(row["status"] for row in audit),
        )
        self.assertEqual(
            {
                "ORF-BUILD-001",
                "ORF-BUILD-002",
                "ORF-BUILD-003",
                "ORF-BUILD-004",
                "ORF-BUILD-005",
                "ORF-BUILD-006",
                "ORF-BUILD-008",
                "ORF-BUILD-009",
                "ORF-BUILD-010",
                "ORF-BUILD-011",
                "ORF-BUILD-013",
                "ORF-BUILD-014",
                "ORF-BUILD-015",
                "ORF-BUILD-018",
                "ORF-BUILD-019",
                "ORF-BUILD-022",
                "ORF-BUILD-023",
                "ORF-AREA-001",
                "ORF-AREA-002",
                "ORF-AREA-003",
                "ORF-AREA-008",
                "ORF-AREA-022",
                "ORF-AREA-030",
                "ORF-AREA-031",
                "ORF-AREA-036",
                "ORF-AREA-037",
                "ORF-PAL-001",
                "ORF-PAL-002",
                "ORF-PAL-003",
                "ORF-PAL-006",
                "ORF-PAL-007",
                "ORF-PAL-012",
                "ORF-PAL-015",
                "ORF-PAL-016",
                "ORF-PROJ-001",
                "ORF-PROJ-002",
                "ORF-PROJ-003",
                "ORF-PROJ-004",
                "ORF-PROJ-005",
                "ORF-PROJ-006",
                "ORF-PROJ-019",
                "ORF-PROJ-020",
                "ORF-PROJ-021",
                "ORF-PROJ-024",
                "ORF-PROJ-025",
                "ORF-SHELL-001",
                "ORF-SHELL-002",
                "ORF-SHELL-003",
                "ORF-SHELL-004",
                "ORF-SHELL-005",
                "ORF-SHELL-006",
                "ORF-SHELL-007",
                "ORF-SHELL-010",
                "ORF-SHELL-011",
                "ORF-SHELL-013",
                "ORF-SHELL-014",
                "ORF-SHELL-015",
                "ORF-SHELL-016",
                "ORF-SHELL-017",
                "ORF-SHELL-018",
                "ORF-SHELL-019",
                "ORF-SHELL-020",
                "ORF-QA-001",
                "ORF-QA-002",
                "ORF-QA-003",
                "ORF-QA-004",
                "ORF-QA-005",
                "ORF-QA-007",
                "ORF-QA-008",
                "ORF-QA-010",
                "ORF-QA-015",
                "ORF-QA-020",
                "ORF-QA-021",
                "ORF-QA-026",
                "ORF-QA-029",
                "ORF-QA-030",
            },
            {row["feature_id"] for row in audit if row["status"] == "Tested"},
        )
        for row in audit:
            if row["status"] == "Implemented":
                self.assertTrue(row["notes"].startswith("Partial:"), row["feature_id"])

    def test_feature_implementation_audit_is_unique_and_evidenced(self) -> None:
        catalog = {row["id"]: row for row in read_csv(SNAPSHOT / "feature_catalog.csv")}
        core_ids = {row["feature_id"] for row in read_csv(CORE_AUDIT)}
        audit = read_csv(FEATURE_AUDIT)
        self.assertEqual(len(audit), len({row["feature_id"] for row in audit}))
        self.assertTrue(all(None not in row for row in audit), "Feature audit rows must have exactly five fields")
        self.assertFalse(core_ids & {row["feature_id"] for row in audit})
        for row in audit:
            self.assertIn(row["feature_id"], catalog)
            self.assertIn(row["status"], {"NotStarted", "Designed", "Implemented", "Tested"})
            if row["status"] == "Implemented":
                self.assertTrue(row["implementation_evidence"], row["feature_id"])
                self.assertTrue(row["test_evidence"], row["feature_id"])
                self.assertTrue(row["notes"].startswith("Partial:"), row["feature_id"])

    def test_gate_matrix_is_total_and_generated_files_are_fresh(self) -> None:
        tracker = read_csv(PROJECT_TRACKER)
        gates = read_csv(GATE_MATRIX)
        self.assertEqual(717, len(gates))
        self.assertEqual(
            [row["feature_id"] for row in tracker],
            [row["feature_id"] for row in gates],
        )
        allowed_gates = {"G0", "G1", "G2", "G3", "G4", "G5", "G7", "G8", "G9", "G10", "G11", "G12", "G13"}
        self.assertFalse({row["target_gate"] for row in gates} - allowed_gates)
        self.assertTrue(all(row["target_milestone"] for row in gates))

        shell = shutil.which("pwsh") or shutil.which("powershell")
        self.assertIsNotNone(shell, "PowerShell is required to verify the generated tracker")
        result = subprocess.run(
            [
                str(shell),
                "-NoProfile",
                "-File",
                str(ROOT / "scripts" / "Update-AuroraParityTracker.ps1"),
                "-Check",
            ],
            cwd=ROOT,
            check=False,
            capture_output=True,
            text=True,
            encoding="utf-8",
        )
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        self.assertIn("OPENRPG_FORGE_AURORA_TRACKER_FRESH=PASS", result.stdout)


if __name__ == "__main__":
    unittest.main()
