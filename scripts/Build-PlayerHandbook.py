#!/usr/bin/env python3
"""Build the OpenRPG Forge French player handbook from its Markdown parts.

The Markdown source is canonical. The DOCX is a generated reading artifact.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import re
from datetime import date
from pathlib import Path

from docx import Document
from docx.enum.section import WD_ORIENT, WD_SECTION
from docx.enum.style import WD_STYLE_TYPE
from docx.enum.table import WD_CELL_VERTICAL_ALIGNMENT, WD_TABLE_ALIGNMENT
from docx.enum.text import WD_ALIGN_PARAGRAPH, WD_BREAK, WD_LINE_SPACING
from docx.oxml import OxmlElement
from docx.oxml.ns import qn
from docx.shared import Inches, Mm, Pt, RGBColor


ROOT = Path(__file__).resolve().parents[1]
PARTS_DIR = ROOT / "docs" / "design" / "player-handbook" / "parts"
ASSETS_DIR = ROOT / "docs" / "design" / "assets" / "player-handbook"
CANONICAL_MD = ROOT / "docs" / "design" / "OPENRPG_FORGE_PLAYER_HANDBOOK_FR.md"
DEFAULT_OUTPUT = ROOT / "output" / "player-handbook"

GREEN = "274C3B"
GREEN_DARK = "173428"
BRONZE = "8A572A"
PARCHMENT = "F5EEDC"
PARCHMENT_LIGHT = "FBF8F0"
SLATE = "496573"
CHARCOAL = "2D2D2A"
WHITE = "FFFFFF"
GRAY = "E8E4DA"
BLUE = "2E6F9E"
EXPECTED_ILLUSTRATION_COUNT = 11


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


def part_files() -> list[Path]:
    files = sorted(PARTS_DIR.glob("[0-9][0-9]_*.md"))
    expected = [f"{index:02d}" for index in range(1, 7)]
    found = [path.name[:2] for path in files]
    if found != expected:
        raise RuntimeError(f"Expected handbook parts {expected}, found {found}")
    return files


def without_marker(text: str) -> str:
    return re.sub(r"^<!-- handbook-part: [a-z-]+ -->\s*", "", text, count=1)


def assemble_markdown(files: list[Path]) -> str:
    preface = """# Manuel du joueur — OpenRPG Forge

## Devenez ce que vous faites

**Version de conception 0.1 — 30 août 2026**  
Ruleset parent : `openrpg-forge.systemic-rpg.v1`  
Statut : proposition normative ; runtime Rust non implémenté et non qualifié.

Ce manuel est une création originale en clean-room. Il sert à la fois de guide de jeu et de base de
travail pour les futurs contrats, données, prototypes et tests du moteur.

![Une forteresse claire, un village et des voyageurs occupés à négocier, étudier, travailler et s’entraîner.](assets/player-handbook/bright-fortress-life.png)

---
"""
    bodies: list[str] = []
    for path in files:
        text = without_marker(path.read_text(encoding="utf-8")).strip()
        text = text.replace("../../assets/player-handbook/", "assets/player-handbook/")
        bodies.append(text)
    return preface + "\n\n" + "\n\n---\n\n".join(bodies) + "\n"


def validate_sources(files: list[Path]) -> dict[str, object]:
    texts = {path.name: path.read_text(encoding="utf-8") for path in files}
    all_text = "\n".join(texts.values())

    for path, text in texts.items():
        markers = re.findall(r"^<!-- handbook-part: ([a-z-]+) -->$", text, re.MULTILINE)
        if len(markers) != 1:
            raise RuntimeError(f"{path} must contain exactly one handbook-part marker")

        fences = [line.strip() for line in text.splitlines() if line.strip().startswith(("```", "~~~"))]
        if len(fences) % 2:
            raise RuntimeError(f"Unbalanced code fences in {path}")

    forbidden = ["Physique représentée", "Échec de 8 ou plus", "Réussite de 0 à 3"]
    for term in forbidden:
        if term in all_text:
            raise RuntimeError(f"Forbidden or stale term found: {term}")

    progression = texts["02_progression_skills.md"]
    skills = [int(value) for value in re.findall(r"^####\s+(\d+)\.\s+.+?\(`[^`]+`\)", progression, re.MULTILINE)]
    if skills != list(range(1, 43)):
        raise RuntimeError(f"Skill sequence is invalid: {skills}")

    acquisitions = re.findall(r"^- \*\*(10|25|45|65|85) —", progression, re.MULTILINE)
    if len(acquisitions) != 210:
        raise RuntimeError(f"Expected 210 automatic acquisitions, found {len(acquisitions)}")

    talents_text = texts["03_talents.md"]
    talents = [int(value) for value in re.findall(r"^\|\s*(\d+)\s*\|\s*\*\*", talents_text, re.MULTILINE)]
    if talents != list(range(1, 101)):
        raise RuntimeError(f"Talent sequence is invalid: {talents[:5]}...{talents[-5:]}")

    manifest_path = ASSETS_DIR / "asset-provenance.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    if len(manifest.get("assets", [])) != EXPECTED_ILLUSTRATION_COUNT:
        raise RuntimeError(
            f"The illustration manifest must contain {EXPECTED_ILLUSTRATION_COUNT} selected assets"
        )
    for asset in manifest["assets"]:
        asset_path = ASSETS_DIR / asset["path"]
        if not asset_path.is_file():
            raise RuntimeError(f"Missing illustration: {asset_path}")
        if sha256(asset_path) != asset["sha256"]:
            raise RuntimeError(f"Hash mismatch for {asset_path.name}")

    words = len(re.findall(r"\b[\wÀ-ÿ’'-]+\b", all_text, flags=re.UNICODE))
    return {
        "parts": [path.name for path in files],
        "word_count": words,
        "skill_count": len(skills),
        "automatic_acquisition_count": len(acquisitions),
        "talent_count": len(talents),
        "illustration_count": len(manifest["assets"]),
    }


def set_repeat_table_header(row) -> None:
    tr_pr = row._tr.get_or_add_trPr()
    header = OxmlElement("w:tblHeader")
    header.set(qn("w:val"), "true")
    tr_pr.append(header)


def set_row_cant_split(row) -> None:
    """Keep a logical table record together when Word paginates the table."""
    tr_pr = row._tr.get_or_add_trPr()
    if tr_pr.find(qn("w:cantSplit")) is None:
        tr_pr.append(OxmlElement("w:cantSplit"))


def create_numbering_restart(doc: Document) -> int:
    """Create a real numbered-list instance that restarts at one."""
    style = doc.styles["List Number"].element
    style_p_pr = style.find(qn("w:pPr"))
    style_num_pr = style_p_pr.find(qn("w:numPr")) if style_p_pr is not None else None
    style_num_id = style_num_pr.find(qn("w:numId")) if style_num_pr is not None else None
    if style_num_id is None:
        raise RuntimeError("List Number style does not expose a numbering definition")

    base_num_id = int(style_num_id.get(qn("w:val")))
    numbering = doc.part.numbering_part.element
    base_num = next(
        node
        for node in numbering.findall(qn("w:num"))
        if int(node.get(qn("w:numId"))) == base_num_id
    )
    abstract_num_id = base_num.find(qn("w:abstractNumId")).get(qn("w:val"))
    new_num_id = max(int(node.get(qn("w:numId"))) for node in numbering.findall(qn("w:num"))) + 1

    num = OxmlElement("w:num")
    num.set(qn("w:numId"), str(new_num_id))
    abstract = OxmlElement("w:abstractNumId")
    abstract.set(qn("w:val"), abstract_num_id)
    num.append(abstract)
    level_override = OxmlElement("w:lvlOverride")
    level_override.set(qn("w:ilvl"), "0")
    start_override = OxmlElement("w:startOverride")
    start_override.set(qn("w:val"), "1")
    level_override.append(start_override)
    num.append(level_override)
    numbering.append(num)
    return new_num_id


def apply_numbering(paragraph, num_id: int) -> None:
    p_pr = paragraph._p.get_or_add_pPr()
    existing = p_pr.find(qn("w:numPr"))
    if existing is not None:
        p_pr.remove(existing)
    num_pr = OxmlElement("w:numPr")
    level = OxmlElement("w:ilvl")
    level.set(qn("w:val"), "0")
    number = OxmlElement("w:numId")
    number.set(qn("w:val"), str(num_id))
    num_pr.extend([level, number])
    p_pr.append(num_pr)


def set_cell_shading(cell, fill: str) -> None:
    tc_pr = cell._tc.get_or_add_tcPr()
    shading = tc_pr.find(qn("w:shd"))
    if shading is None:
        shading = OxmlElement("w:shd")
        tc_pr.append(shading)
    shading.set(qn("w:fill"), fill)


def set_cell_width(cell, width_inches: float) -> None:
    tc_pr = cell._tc.get_or_add_tcPr()
    tc_width = tc_pr.find(qn("w:tcW"))
    if tc_width is None:
        tc_width = OxmlElement("w:tcW")
        tc_pr.append(tc_width)
    tc_width.set(qn("w:type"), "dxa")
    tc_width.set(qn("w:w"), str(int(width_inches * 1440)))


def set_table_geometry(table, widths_inches: list[float]) -> None:
    """Write one explicit, fixed table geometry understood by Word and other readers."""
    widths_twips = [int(width * 1440) for width in widths_inches]
    table_width = sum(widths_twips)

    tbl_pr = table._tbl.tblPr
    tbl_width = tbl_pr.find(qn("w:tblW"))
    if tbl_width is None:
        tbl_width = OxmlElement("w:tblW")
        tbl_pr.append(tbl_width)
    tbl_width.set(qn("w:type"), "dxa")
    tbl_width.set(qn("w:w"), str(table_width))

    tbl_indent = tbl_pr.find(qn("w:tblInd"))
    if tbl_indent is None:
        tbl_indent = OxmlElement("w:tblInd")
        tbl_pr.append(tbl_indent)
    tbl_indent.set(qn("w:type"), "dxa")
    # Match Word's default 120-DXA cell margin so the visible cell content and
    # the surrounding body text share the same left edge.
    tbl_indent.set(qn("w:w"), "120")

    layout = tbl_pr.find(qn("w:tblLayout"))
    if layout is None:
        layout = OxmlElement("w:tblLayout")
        tbl_pr.append(layout)
    layout.set(qn("w:type"), "fixed")

    grid = table._tbl.tblGrid
    for grid_col in list(grid):
        grid.remove(grid_col)
    for width_twips in widths_twips:
        grid_col = OxmlElement("w:gridCol")
        grid_col.set(qn("w:w"), str(width_twips))
        grid.append(grid_col)

    for row in table.rows:
        for column_index, cell in enumerate(row.cells):
            set_cell_width(cell, widths_inches[column_index])


def set_paragraph_background(paragraph, fill: str) -> None:
    p_pr = paragraph._p.get_or_add_pPr()
    shading = p_pr.find(qn("w:shd"))
    if shading is None:
        shading = OxmlElement("w:shd")
        p_pr.append(shading)
    shading.set(qn("w:fill"), fill)


def set_quote_border(paragraph) -> None:
    p_pr = paragraph._p.get_or_add_pPr()
    borders = p_pr.find(qn("w:pBdr"))
    if borders is None:
        borders = OxmlElement("w:pBdr")
        p_pr.append(borders)
    left = OxmlElement("w:left")
    left.set(qn("w:val"), "single")
    left.set(qn("w:sz"), "18")
    left.set(qn("w:space"), "8")
    left.set(qn("w:color"), BRONZE)
    borders.append(left)


def set_bottom_rule(paragraph, color: str, size: str = "6") -> None:
    p_pr = paragraph._p.get_or_add_pPr()
    borders = p_pr.find(qn("w:pBdr"))
    if borders is None:
        borders = OxmlElement("w:pBdr")
        p_pr.append(borders)
    bottom = OxmlElement("w:bottom")
    bottom.set(qn("w:val"), "single")
    bottom.set(qn("w:sz"), size)
    bottom.set(qn("w:space"), "4")
    bottom.set(qn("w:color"), color)
    borders.append(bottom)


def add_bookmark(paragraph, name: str, bookmark_id: int) -> None:
    start = OxmlElement("w:bookmarkStart")
    start.set(qn("w:id"), str(bookmark_id))
    start.set(qn("w:name"), name)
    end = OxmlElement("w:bookmarkEnd")
    end.set(qn("w:id"), str(bookmark_id))
    paragraph._p.insert(0, start)
    paragraph._p.append(end)


def add_internal_link(paragraph, text: str, anchor: str) -> None:
    hyperlink = OxmlElement("w:hyperlink")
    hyperlink.set(qn("w:anchor"), anchor)
    run = OxmlElement("w:r")
    run_pr = OxmlElement("w:rPr")
    color = OxmlElement("w:color")
    color.set(qn("w:val"), BLUE)
    underline = OxmlElement("w:u")
    underline.set(qn("w:val"), "single")
    run_pr.extend([color, underline])
    text_node = OxmlElement("w:t")
    text_node.text = text
    run.extend([run_pr, text_node])
    hyperlink.append(run)
    paragraph._p.append(hyperlink)


def add_page_number(paragraph) -> None:
    paragraph.alignment = WD_ALIGN_PARAGRAPH.CENTER
    run = paragraph.add_run()
    begin = OxmlElement("w:fldChar")
    begin.set(qn("w:fldCharType"), "begin")
    instruction = OxmlElement("w:instrText")
    instruction.set(qn("xml:space"), "preserve")
    instruction.text = " PAGE "
    separate = OxmlElement("w:fldChar")
    separate.set(qn("w:fldCharType"), "separate")
    display = OxmlElement("w:t")
    display.text = "1"
    end = OxmlElement("w:fldChar")
    end.set(qn("w:fldCharType"), "end")
    run._r.extend([begin, instruction, separate, display, end])


INLINE_RE = re.compile(r"(\*\*.+?\*\*|`[^`]+`|\*[^*]+\*|\[[^\]]+\]\([^)]+\))")


def add_inline(paragraph, text: str, *, color: str | None = None, size: float | None = None) -> None:
    position = 0
    for match in INLINE_RE.finditer(text):
        if match.start() > position:
            run = paragraph.add_run(text[position : match.start()])
            if color:
                run.font.color.rgb = RGBColor.from_string(color)
            if size:
                run.font.size = Pt(size)
        token = match.group(0)
        if token.startswith("**"):
            run = paragraph.add_run(token[2:-2])
            run.bold = True
        elif token.startswith("`"):
            run = paragraph.add_run(token[1:-1])
            run.font.name = "Consolas"
            run.font.size = Pt((size or 9.2) - 0.5)
            run.font.color.rgb = RGBColor.from_string(GREEN_DARK)
        elif token.startswith("*"):
            run = paragraph.add_run(token[1:-1])
            run.italic = True
        else:
            label, target = re.match(r"\[([^\]]+)\]\(([^)]+)\)", token).groups()
            run = paragraph.add_run(f"{label} ({target})")
            run.font.color.rgb = RGBColor.from_string(BLUE)
            run.underline = True
        if color and not token.startswith("`"):
            run.font.color.rgb = RGBColor.from_string(color)
        if size and not token.startswith("`"):
            run.font.size = Pt(size)
        position = match.end()
    if position < len(text):
        run = paragraph.add_run(text[position:])
        if color:
            run.font.color.rgb = RGBColor.from_string(color)
        if size:
            run.font.size = Pt(size)


def add_alt_text(inline_shape, alt: str) -> None:
    doc_pr = inline_shape._inline.docPr
    doc_pr.set("descr", alt)
    doc_pr.set("title", alt[:120])


def parse_table_row(line: str) -> list[str]:
    body = line.strip().strip("|")
    return [cell.strip().replace("\\|", "|") for cell in body.split("|")]


def is_separator_row(cells: list[str]) -> bool:
    return all(re.fullmatch(r":?-{3,}:?", cell.replace(" ", "")) for cell in cells)


def table_widths(column_count: int, available: float) -> list[float]:
    if column_count == 6:
        ratios = [0.045, 0.145, 0.11, 0.20, 0.25, 0.25]
    elif column_count == 5:
        ratios = [0.18, 0.22, 0.16, 0.22, 0.22]
    elif column_count == 4:
        ratios = [0.22, 0.26, 0.22, 0.30]
    elif column_count == 3:
        ratios = [0.28, 0.20, 0.52]
    elif column_count == 2:
        ratios = [0.40, 0.60]
    else:
        ratios = [1 / column_count] * column_count
    return [available * ratio for ratio in ratios]


def configure_section(section, landscape: bool) -> None:
    if landscape:
        section.orientation = WD_ORIENT.LANDSCAPE
        section.page_width = Mm(297)
        section.page_height = Mm(210)
        section.left_margin = Mm(13)
        section.right_margin = Mm(13)
        section.top_margin = Mm(14)
        section.bottom_margin = Mm(14)
    else:
        section.orientation = WD_ORIENT.PORTRAIT
        section.page_width = Mm(210)
        section.page_height = Mm(297)
        section.left_margin = Mm(17)
        section.right_margin = Mm(17)
        section.top_margin = Mm(16)
        section.bottom_margin = Mm(16)
    section.header_distance = Mm(7)
    section.footer_distance = Mm(7)


def configure_styles(doc: Document) -> None:
    styles = doc.styles
    normal = styles["Normal"]
    normal.font.name = "Georgia"
    normal.font.size = Pt(9.4)
    normal.font.color.rgb = RGBColor.from_string(CHARCOAL)
    normal.paragraph_format.space_after = Pt(4.5)
    normal.paragraph_format.line_spacing = 1.08
    normal.paragraph_format.widow_control = True

    for index, size, color, before, after in [
        (1, 22, GREEN_DARK, 18, 8),
        (2, 15, GREEN, 13, 5),
        (3, 11.5, BRONZE, 9, 3),
        (4, 9.8, SLATE, 7, 2),
    ]:
        style = styles[f"Heading {index}"]
        style.font.name = "Georgia"
        style.font.size = Pt(size)
        style.font.bold = True
        style.font.color.rgb = RGBColor.from_string(color)
        style.paragraph_format.space_before = Pt(before)
        style.paragraph_format.space_after = Pt(after)
        style.paragraph_format.keep_with_next = True
        style.paragraph_format.keep_together = index <= 2
        style.paragraph_format.widow_control = True

    caption = styles["Caption"]
    caption.font.name = "Georgia"
    caption.font.size = Pt(8.2)
    caption.font.italic = True
    caption.font.color.rgb = RGBColor.from_string(SLATE)
    caption.paragraph_format.space_after = Pt(8)
    caption.paragraph_format.space_before = Pt(2)
    caption.paragraph_format.left_indent = Mm(3)
    caption.paragraph_format.right_indent = Mm(3)
    caption.paragraph_format.keep_together = True
    caption.paragraph_format.widow_control = True
    caption.paragraph_format.keep_with_next = False

    for style_name, size, color in [
        ("Handbook Cover Title", 28, GREEN_DARK),
        ("Handbook Cover Subtitle", 16, BRONZE),
        ("Handbook TOC 1", 10.5, GREEN_DARK),
        ("Handbook TOC 2", 8.8, SLATE),
        ("Handbook Code", 8.2, CHARCOAL),
        ("Handbook Quote", 9.1, CHARCOAL),
        ("Handbook Chapter Kicker", 8.2, BRONZE),
    ]:
        if style_name not in styles:
            style = styles.add_style(style_name, WD_STYLE_TYPE.PARAGRAPH)
        else:
            style = styles[style_name]
        style.font.name = "Georgia" if "Code" not in style_name else "Consolas"
        style.font.size = Pt(size)
        style.font.color.rgb = RGBColor.from_string(color)
        style.paragraph_format.space_after = Pt(5)
    styles["Handbook Cover Title"].font.bold = True
    styles["Handbook TOC 1"].font.bold = True
    styles["Handbook TOC 2"].paragraph_format.left_indent = Mm(6)
    styles["Handbook Quote"].paragraph_format.space_before = Pt(4)
    styles["Handbook Quote"].paragraph_format.space_after = Pt(8)
    styles["Handbook Chapter Kicker"].font.bold = True
    styles["Handbook Chapter Kicker"].paragraph_format.keep_with_next = True
    styles["Handbook Chapter Kicker"].paragraph_format.space_before = Pt(0)
    styles["Handbook Chapter Kicker"].paragraph_format.space_after = Pt(5)

    # Set the document language for spell checking and assistive technology.
    for style in [normal, styles["Heading 1"], styles["Heading 2"], styles["Heading 3"], styles["Heading 4"]]:
        r_pr = style.element.get_or_add_rPr()
        language = r_pr.find(qn("w:lang"))
        if language is None:
            language = OxmlElement("w:lang")
            r_pr.append(language)
        language.set(qn("w:val"), "fr-FR")


def extract_toc(files: list[Path]) -> list[tuple[int, str, str]]:
    entries: list[tuple[int, str, str]] = []
    index = 0
    for path in files:
        for line in without_marker(path.read_text(encoding="utf-8")).splitlines():
            match = re.match(r"^(#{1,2})\s+(.+?)\s*$", line)
            if not match:
                continue
            index += 1
            level = len(match.group(1))
            title = re.sub(r"[*`]", "", match.group(2)).strip()
            entries.append((level, title, f"section_{index:03d}"))
    return entries


def add_cover(doc: Document) -> None:
    section = doc.sections[0]
    configure_section(section, landscape=False)
    section.different_first_page_header_footer = True

    title = doc.add_paragraph(style="Handbook Cover Title")
    title.alignment = WD_ALIGN_PARAGRAPH.CENTER
    title.add_run("MANUEL DU JOUEUR")

    subtitle = doc.add_paragraph(style="Handbook Cover Subtitle")
    subtitle.alignment = WD_ALIGN_PARAGRAPH.CENTER
    subtitle.add_run("OPENRPG FORGE")

    motto = doc.add_paragraph()
    motto.alignment = WD_ALIGN_PARAGRAPH.CENTER
    run = motto.add_run("Devenez ce que vous faites")
    run.italic = True
    run.font.size = Pt(11)
    run.font.color.rgb = RGBColor.from_string(SLATE)

    cover = ASSETS_DIR / "bright-fortress-life.png"
    picture_paragraph = doc.add_paragraph()
    picture_paragraph.alignment = WD_ALIGN_PARAGRAPH.CENTER
    shape = picture_paragraph.add_run().add_picture(str(cover), width=Inches(6.65))
    add_alt_text(shape, "Une forteresse claire, un village et des voyageurs occupés à négocier, étudier, travailler et s’entraîner.")

    promise = doc.add_paragraph()
    promise.alignment = WD_ALIGN_PARAGRAPH.CENTER
    add_inline(promise, "SANS CLASSE  •  42 COMPÉTENCES  •  100 TALENTS", color=GREEN_DARK, size=10.5)

    version = doc.add_paragraph()
    version.alignment = WD_ALIGN_PARAGRAPH.CENTER
    add_inline(version, "Version de conception 0.1 • 30 août 2026", color=GREEN_DARK, size=8.5)

    note = doc.add_paragraph()
    note.alignment = WD_ALIGN_PARAGRAPH.CENTER
    add_inline(note, "Ruleset parent : openrpg-forge.systemic-rpg.v1 • Runtime Rust non implémenté", color=SLATE, size=7.7)


def add_toc(doc: Document, toc: list[tuple[int, str, str]]) -> None:
    section = doc.add_section(WD_SECTION.NEW_PAGE)
    configure_section(section, landscape=False)
    title = doc.add_heading("Sommaire", level=1)
    add_bookmark(title, "handbook_toc", 1)
    intro = doc.add_paragraph()
    add_inline(intro, "Les entrées sont cliquables dans le document numérique. Les titres de niveau trois et quatre sont consultables dans chaque chapitre.", color=SLATE, size=8.5)
    for level, text, bookmark in toc:
        paragraph = doc.add_paragraph(style="Handbook TOC 1" if level == 1 else "Handbook TOC 2")
        if level == 1:
            paragraph.paragraph_format.space_before = Pt(4)
        add_internal_link(paragraph, text, bookmark)


def add_headers_and_footers(doc: Document, chapter_titles: list[str]) -> None:
    for index, section in enumerate(doc.sections):
        if index == 0:
            section.different_first_page_header_footer = True
        header = section.header
        header.is_linked_to_previous = False
        paragraph = header.paragraphs[0]
        paragraph.alignment = WD_ALIGN_PARAGRAPH.CENTER
        paragraph.clear()
        if index == 0:
            label = "MANUEL DU JOUEUR"
        elif index == 1:
            label = "SOMMAIRE"
        else:
            label = chapter_titles[index - 2].upper()
        add_inline(paragraph, f"OPENRPG FORGE  •  {label}", color=GREEN, size=7.5)

        footer = section.footer
        footer.is_linked_to_previous = False
        paragraph = footer.paragraphs[0]
        paragraph.clear()
        add_page_number(paragraph)
        for run in paragraph.runs:
            run.font.name = "Georgia"
            run.font.size = Pt(7.5)
            run.font.color.rgb = RGBColor.from_string(SLATE)


def build_docx(files: list[Path], output_path: Path) -> None:
    doc = Document()
    configure_styles(doc)
    doc.core_properties.title = "Manuel du joueur — OpenRPG Forge"
    doc.core_properties.subject = "Ruleset classless, progression par usage, compétences et talents"
    doc.core_properties.author = "OpenRPG Forge"
    doc.core_properties.keywords = "OpenRPG Forge, manuel du joueur, ruleset, compétences, talents, Rust"
    doc.core_properties.comments = "Version de conception 0.1 — runtime Rust non implémenté"

    add_cover(doc)
    toc = extract_toc(files)
    add_toc(doc, toc)

    heading_bookmarks = iter(toc)
    bookmark_id = 2
    first_content_heading = True
    current_landscape = False
    chapter_titles: list[str] = []
    chapter_number = 0

    def start_chapter(title: str) -> None:
        nonlocal current_landscape, first_content_heading, chapter_number
        landscape = title.startswith("Talents :")
        section = doc.add_section(WD_SECTION.NEW_PAGE)
        configure_section(section, landscape=landscape)
        current_landscape = landscape
        first_content_heading = False
        chapter_number += 1
        chapter_titles.append(title)

    for source in files:
        lines = without_marker(source.read_text(encoding="utf-8")).splitlines()
        index = 0
        while index < len(lines):
            line = lines[index].rstrip()
            stripped = line.strip()
            if not stripped:
                index += 1
                continue

            heading_match = re.match(r"^(#{1,4})\s+(.+)$", stripped)
            if heading_match:
                level = len(heading_match.group(1))
                text = heading_match.group(2).strip()
                if level == 1:
                    start_chapter(text)
                    back = doc.add_paragraph()
                    back.alignment = WD_ALIGN_PARAGRAPH.RIGHT
                    add_internal_link(back, "Retour au sommaire", "handbook_toc")
                    back.paragraph_format.space_after = Pt(2)
                    kicker = doc.add_paragraph(style="Handbook Chapter Kicker")
                    kicker.add_run(f"PARTIE {chapter_number:02d}")
                    set_bottom_rule(kicker, BRONZE)
                paragraph = doc.add_heading(level=min(level, 4))
                add_inline(paragraph, text)
                if level <= 2:
                    expected_level, expected_text, bookmark = next(heading_bookmarks)
                    if expected_level != level or expected_text != re.sub(r"[*`]", "", text):
                        raise RuntimeError(f"TOC mismatch at {text}")
                    add_bookmark(paragraph, bookmark, bookmark_id)
                    bookmark_id += 1
                index += 1
                continue

            if stripped.startswith(("```", "~~~")):
                fence = stripped[:3]
                code_lines: list[str] = []
                index += 1
                while index < len(lines) and not lines[index].strip().startswith(fence):
                    code_lines.append(lines[index].rstrip())
                    index += 1
                if index >= len(lines):
                    raise RuntimeError(f"Unclosed code fence in {source.name}")
                paragraph = doc.add_paragraph(style="Handbook Code")
                set_paragraph_background(paragraph, GRAY)
                paragraph.paragraph_format.left_indent = Mm(4)
                paragraph.paragraph_format.right_indent = Mm(4)
                paragraph.paragraph_format.space_before = Pt(3)
                paragraph.paragraph_format.space_after = Pt(6)
                run = paragraph.add_run("\n".join(code_lines))
                run.font.name = "Consolas"
                run.font.size = Pt(8.2)
                paragraph.paragraph_format.keep_together = len(code_lines) <= 12
                paragraph.paragraph_format.widow_control = True
                index += 1
                continue

            image_match = re.fullmatch(r"!\[([^\]]*)\]\(([^)]+)\)", stripped)
            if image_match:
                alt, target = image_match.groups()
                image_path = (source.parent / target).resolve()
                if not image_path.is_file():
                    raise RuntimeError(f"Missing image referenced by {source.name}: {image_path}")
                paragraph = doc.add_paragraph()
                paragraph.alignment = WD_ALIGN_PARAGRAPH.CENTER
                max_width = 9.4 if current_landscape else 6.6
                if image_path.name == "progression-by-practice.png":
                    # Complete the progression chapter opener instead of leaving an
                    # accidental half-page void before this explanatory plate.
                    max_width = 5.35
                max_height = 6.1 if current_landscape else 7.1
                shape = paragraph.add_run().add_picture(str(image_path), width=Inches(max_width))
                if shape.height > Inches(max_height):
                    ratio = Inches(max_height) / shape.height
                    shape.width = int(shape.width * ratio)
                    shape.height = Inches(max_height)
                add_alt_text(shape, alt)
                index += 1
                caption_index = index
                while caption_index < len(lines) and not lines[caption_index].strip():
                    caption_index += 1
                caption_text = lines[caption_index].strip() if caption_index < len(lines) else ""
                if re.fullmatch(r"\*[^*]+\*", caption_text):
                    paragraph.paragraph_format.keep_with_next = True
                    caption = doc.add_paragraph(style="Caption")
                    add_inline(caption, caption_text)
                    index = caption_index + 1
                continue

            if stripped.startswith("|"):
                raw_rows: list[list[str]] = []
                while index < len(lines) and lines[index].strip().startswith("|"):
                    raw_rows.append(parse_table_row(lines[index]))
                    index += 1
                rows = [row for row in raw_rows if not is_separator_row(row)]
                if not rows:
                    continue
                columns = len(rows[0])
                if any(len(row) != columns for row in rows):
                    raise RuntimeError(f"Ragged table in {source.name}: {rows[:3]}")
                table = doc.add_table(rows=len(rows), cols=columns)
                table.alignment = WD_TABLE_ALIGNMENT.CENTER
                table.autofit = False
                table.style = "Table Grid"
                available = 10.1 if current_landscape else 6.75
                if columns == 6 and rows[0][0].strip().lower() == "domaine":
                    widths = [available * ratio for ratio in [0.14, 0.20, 0.09, 0.12, 0.22, 0.23]]
                    font_size = 7.4
                else:
                    widths = table_widths(columns, available)
                    font_size = 7.0 if columns >= 6 else 7.8 if columns == 5 else 8.1
                set_table_geometry(table, widths)
                for row_index, values in enumerate(rows):
                    set_row_cant_split(table.rows[row_index])
                    for column_index, value in enumerate(values):
                        cell = table.cell(row_index, column_index)
                        cell.vertical_alignment = WD_CELL_VERTICAL_ALIGNMENT.CENTER
                        paragraph = cell.paragraphs[0]
                        paragraph.paragraph_format.space_after = Pt(1.3)
                        paragraph.paragraph_format.space_before = Pt(1.3)
                        add_inline(paragraph, value, size=font_size)
                        if row_index == 0:
                            set_cell_shading(cell, GREEN)
                            for run in paragraph.runs:
                                run.font.color.rgb = RGBColor.from_string(WHITE)
                                run.bold = True
                        elif row_index % 2 == 0:
                            set_cell_shading(cell, PARCHMENT_LIGHT)
                set_repeat_table_header(table.rows[0])
                doc.add_paragraph().paragraph_format.space_after = Pt(1)
                continue

            if stripped.startswith(">"):
                quote_lines: list[str] = []
                while index < len(lines) and (lines[index].strip().startswith(">") or not lines[index].strip()):
                    if lines[index].strip().startswith(">"):
                        quote_lines.append(lines[index].strip()[1:].strip())
                    elif quote_lines and quote_lines[-1] != "":
                        quote_lines.append("")
                    index += 1
                paragraph = doc.add_paragraph(style="Handbook Quote")
                set_quote_border(paragraph)
                set_paragraph_background(paragraph, PARCHMENT_LIGHT)
                paragraph.paragraph_format.left_indent = Mm(5)
                paragraph.paragraph_format.right_indent = Mm(3)
                paragraph.paragraph_format.keep_together = True
                paragraph.paragraph_format.widow_control = True
                add_inline(paragraph, "\n".join(quote_lines))
                continue

            bullet_match = re.match(r"^-\s+(.+)$", stripped)
            number_match = re.match(r"^\d+\.\s+(.+)$", stripped)
            if bullet_match or number_match:
                paragraph = doc.add_paragraph(style="List Bullet" if bullet_match else "List Number")
                paragraph.paragraph_format.space_after = Pt(1.8)
                paragraph.paragraph_format.keep_together = True
                paragraph.paragraph_format.widow_control = True
                if number_match:
                    previous = lines[index - 1].strip() if index > 0 else ""
                    if not re.match(r"^\d+\.\s+", previous):
                        active_numbering_id = create_numbering_restart(doc)
                    apply_numbering(paragraph, active_numbering_id)
                add_inline(paragraph, (bullet_match or number_match).group(1))
                index += 1
                continue

            if stripped in {"---", "***", "___"}:
                paragraph = doc.add_paragraph()
                paragraph.paragraph_format.space_before = Pt(4)
                paragraph.paragraph_format.space_after = Pt(4)
                run = paragraph.add_run("◆")
                run.font.color.rgb = RGBColor.from_string(BRONZE)
                paragraph.alignment = WD_ALIGN_PARAGRAPH.CENTER
                index += 1
                continue

            paragraph_lines = [stripped]
            index += 1
            while index < len(lines):
                candidate = lines[index].strip()
                if not candidate:
                    break
                if re.match(r"^(#{1,4})\s+", candidate):
                    break
                if candidate.startswith(("```", "~~~", "|", ">", "![")):
                    break
                if re.match(r"^(-\s+|\d+\.\s+)", candidate):
                    break
                if candidate in {"---", "***", "___"}:
                    break
                paragraph_lines.append(candidate)
                index += 1
            paragraph = doc.add_paragraph()
            text = " ".join(paragraph_lines).replace("  ", " ")
            if len(paragraph_lines) == 1 and re.fullmatch(r"\*[^*]+\*", text):
                paragraph.style = doc.styles["Caption"]
            add_inline(paragraph, text)

    add_headers_and_footers(doc, chapter_titles)
    output_path.parent.mkdir(parents=True, exist_ok=True)
    doc.save(output_path)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--output-dir", type=Path, default=DEFAULT_OUTPUT)
    args = parser.parse_args()
    output_dir = args.output_dir.resolve()
    output_dir.mkdir(parents=True, exist_ok=True)

    files = part_files()
    report = validate_sources(files)

    canonical = assemble_markdown(files)
    CANONICAL_MD.write_text(canonical, encoding="utf-8", newline="\n")

    docx_path = output_dir / "Manuel_du_joueur_OpenRPG_Forge.docx"
    build_docx(files, docx_path)

    report.update(
        {
            "generated_on": date.today().isoformat(),
            "ruleset_id": "openrpg-forge.systemic-rpg.v1",
            "manual_id": "openrpg-forge.player-handbook.fr.v0.1",
            "canonical_markdown": str(CANONICAL_MD),
            "canonical_markdown_sha256": sha256(CANONICAL_MD),
            "docx": str(docx_path),
            "docx_sha256": sha256(docx_path),
        }
    )
    report_path = output_dir / "build-report.json"
    report_path.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

    print(json.dumps(report, ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
