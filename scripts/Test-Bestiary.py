#!/usr/bin/env python3
"""Validate the bestiary sources, PDF contract, assets and viewer."""

from __future__ import annotations

import argparse
import copy
import hashlib
import json
import re
import shutil
import subprocess
import sys
from pathlib import Path
from typing import Any, Callable

try:
    from pypdf import PdfReader
except ImportError:  # Poppler fallback keeps the verifier usable with stock Python.
    PdfReader = None  # type: ignore[assignment,misc]


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "docs" / "design" / "bestiary"
ASSETS = ROOT / "docs" / "design" / "assets" / "bestiary"
PDF = ROOT / "output" / "pdf" / "Bestiaire_OpenRPG_Forge.pdf"
VIEWER = ROOT / "output" / "bestiary" / "interactive"
REPORT = ROOT / "output" / "bestiary" / "build-report.json"
PROVENANCE = ASSETS / "asset-provenance.json"
RULESET_SOURCE = ROOT / "docs" / "design" / "OPENRPG_FORGE_RULESET_FR.md"
HANDBOOK_SOURCE = ROOT / "docs" / "design" / "OPENRPG_FORGE_PLAYER_HANDBOOK_FR.md"
DESIGN_SYSTEM_SOURCE = ROOT / "docs" / "design" / "DESIGN_SYSTEME.md"
FRONT_MATTER_PAGES = 3
BACK_MATTER_PAGES = 3
HISTORICAL_ANCHORS = {
    "creature.brumaille",
    "creature.rodeur-corrompu",
    "creature.sentinelle-de-cendre",
    "creature.gardien-des-cendres",
    "creature.gardien-des-racines",
}

CANONICAL_SKILLS = {
    "athletics", "acrobatics", "climbing", "swimming", "survival", "stealth",
    "blades", "impact_weapons", "polearms", "marksmanship", "throwing", "unarmed",
    "shields", "parry", "armor", "tactics", "crafting", "smithing", "mechanisms",
    "alchemy", "traps", "medicine", "lore", "investigation", "persuasion",
    "intimidation", "deception", "empathy", "leadership", "trade", "vigilance",
    "tracking", "lockpicking", "sleight_of_hand", "animal_handling", "elementalism",
    "force_magic", "alteration", "illusion", "spirit", "vitality_magic", "summoning",
}
CANONICAL_ATTRIBUTES = {
    "strength", "dexterity", "constitution", "intelligence", "willpower", "perception", "presence"
}
DIFFICULTIES = {14, 18, 22, 26, 30, 34}


def load_json(path: Path) -> Any:
    with path.open("r", encoding="utf-8") as handle:
        return json.load(handle)


def digest(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as handle:
        for block in iter(lambda: handle.read(1024 * 1024), b""):
            h.update(block)
    return h.hexdigest().upper()


def load_bundle() -> tuple[dict[str, Any], list[dict[str, Any]]]:
    manifest = load_json(SOURCE / "bestiary.json")
    return manifest, [load_json(SOURCE / relative) for relative in manifest["entries"]]


def resources(attributes: dict[str, int]) -> dict[str, int]:
    return {
        "vitality": 60 + 6 * attributes["constitution"] + 2 * attributes["strength"],
        "stamina": 50 + 5 * attributes["constitution"] + 3 * attributes["willpower"],
        "mana": 20 + 6 * attributes["willpower"] + 4 * attributes["intelligence"],
        "balance": 40 + 4 * attributes["strength"] + 4 * attributes["constitution"],
        "focus": 40 + 4 * attributes["perception"] + 4 * attributes["willpower"],
    }


def expected_pdf_pages(entries: list[dict[str, Any]]) -> int:
    entry_pages = sum(4 if entry["major"] else 2 for entry in entries)
    return FRONT_MATTER_PAGES + entry_pages + BACK_MATTER_PAGES


def test_schema(manifest: dict[str, Any], entries: list[dict[str, Any]]) -> None:
    try:
        import jsonschema
    except ImportError:
        powershell = shutil.which("pwsh") or shutil.which("powershell")
        assert powershell, "Neither jsonschema nor PowerShell Test-Json is available"
        schema_path = str(SOURCE / "entry.schema.json").replace("'", "''")
        legacy_path = str(SOURCE / "entries/01-loup-de-bruyere.json").replace("'", "''")
        entry_paths = [str(SOURCE / relative).replace("'", "''") for relative in manifest["entries"]]
        quoted = ",".join(f"'{path}'" for path in entry_paths)
        script = (
            "$ErrorActionPreference='Stop'; "
            f"$schema=Get-Content -Raw -LiteralPath '{schema_path}'; "
            f"$paths=@({quoted}); "
            "foreach($path in $paths){$ok=Get-Content -Raw -LiteralPath $path | Test-Json -Schema $schema; "
            "if(-not $ok){throw \"Schema validation failed: $path\"}}; "
            # The planned-art exception must never silently weaken old entries.
            f"$legacy=Get-Content -Raw -LiteralPath '{legacy_path}' | ConvertFrom-Json -AsHashtable; "
            "$plate=$legacy.plates[0]; $legacy.plates=@(); "
            "if(($legacy | ConvertTo-Json -Depth 30 | Test-Json -Schema $schema -ErrorAction SilentlyContinue)){throw 'Empty legacy plates accepted'}; "
            "$legacy.illustration_status='illustrated'; "
            "if(($legacy | ConvertTo-Json -Depth 30 | Test-Json -Schema $schema -ErrorAction SilentlyContinue)){throw 'Empty illustrated plates accepted'}; "
            "$legacy.illustration_status='planned'; $legacy.major=$false; "
            "if(-not ($legacy | ConvertTo-Json -Depth 30 | Test-Json -Schema $schema)){throw 'Planned source rejected'}; "
            "$legacy.plates=@($plate); "
            "if(($legacy | ConvertTo-Json -Depth 30 | Test-Json -Schema $schema -ErrorAction SilentlyContinue)){throw 'Planned source with borrowed plate accepted'}; "
            "$legacy.plates=@(); $legacy.major=$true; "
            "if(($legacy | ConvertTo-Json -Depth 30 | Test-Json -Schema $schema -ErrorAction SilentlyContinue)){throw 'Planned major accepted'}"
        )
        result = subprocess.run(
            [powershell, "-NoProfile", "-NonInteractive", "-Command", script],
            capture_output=True,
            text=True,
            encoding="utf-8",
            errors="replace",
            check=False,
        )
        assert result.returncode == 0, (result.stderr or result.stdout).strip()
    else:
        schema = load_json(SOURCE / "entry.schema.json")
        validator = jsonschema.Draft202012Validator(schema)
        errors = []
        for relative, entry in zip(manifest["entries"], entries, strict=True):
            for error in validator.iter_errors(entry):
                location = ".".join(str(part) for part in error.absolute_path)
                errors.append(f"{relative}:{location}: {error.message}")
        assert not errors, "\n".join(errors)
        fixture = copy.deepcopy(load_json(SOURCE / "entries/01-loup-de-bruyere.json"))
        plate = fixture["plates"][0]
        fixture["plates"] = []
        assert not validator.is_valid(fixture)
        fixture["illustration_status"] = "illustrated"
        assert not validator.is_valid(fixture)
        fixture.update(illustration_status="planned", major=False)
        assert validator.is_valid(fixture)
        fixture["plates"] = [plate]
        assert not validator.is_valid(fixture)
        fixture.update(plates=[], major=True)
        assert not validator.is_valid(fixture)


def test_roster(manifest: dict[str, Any], entries: list[dict[str, Any]]) -> None:
    assert manifest["bestiary_id"] == "openrpg-forge.bestiary.fr.v0.3"
    assert manifest["ruleset"]["id"] == "openrpg-forge.systemic-rpg.v1"
    assert manifest["player_handbook"]["id"] == "openrpg-forge.player-handbook.fr.v0.1"
    assert manifest["status"] == "design-proposal-not-implemented"
    assert manifest["ruleset"]["sha256"] == digest(RULESET_SOURCE)
    assert manifest["player_handbook"]["sha256"] == digest(HANDBOOK_SOURCE)
    assert manifest["design_system"]["sha256"] == digest(DESIGN_SYSTEM_SOURCE)
    assert len(entries) == 51
    assert len(manifest["entries"]) == len(set(manifest["entries"]))
    ids = [entry["creature_id"] for entry in entries]
    assert len(ids) == len(set(ids))
    assert len({entry["name"] for entry in entries}) == len(entries)
    assert HISTORICAL_ANCHORS.issubset(ids)

    wave = manifest["release_wave"]
    assert wave["id"] == "humanoid-families.v0.2"
    assert wave["status"] == manifest["status"]
    wave_ids = set(wave["entry_ids"])
    assert len(wave_ids) == 9 and wave_ids.issubset(ids)
    assert set(wave["families"]) == {"gobelin", "gnoll", "orc"}
    family_ids: set[str] = set()
    entries_by_id = {entry["creature_id"]: entry for entry in entries}
    for family in wave["families"].values():
        members = set(family["entry_ids"])
        assert len(members) == 3
        assert not family_ids.intersection(members)
        family_ids.update(members)
        for creature_id in members:
            entry = entries_by_id[creature_id]
            assert entry["classification"]["family"] == family["label"]
            assert "humanoïde sapient" in entry["classification"]["nature"].casefold()
            assert entry["canonicality"] == "new-design-proposal"
            assert len(entry["encounter_use"]["noncombat_options"]) >= 2
            salvage = " ".join(entry["salvage"]).casefold()
            if salvage:
                assert any(term in salvage for term in ("abandonn", "céd", "donn", "remplac"))
                assert any(term in salvage for term in ("uniquement", "jamais", "butin", "troph"))
    assert family_ids == wave_ids
    wave_major_ids = set(wave["major_entry_ids"])
    assert len(wave_major_ids) == 3
    for family in wave["families"].values():
        assert len(set(family["entry_ids"]) & wave_major_ids) == 1
    assert {entry["creature_id"] for entry in entries if entry["major"]} == {
        "creature.gardien-des-cendres",
        "creature.gardien-des-racines",
        *wave_major_ids,
    }


def test_rules(entries: list[dict[str, Any]]) -> None:
    for entry in entries:
        attrs = entry["rules"]["attributes"]
        assert set(attrs) == CANONICAL_ATTRIBUTES
        assert all(1 <= value <= 10 for value in attrs.values())
        assert entry["rules"]["resources"] == resources(attrs), entry["creature_id"]
        assert set(entry["rules"]["skills"]).issubset(CANONICAL_SKILLS), entry["creature_id"]
        assert entry["rules"]["vulnerabilities"], entry["creature_id"]
        assert len(entry["rules"]["environmental_interactions"]) >= 2
        for action in entry["rules"]["attacks"] + entry["rules"]["abilities"]:
            assert action["skill_id"] in CANONICAL_SKILLS
            assert action["attribute_id"] in CANONICAL_ATTRIBUTES
            assert action["telegraph"].strip() and action["counterplay"].strip()
            assert 0 <= action["stamina_cost"] <= 100
            assert 0 <= action["impact"] <= 100
        assert {fact["status"] for fact in entry["field_knowledge"]} >= {"etabli", "suppose", "secret"}
        assert all(fact["difficulty"] in DIFFICULTIES for fact in entry["field_knowledge"])
        assert all(fact["skill_id"] in {"lore", "tracking", "investigation", "vigilance", "animal_handling"} for fact in entry["field_knowledge"])
        if entry.get("illustration_status") == "planned":
            assert not entry["major"] and not entry["plates"]
            assert "major_dossier" not in entry
        elif entry["major"]:
            assert "major_dossier" in entry
            assert len(entry["plates"]) == 2
        else:
            assert "major_dossier" not in entry
            assert len(entry["plates"]) == 1


def test_expansion(manifest: dict[str, Any], entries: list[dict[str, Any]]) -> None:
    wave = manifest["expansion_wave"]
    assert wave["id"] == "wild-realms.v0.3"
    assert wave["status"] == "design-proposal-not-implemented"
    assert len(wave["entry_ids"]) == len(set(wave["entry_ids"])) == 30
    assert [entry["creature_id"] for entry in entries[21:]] == wave["entry_ids"]
    assert (SOURCE / wave["research_document"]).is_file()
    assert len(wave["sources"]) == 2
    pins = wave["preserved_entries"]
    assert len(pins) == 21
    assert [pin["path"] for pin in pins] == manifest["entries"][:21]
    for pin in pins:
        assert digest(SOURCE / pin["path"]) == pin["sha256"], pin["path"]
    illustrated = set(wave["entry_ids"])
    assert len(illustrated) == 30
    assert wave["illustration_status"] == "complete"
    assert len(wave["illustrated_entry_ids"]) == len(illustrated)
    assert set(wave["illustrated_entry_ids"]) == illustrated
    assert len(entries) == 51 and all(entry["plates"] for entry in entries)
    assert sum(len(entry["plates"]) for entry in entries) == 56
    for entry in entries[21:]:
        assert entry["creature_id"] in illustrated
        assert entry["illustration_status"] == "illustrated" and len(entry["plates"]) == 1
        assert entry["plates"][0]["image"] == entry["creature_id"].removeprefix("creature.") + "-plate-v1.png"
        assert entry["canonicality"] == "new-design-proposal"
        assert len(entry["encounter_use"]["noncombat_options"]) >= 2
        assert len(entry["rules"]["attacks"]) >= 2
        assert len(entry["rules"]["abilities"]) >= 1
        for action in entry["rules"]["attacks"] + entry["rules"]["abilities"]:
            assert action["skill_id"] in entry["rules"]["skills"], (entry["name"], action["name"])


def test_spreads(entries: list[dict[str, Any]]) -> None:
    page = 4
    for entry in entries:
        assert page % 2 == 0, f"{entry['creature_id']} visual starts on page {page}"
        assert (page + 1) % 2 == 1
        expected = 2 if entry["major"] else 1
        assert len(entry["plates"]) == expected
        if entry["major"]:
            assert "major_dossier" in entry
            assert (page + 2) % 2 == 0
            page += 4
        else:
            page += 2
    assert page == expected_pdf_pages(entries) - 2


def test_assets(entries: list[dict[str, Any]]) -> None:
    provenance = load_json(PROVENANCE)
    records = {}
    for relative in provenance["record_files"]:
        fragment = load_json(ASSETS / relative)
        for record in fragment["assets"]:
            assert record["file"] not in records, record["file"]
            records[record["file"]] = record
    expected = {"cover.png"}
    prompt_keys: set[str] = set()
    for entry in entries:
        for plate in entry["plates"]:
            expected.add(plate["image"])
            assert len(plate["alt"]) >= 40
            assert plate["prompt_key"] not in prompt_keys
            prompt_keys.add(plate["prompt_key"])
    assert expected == set(records), (expected - set(records), set(records) - expected)
    for filename in expected:
        path = ASSETS / filename
        assert path.is_file(), path
        record = records[filename]
        assert record["sha256"] == digest(path)
        assert record["license"] == "LicenseRef-OpenAI-Generated-Output"
        assert record["prompt"].strip()
        assert record["alt"].strip()
        if record.get("prompt_file"):
            assert (ASSETS / record["prompt_file"]).read_text(encoding="utf-8").strip() == record["prompt"]
            raw = path.read_bytes()
            assert raw[:8] == b"\x89PNG\r\n\x1a\n"
            assert int.from_bytes(raw[16:20], "big") == record["dimensions"]["width"] == 1024
            assert int.from_bytes(raw[20:24], "big") == record["dimensions"]["height"] == 1536
            entry = load_json(ROOT / record["source_entry"])
            assert any(plate["image"] == filename and plate["alt"] == record["alt"] for plate in entry["plates"])
            content = {key: value for key, value in entry.items() if key not in {"plates", "illustration_status"}}
            canonical = json.dumps(content, ensure_ascii=False, sort_keys=True, separators=(",", ":"))
            assert hashlib.sha256(canonical.encode("utf-8")).hexdigest().upper() == record["source_content_sha256"]
            assert record["review"]["authority"] == "agent_review"
            assert record["review"]["human_review"] == "pending"
            assert record["runtime_qualified"] is False


def test_pdf(entries: list[dict[str, Any]]) -> None:
    assert PDF.is_file(), PDF
    if PdfReader is not None:
        reader = PdfReader(str(PDF))
        page_count = len(reader.pages)
        extracted = "\n".join(page.extract_text() or "" for page in reader.pages)
        page_sizes = [
            (float(page.mediabox.width), float(page.mediabox.height))
            for page in reader.pages
        ]
        titles: set[str] | None = None
    else:
        # ReportLab keeps page objects, media boxes, metadata and outline titles
        # uncompressed. Reading those structures is enough for a dependency-free
        # integrity check; the pypdf path above additionally checks page text.
        raw_pdf = PDF.read_bytes()
        page_count = len(re.findall(rb"/Type\s*/Page\b", raw_pdf))
        media_boxes = re.findall(
            rb"/MediaBox\s*\[\s*([\d.]+)\s+([\d.]+)\s+([\d.]+)\s+([\d.]+)\s*\]",
            raw_pdf,
        )
        page_sizes = [
            (float(x2) - float(x1), float(y2) - float(y1))
            for x1, y1, x2, y2 in media_boxes
        ]

        def decode_literal(value: bytes) -> str:
            value = re.sub(
                rb"\\([0-7]{1,3})",
                lambda match: bytes([int(match.group(1), 8)]),
                value,
            )
            value = value.replace(rb"\(", b"(").replace(rb"\)", b")")
            return value.decode("latin-1", errors="replace")

        titles = {
            decode_literal(match)
            for match in re.findall(rb"/Title\s*\(((?:\\.|[^\\)])*)\)", raw_pdf)
        }
        subject_match = re.search(rb"/Subject\s*\(((?:\\.|[^\\)])*)\)", raw_pdf)
        assert subject_match, "PDF subject metadata missing"
        assert "proposition non implémentée" in decode_literal(subject_match.group(1)).lower()
        extracted = None
    assert page_count == expected_pdf_pages(entries)
    for entry in entries:
        if extracted is not None:
            assert entry["name"].upper() in extracted.upper(), entry["name"]
        else:
            assert titles is not None and entry["name"] in titles, entry["name"]
    if extracted is not None:
        assert "RUNTIME RUST NON IMPLÉMENTÉ" in extracted.upper()
        assert "OPENRPG-FORGE.SYSTEMIC-RPG.V1" in extracted.upper()
    assert len(page_sizes) == page_count
    for width, height in page_sizes:
        assert 594 <= width <= 596 and 841 <= height <= 843
        assert height > width


def test_viewer(entries: list[dict[str, Any]]) -> None:
    for filename in ("index.html", "styles.css", "app.js", "data.js", "bestiary.json"):
        assert (VIEWER / filename).is_file(), filename
    data = load_json(VIEWER / "bestiary.json")
    assert [entry["creature_id"] for entry in data["entries"]] == [entry["creature_id"] for entry in entries]
    html_text = (VIEWER / "index.html").read_text(encoding="utf-8")
    js_text = (VIEWER / "app.js").read_text(encoding="utf-8")
    css_text = (VIEWER / "styles.css").read_text(encoding="utf-8")
    assert "type=\"search\"" in html_text
    assert "id=\"family\"" in html_text
    assert "Toutes les familles" in html_text
    assert "e.classification.family === family.value" in js_text
    assert "tokens.some(token => token.startsWith(needle))" in js_text
    assert "Révéler le dossier complet" in js_text
    assert "entryNode.focus()" not in js_text
    assert "e.plates.map" in js_text
    assert "object-fit: contain" in css_text
    assert "object-fit: cover" not in css_text
    assert ".status { display: none" not in css_text
    assert "Gates 3 et 5" not in (ROOT / "scripts" / "Build-Bestiary.py").read_text(encoding="utf-8")


def test_report(entries: list[dict[str, Any]]) -> None:
    report = load_json(REPORT)
    assert report["entry_count"] == len(entries)
    assert report["major_entry_count"] == sum(1 for entry in entries if entry["major"])
    assert report["plate_count"] == sum(len(entry["plates"]) for entry in entries)
    assert report["pdf"]["pages"] == expected_pdf_pages(entries)
    assert report["pdf"]["sha256"] == digest(PDF)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source-only", action="store_true", help="Validate canonical sources and existing art, not PDF/viewer outputs.")
    args = parser.parse_args()
    manifest, entries = load_bundle()
    tests: list[tuple[str, Callable[[], None]]] = [
        ("schéma JSON", lambda: test_schema(manifest, entries)),
        ("roster et autorités", lambda: test_roster(manifest, entries)),
        ("règles communes", lambda: test_rules(entries)),
        ("extension et préservation des 21 originaux", lambda: test_expansion(manifest, entries)),
        ("illustrations et provenance", lambda: test_assets(entries)),
    ]
    if args.source_only:
        print("SOURCES SEULES — PDF et ancienne visionneuse non qualifiés par cette commande.")
    else:
        tests += [
        ("parité des doubles pages", lambda: test_spreads(entries)),
        ("PDF A4 et contenu", lambda: test_pdf(entries)),
        ("visionneuse autonome", lambda: test_viewer(entries)),
        ("rapport de build", lambda: test_report(entries)),
        ]
    failures = 0
    for name, test in tests:
        try:
            test()
            print(f"PASS — {name}")
        except Exception as exc:  # noqa: BLE001 - test runner reports all failures
            failures += 1
            print(f"FAIL — {name}: {exc}", file=sys.stderr)
    print(f"{len(tests) - failures}/{len(tests)} contrôles passent")
    return 1 if failures else 0


if __name__ == "__main__":
    raise SystemExit(main())
