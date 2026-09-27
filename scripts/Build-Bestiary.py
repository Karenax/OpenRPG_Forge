#!/usr/bin/env python3
"""Build the OpenRPG Forge bestiary PDF and its standalone data viewer.

The source of truth lives in docs/design/bestiary. The Rust runtime does not yet
implement these mechanics; generated artifacts keep that status visible.
"""

from __future__ import annotations

import argparse
import hashlib
import html
import json
import shutil
from collections import defaultdict
from datetime import datetime, timezone
from io import BytesIO
from pathlib import Path
from typing import Any, Iterable

from PIL import Image, ImageDraw, ImageEnhance, ImageFilter, ImageOps
from pypdf import PdfReader
from reportlab.lib import colors
from reportlab.lib.enums import TA_CENTER, TA_LEFT
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle, getSampleStyleSheet
from reportlab.lib.units import mm
from reportlab.pdfbase.pdfmetrics import stringWidth
from reportlab.pdfgen import canvas
from reportlab.platypus import Frame, KeepTogether, Paragraph, Spacer, Table, TableStyle
from reportlab.lib.utils import ImageReader


ROOT = Path(__file__).resolve().parents[1]
SOURCE_DIR = ROOT / "docs" / "design" / "bestiary"
ASSET_DIR = ROOT / "docs" / "design" / "assets" / "bestiary"
OUTPUT_DIR = ROOT / "output" / "bestiary"
PDF_OUTPUT_DIR = ROOT / "output" / "pdf"
TMP_DIR = ROOT / "tmp" / "pdfs" / "bestiary"

PAGE_W, PAGE_H = A4
GREEN = colors.HexColor("#274C3B")
GREEN_DARK = colors.HexColor("#173428")
BRONZE = colors.HexColor("#A66F36")
PARCHMENT = colors.HexColor("#F5EEDC")
PARCHMENT_LIGHT = colors.HexColor("#FBF8F0")
SLATE = colors.HexColor("#496573")
CHARCOAL = colors.HexColor("#2D2D2A")
PALE_GREY = colors.HexColor("#E8E4DA")
WHITE = colors.white

ATTRIBUTE_LABELS = {
    "strength": "FOR",
    "dexterity": "DEX",
    "constitution": "CON",
    "intelligence": "INT",
    "willpower": "VOL",
    "perception": "PER",
    "presence": "PRE",
}
RESOURCE_LABELS = {
    "vitality": "Vitalité",
    "stamina": "Endurance",
    "mana": "Mana",
    "balance": "Équilibre",
    "focus": "Concentration",
}
KNOWLEDGE_LABELS = {"etabli": "ÉTABLI", "suppose": "SUPPOSÉ", "secret": "DOSSIER COMPLET"}
VISIBILITY_LABELS = {"etablie": "établie", "supposee": "supposée", "secrete": "secrète"}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument("--allow-placeholders", action="store_true", help="Build with generated placeholder plates.")
    parser.add_argument("--pdf-only", action="store_true", help="Skip the standalone viewer.")
    parser.add_argument("--viewer-only", action="store_true", help="Skip the PDF.")
    return parser.parse_args()


def read_json(path: Path) -> dict[str, Any]:
    with path.open("r", encoding="utf-8") as handle:
        return json.load(handle)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest().upper()


def load_bestiary() -> tuple[dict[str, Any], list[dict[str, Any]]]:
    manifest = read_json(SOURCE_DIR / "bestiary.json")
    entries: list[dict[str, Any]] = []
    for relative in manifest["entries"]:
        path = SOURCE_DIR / relative
        entry = read_json(path)
        entry["_source_path"] = relative
        entries.append(entry)
    return manifest, entries


def calculated_resources(attributes: dict[str, int]) -> dict[str, int]:
    return {
        "vitality": 60 + 6 * attributes["constitution"] + 2 * attributes["strength"],
        "stamina": 50 + 5 * attributes["constitution"] + 3 * attributes["willpower"],
        "mana": 20 + 6 * attributes["willpower"] + 4 * attributes["intelligence"],
        "balance": 40 + 4 * attributes["strength"] + 4 * attributes["constitution"],
        "focus": 40 + 4 * attributes["perception"] + 4 * attributes["willpower"],
    }


def validate_bundle(manifest: dict[str, Any], entries: list[dict[str, Any]], allow_placeholders: bool) -> None:
    ids: set[str] = set()
    images: set[str] = set()
    for entry in entries:
        creature_id = entry["creature_id"]
        if creature_id in ids:
            raise ValueError(f"Duplicate creature_id: {creature_id}")
        ids.add(creature_id)
        expected = calculated_resources(entry["rules"]["attributes"])
        if entry["rules"]["resources"] != expected:
            raise ValueError(f"Resource mismatch for {creature_id}: expected {expected}")
        expected_plate_count = 2 if entry["major"] else 1
        if len(entry["plates"]) != expected_plate_count:
            raise ValueError(f"{creature_id} must define {expected_plate_count} plate(s)")
        if entry["major"] and "major_dossier" not in entry:
            raise ValueError(f"{creature_id} is major but has no major_dossier")
        for plate in entry["plates"]:
            filename = plate["image"]
            if filename in images:
                raise ValueError(f"Duplicate image filename: {filename}")
            images.add(filename)
            if not allow_placeholders and not (ASSET_DIR / filename).is_file():
                raise FileNotFoundError(f"Missing plate: {ASSET_DIR / filename}")
    if not allow_placeholders and not (ASSET_DIR / "cover.png").is_file():
        raise FileNotFoundError(f"Missing cover: {ASSET_DIR / 'cover.png'}")
    if manifest["status"] != "design-proposal-not-implemented":
        raise ValueError("The Rust bestiary must remain explicitly non-implemented at this gate.")
    release_wave = manifest.get("release_wave")
    if release_wave:
        family_ids = {
            creature_id
            for family in release_wave.get("families", {}).values()
            for creature_id in family.get("entry_ids", [])
        }
        wave_ids = set(release_wave.get("entry_ids", []))
        if wave_ids != family_ids:
            raise ValueError("release_wave entry_ids must match the union of its family rosters")
        unknown_ids = wave_ids - ids
        if unknown_ids:
            raise ValueError(f"release_wave references unknown creatures: {sorted(unknown_ids)}")


def styles() -> dict[str, ParagraphStyle]:
    sample = getSampleStyleSheet()
    return {
        "body": ParagraphStyle(
            "BestiaryBody",
            parent=sample["BodyText"],
            fontName="Times-Roman",
            fontSize=9,
            leading=10.6,
            textColor=CHARCOAL,
            spaceAfter=3.4,
            allowWidows=0,
            allowOrphans=0,
        ),
        "small": ParagraphStyle(
            "BestiarySmall",
            parent=sample["BodyText"],
            fontName="Helvetica",
            fontSize=7.8,
            leading=8.65,
            textColor=CHARCOAL,
            spaceAfter=1.8,
        ),
        "section": ParagraphStyle(
            "BestiarySection",
            parent=sample["Heading3"],
            fontName="Helvetica-Bold",
            fontSize=9.2,
            leading=10.4,
            textColor=GREEN_DARK,
            spaceBefore=2.4,
            spaceAfter=1.6,
            keepWithNext=True,
        ),
        "action": ParagraphStyle(
            "BestiaryAction",
            parent=sample["BodyText"],
            fontName="Helvetica",
            fontSize=7.8,
            leading=8.7,
            textColor=CHARCOAL,
            leftIndent=4,
            borderColor=PALE_GREY,
            borderWidth=0.5,
            borderPadding=2.2,
            backColor=PARCHMENT_LIGHT,
            spaceAfter=2.2,
        ),
        "knowledge": ParagraphStyle(
            "BestiaryKnowledge",
            parent=sample["BodyText"],
            fontName="Helvetica",
            fontSize=7.8,
            leading=8.6,
            textColor=CHARCOAL,
            leftIndent=3,
            spaceAfter=1.8,
        ),
        "guide": ParagraphStyle(
            "BestiaryGuide",
            parent=sample["BodyText"],
            fontName="Times-Roman",
            fontSize=10,
            leading=13,
            textColor=CHARCOAL,
            spaceAfter=7,
        ),
        "guide_section": ParagraphStyle(
            "BestiaryGuideSection",
            parent=sample["Heading2"],
            fontName="Helvetica-Bold",
            fontSize=14,
            leading=17,
            textColor=GREEN_DARK,
            spaceBefore=7,
            spaceAfter=6,
        ),
        "cover_title": ParagraphStyle(
            "CoverTitle",
            parent=sample["Title"],
            fontName="Helvetica-Bold",
            fontSize=31,
            leading=34,
            alignment=TA_CENTER,
            textColor=WHITE,
        ),
    }


def esc(value: Any) -> str:
    return html.escape(str(value), quote=True)


def paragraph(text: str, style: ParagraphStyle) -> Paragraph:
    return Paragraph(esc(text).replace("\n", "<br/>"), style)


def rich_paragraph(text: str, style: ParagraphStyle) -> Paragraph:
    return Paragraph(text, style)


def section(title: str, st: dict[str, ParagraphStyle]) -> Paragraph:
    return rich_paragraph(f"<font color='#A66F36'>|</font> {esc(title.upper())}", st["section"])


def comma(items: Iterable[str]) -> str:
    return ", ".join(str(item) for item in items)


def compact(text: str, limit: int) -> str:
    """Keep PDF copy compact while the interactive viewer retains the full source."""
    normalized = " ".join(str(text).split())
    if len(normalized) <= limit:
        return normalized
    sentence_end = normalized.rfind(". ", 0, limit)
    if sentence_end >= max(45, int(limit * 0.58)):
        return normalized[: sentence_end + 1]
    cut = normalized.rfind(" ", 0, limit - 1)
    return normalized[: max(cut, 1)].rstrip(" ,;:") + "…"


def labelled(label: str, value: Any, st: dict[str, ParagraphStyle], small: bool = False) -> Paragraph:
    style = st["small"] if small else st["body"]
    return rich_paragraph(f"<b>{esc(label)}.</b> {esc(value)}", style)


def bullet(value: str, st: dict[str, ParagraphStyle], small: bool = False) -> Paragraph:
    style = st["small"] if small else st["body"]
    clone = ParagraphStyle(f"{style.name}Bullet", parent=style, leftIndent=7, firstLineIndent=-5, bulletIndent=0)
    return Paragraph(f"- {esc(value)}", clone)


def attributes_table(entry: dict[str, Any]) -> Table:
    attrs = entry["rules"]["attributes"]
    resources = entry["rules"]["resources"]
    resource_abbr = {"vitality": "VIT", "stamina": "END", "mana": "MAN", "balance": "ÉQU", "focus": "CONC"}
    attr_keys = list(ATTRIBUTE_LABELS)
    resource_keys = list(RESOURCE_LABELS)
    rows = [
        [ATTRIBUTE_LABELS[key] for key in attr_keys],
        [str(attrs[key]) for key in attr_keys],
        [resource_abbr[key] for key in resource_keys] + ["", ""],
        [str(resources[key]) for key in resource_keys] + ["", ""],
    ]
    table = Table(rows, colWidths=[30] * 7, rowHeights=[11, 13, 11, 13], hAlign="LEFT")
    table.setStyle(
        TableStyle(
            [
                ("FONTNAME", (0, 0), (-1, -1), "Helvetica"),
                ("FONTNAME", (0, 0), (-1, 0), "Helvetica-Bold"),
                ("FONTNAME", (0, 2), (-1, 2), "Helvetica-Bold"),
                ("FONTSIZE", (0, 0), (-1, -1), 6.9),
                ("ALIGN", (0, 0), (-1, -1), "CENTER"),
                ("TEXTCOLOR", (0, 0), (-1, -1), CHARCOAL),
                ("BACKGROUND", (0, 0), (-1, -1), PARCHMENT_LIGHT),
                ("GRID", (0, 0), (-1, -1), 0.35, PALE_GREY),
                ("VALIGN", (0, 0), (-1, -1), "MIDDLE"),
                ("LEFTPADDING", (0, 0), (-1, -1), 1),
                ("RIGHTPADDING", (0, 0), (-1, -1), 1),
                ("TOPPADDING", (0, 0), (-1, -1), 1),
                ("BOTTOMPADDING", (0, 0), (-1, -1), 1),
            ]
        )
    )
    return table


def knowledge_story(entry: dict[str, Any], st: dict[str, ParagraphStyle]) -> list[Any]:
    story: list[Any] = [section("Connaissances de terrain", st)]
    selected = []
    for status in ("etabli", "suppose", "secret"):
        matches = [item for item in entry["field_knowledge"] if item["status"] == status]
        if matches:
            selected.append(sorted(matches, key=lambda item: item["difficulty"])[0])
    for item in selected:
        color = {"etabli": "#274C3B", "suppose": "#496573", "secret": "#A66F36"}[item["status"]]
        text = (
            f"<font color='{color}'><b>{KNOWLEDGE_LABELS[item['status']]}</b></font> "
            f"· {esc(item['skill_id'])} {item['difficulty']} — {esc(compact(item['fact'], 120))}"
        )
        story.append(Paragraph(text, st["knowledge"]))
    return story


def action_paragraph(action: dict[str, Any], st: dict[str, ParagraphStyle]) -> Paragraph:
    text = (
        f"<b>{esc(action['name'])}</b> <font color='#496573'>({VISIBILITY_LABELS[action['visibility']]})</font> · "
        f"{esc(action['skill_id'])}+{esc(action['attribute_id'])} · Impact {action['impact']} · "
        f"End. {action['stamina_cost']} · {esc(compact(action['reach'], 55))}<br/>"
        f"<b>Signal</b> {esc(compact(action['telegraph'], 90))} "
        f"<font color='#496573'>({esc(compact(action['preparation'], 48))})</font><br/>"
        f"<b>Effet</b> {esc(compact(action['effect'], 92))} "
        f"<b>Parade</b> {esc(compact(action['counterplay'], 92))}"
    )
    return Paragraph(text, st["action"])


def entry_column_stories(entry: dict[str, Any], st: dict[str, ParagraphStyle]) -> tuple[list[Any], list[Any]]:
    ecology = entry["ecology"]
    physique = entry["physique"]
    demeanor = entry["demeanor"]
    rules = entry["rules"]

    left: list[Any] = [
        section("Zoologie", st),
        labelled("Morphologie", compact(f"{physique['dimensions']} ; {physique['mass_kg'][0]:g}–{physique['mass_kg'][1]:g} kg. {physique['silhouette']}", 185), st),
        labelled("Locomotion", compact(f"{comma(physique['locomotion'])}. {physique['center_of_gravity']}", 145), st),
        labelled("Régime", compact(ecology["diet"], 135), st),
        labelled("Cycle et reproduction", compact(f"{ecology['lifecycle']} {ecology['reproduction']}", 180), st),
        labelled("Comportement et niche", compact(f"{ecology['behavior']} {ecology['niche']}", 210), st),
        section("Milieux, sens et traces", st),
        labelled("Habitat", compact(comma(entry["habitats"]), 135), st),
    ]
    left.extend(bullet(compact(item, 100), st, small=True) for item in entry["signs"][:2])
    left.append(labelled("Sens et communication", compact(f"{comma(entry['senses'])}. {comma(entry['communication'])}", 175), st, small=True))
    left.extend(
        [
            section("Volonté et moral", st),
            labelled("Intentions et moral", compact(f"{comma(demeanor['objectives'])}. {demeanor['morale']}", 180), st, small=True),
            labelled("Escalade et fuite", compact(f"{demeanor['escalation']} {demeanor['retreat']}", 185), st, small=True),
        ]
    )
    left.extend(knowledge_story(entry, st))
    left.append(section("Monde comme arme", st))
    for item in rules["environmental_interactions"][:2]:
        left.append(labelled(f"{item['input']} -> {compact(item['result'], 55)}", compact(item["chain"], 100), st, small=True))

    right: list[Any] = [section("Profil systémique", st), attributes_table(entry), Spacer(1, 3)]
    skills = sorted(rules["skills"].items(), key=lambda pair: (-pair[1], pair[0]))
    right.append(labelled("Compétences", compact(comma(f"{key} {value}" for key, value in skills[:6]), 145), st, small=True))
    right.append(labelled("Déplacement", compact(rules["movement"], 130), st, small=True))
    right.append(labelled("Protection", compact(comma(rules["protections"]), 155), st, small=True))
    right.append(section("Attaques et capacités", st))
    right.extend(action_paragraph(action, st) for action in rules["attacks"] + rules["abilities"])
    right.append(section("Résistances et failles", st))
    for label, key in (("Résiste", "resistances"), ("Vulnérable", "vulnerabilities"), ("Immunité justifiée", "immunities")):
        values = rules[key]
        if values:
            right.append(labelled(label, compact("; ".join(f"{item['subject']} — {item['mechanism']}" for item in values), 150), st, small=True))
    return left, right


def major_story(entry: dict[str, Any], st: dict[str, ParagraphStyle]) -> tuple[list[Any], list[Any]]:
    dossier = entry["major_dossier"]
    left: list[Any] = [
        section("Arène vivante", st),
        paragraph(dossier["arena"], st["body"]),
        section("Séquence lisible", st),
    ]
    left.extend(bullet(item, st) for item in dossier["sequence"])
    left.extend([section("Chaînes causales", st)])
    left.extend(bullet(item, st) for item in dossier["causal_opportunities"])
    right: list[Any] = [
        section("Télégraphes critiques", st),
    ]
    right.extend(bullet(item, st) for item in entry["encounter_use"]["telegraphs"])
    right.append(section("Complications", st))
    right.extend(bullet(item, st) for item in entry["encounter_use"]["complications"])
    right.extend(knowledge_story(entry, st))
    secret_actions = [a for a in entry["rules"]["abilities"] if a["visibility"] == "secrete"]
    if secret_actions:
        right.append(section("Capacités du dossier complet", st))
        right.extend(action_paragraph(action, st) for action in secret_actions)
    right.extend([section("Après la rencontre", st), paragraph(dossier["aftermath"], st["body"])])
    return left, right


def placeholder_image(name: str, width: int = 1500, height: int = 2200) -> Image.Image:
    image = Image.new("RGB", (width, height), "#173428")
    draw = ImageDraw.Draw(image)
    for y in range(height):
        blend = y / max(height - 1, 1)
        color = (
            int(23 + 39 * blend),
            int(52 + 24 * blend),
            int(40 + 19 * blend),
        )
        draw.line((0, y, width, y), fill=color)
    draw.ellipse((width * 0.20, height * 0.24, width * 0.80, height * 0.78), outline="#A66F36", width=14)
    draw.line((width * 0.3, height * 0.72, width * 0.7, height * 0.28), fill="#F5EEDC", width=9)
    draw.text((width * 0.5, height * 0.84), name, fill="#F5EEDC", anchor="mm")
    return image


def load_art(path: Path, label: str, allow_placeholders: bool) -> Image.Image:
    if path.is_file():
        return Image.open(path).convert("RGB")
    if allow_placeholders:
        return placeholder_image(label)
    raise FileNotFoundError(path)


def fitted_reader(image: Image.Image, width_pt: float, height_pt: float, *, centering: tuple[float, float] = (0.5, 0.48)) -> ImageReader:
    requested_w = max(1, int(width_pt * 2))
    requested_h = max(1, int(height_pt * 2))
    scale = min(1.0, image.width / requested_w, image.height / requested_h)
    target_w = max(1, int(requested_w * scale))
    target_h = max(1, int(requested_h * scale))
    fitted = ImageOps.fit(image, (target_w, target_h), method=Image.Resampling.LANCZOS, centering=centering)
    stream = BytesIO()
    fitted.save(stream, format="JPEG", quality=90, optimize=True, subsampling=0)
    stream.seek(0)
    return ImageReader(stream)


def muted_cover_reader(image: Image.Image, width_pt: float, height_pt: float) -> ImageReader:
    requested_w = max(1, int(width_pt * 2))
    requested_h = max(1, int(height_pt * 2))
    scale = min(1.0, image.width / requested_w, image.height / requested_h)
    target_w = max(1, int(requested_w * scale))
    target_h = max(1, int(requested_h * scale))
    fitted = ImageOps.fit(image, (target_w, target_h), method=Image.Resampling.LANCZOS)
    fitted = ImageEnhance.Color(fitted).enhance(0.70)
    fitted = ImageEnhance.Brightness(fitted).enhance(0.58)
    fitted = fitted.filter(ImageFilter.GaussianBlur(radius=0.35))
    stream = BytesIO()
    fitted.save(stream, format="JPEG", quality=89, optimize=True, subsampling=0)
    stream.seek(0)
    return ImageReader(stream)


def draw_page_base(pdf: canvas.Canvas, page_number: int, total_pages: int, *, title: str = "BESTIAIRE OPENRPG FORGE") -> None:
    pdf.setFillColor(PARCHMENT)
    pdf.rect(0, 0, PAGE_W, PAGE_H, stroke=0, fill=1)
    inner_x = PAGE_W - 17 * mm if page_number % 2 == 0 else 17 * mm
    pdf.setStrokeColor(BRONZE)
    pdf.setLineWidth(0.6)
    pdf.line(inner_x, 13 * mm, inner_x, PAGE_H - 13 * mm)
    pdf.setFont("Helvetica", 6.9)
    pdf.setFillColor(SLATE)
    header_x = 16 * mm if page_number % 2 == 0 else PAGE_W - 16 * mm
    align = "left" if page_number % 2 == 0 else "right"
    if align == "left":
        pdf.drawString(header_x, PAGE_H - 10 * mm, title)
    else:
        pdf.drawRightString(header_x, PAGE_H - 10 * mm, title)
    pdf.setFont("Helvetica", 7.2)
    page_label = f"{page_number} / {total_pages}"
    if page_number % 2 == 0:
        pdf.drawString(16 * mm, 9 * mm, page_label)
    else:
        pdf.drawRightString(PAGE_W - 16 * mm, 9 * mm, page_label)


def draw_cover(pdf: canvas.Canvas, manifest: dict[str, Any], allow_placeholders: bool) -> None:
    art = load_art(ASSET_DIR / "cover.png", "Bestiaire OpenRPG Forge", allow_placeholders)
    pdf.drawImage(muted_cover_reader(art, PAGE_W, PAGE_H), 0, 0, PAGE_W, PAGE_H, mask="auto")
    pdf.setFillColor(colors.Color(0.04, 0.12, 0.09, alpha=0.54))
    pdf.rect(0, 0, PAGE_W, PAGE_H, stroke=0, fill=1)
    pdf.setStrokeColor(BRONZE)
    pdf.setLineWidth(1.2)
    pdf.roundRect(24 * mm, 70 * mm, PAGE_W - 48 * mm, 102 * mm, 4 * mm, stroke=1, fill=0)
    pdf.setFillColor(WHITE)
    pdf.setFont("Helvetica-Bold", 30)
    pdf.drawCentredString(PAGE_W / 2, 146 * mm, "BESTIAIRE")
    pdf.setFont("Helvetica", 17)
    pdf.drawCentredString(PAGE_W / 2, 133 * mm, "OPENRPG FORGE")
    pdf.setStrokeColor(BRONZE)
    pdf.line(63 * mm, 126 * mm, PAGE_W - 63 * mm, 126 * mm)
    pdf.setFont("Times-Italic", 13)
    pdf.drawCentredString(PAGE_W / 2, 114 * mm, manifest["subtitle"])
    pdf.setFont("Helvetica", 8.5)
    pdf.drawCentredString(PAGE_W / 2, 85 * mm, "ÉDITION DE CONCEPTION · 0.1")
    pdf.setFont("Helvetica", 7.3)
    pdf.drawCentredString(PAGE_W / 2, 16 * mm, "Données proposées · runtime Rust non implémenté")
    pdf.bookmarkPage("cover")
    pdf.addOutlineEntry("Couverture", "cover", level=0, closed=False)


def draw_guide_page(pdf: canvas.Canvas, page_number: int, total_pages: int, st: dict[str, ParagraphStyle], part: int) -> None:
    draw_page_base(pdf, page_number, total_pages, title="GUIDE DE TERRAIN")
    x = 18 * mm if page_number % 2 == 0 else 21 * mm
    usable_w = PAGE_W - 39 * mm
    pdf.setFillColor(GREEN_DARK)
    pdf.setFont("Helvetica-Bold", 23)
    title = "Lire une créature" if part == 1 else "Un même monde, un même système"
    pdf.drawString(x, PAGE_H - 29 * mm, title)
    pdf.setStrokeColor(BRONZE)
    pdf.setLineWidth(1)
    pdf.line(x, PAGE_H - 34 * mm, x + usable_w, PAGE_H - 34 * mm)
    frame = Frame(x, 23 * mm, usable_w, PAGE_H - 63 * mm, showBoundary=0, leftPadding=0, rightPadding=0, topPadding=4, bottomPadding=0)
    if part == 1:
        story: list[Any] = [
            paragraph("Ce volume décrit des organismes, des constructions et des êtres altérés comme des éléments du monde. Une fiche ne répond pas seulement à « combien de dégâts ? » : elle explique comment l’espèce vit, ce qu’elle perçoit, ce qu’elle veut et comment le terrain la transforme.", st["guide"]),
            rich_paragraph("<b>PAGE GAUCHE — RECONNAÎTRE</b>", st["guide_section"]),
            paragraph("La planche montre une silhouette complète dans son milieu, une échelle lisible et les matières importantes : peau, plume, fourrure, carapace, pierre ou manifestation magique. La légende donne les signes qui permettent d’anticiper la rencontre.", st["guide"]),
            rich_paragraph("<b>PAGE DROITE — COMPRENDRE ET AGIR</b>", st["guide_section"]),
            paragraph("Zoologie, cycle de vie, traces, comportement, moral et règles partagent la même page. Les attaques annoncent leur préparation, leur coût, leur portée et leur contre-jeu. Les issues sans combat sont présentées comme de vraies solutions.", st["guide"]),
            rich_paragraph("<b>CONNAISSANCES PROGRESSIVES</b>", st["guide_section"]),
            paragraph("Établi : signe courant ou observation reproductible. Supposé : hypothèse crédible, encore discutable. Dossier complet : vérité destinée au meneur ou à une révélation interactive. Les seuils 14, 18, 22, 26, 30 et 34 utilisent Lore, Pistage, Investigation, Vigilance ou Dressage.", st["guide"]),
            rich_paragraph("<b>MENACE, PAS NIVEAU</b>", st["guide_section"]),
            paragraph("Faible, modérée, forte, critique ou majeure décrit le risque dans des circonstances ordinaires. Ce n’est ni un niveau, ni une promesse d’équilibrage. Une créature dangereuse peut être évitée, détournée ou vaincue par une chaîne causale bien préparée.", st["guide"]),
        ]
    else:
        story = [
            paragraph("Les créatures emploient la même résolution que les personnages. Leurs qualités viennent de leurs attributs, compétences, ressources, anatomie et environnement — pas d’un sous-système opaque.", st["guide"]),
            rich_paragraph("<b>TEST COMMUN</b>", st["guide_section"]),
            rich_paragraph("<font name='Courier-Bold'>2d10 + floor(compétence / 5) + attribut + contexte + aide</font>", st["guide"]),
            paragraph("Les seuils vont de 14, action exigeante mais courante, à 34, exploit presque inaccessible. Les attributs restent compris entre 1 et 10 et les compétences entre 0 et 100.", st["guide"]),
            rich_paragraph("<b>RESSOURCES</b>", st["guide_section"]),
            paragraph("Vitalité, Endurance, Mana, Équilibre et Concentration suivent les formules du Manuel du joueur. Même les créatures majeures ne gagnent pas une réserve artificielle : leur résistance doit venir de leur masse, de leur protection, de leurs appuis, de leur magie ou de leur intelligence.", st["guide"]),
            rich_paragraph("<b>TRAUMA ET PROTECTION</b>", st["guide_section"]),
            rich_paragraph("<font name='Courier-Bold'>Trauma = max(0, arrondi((Impact x qualité de contact) - (0,65 x protection)) x zone)</font>", st["guide"]),
            paragraph("Une zone anatomique, un mauvais angle ou un contact partiel changent le résultat. Les blessures restent physiques et lisibles ; elles ne sont jamais résumées à une barre de points abstraite.", st["guide"]),
            rich_paragraph("<b>MONDE COMME ARME</b>", st["guide_section"]),
            paragraph("Feu, froid, eau, électricité, poison, acide, force, structures et surfaces forment des chaînes causales. Une résistance ralentit l’accumulation ; une immunité n’existe que si son mécanisme reste visible et testable.", st["guide"]),
            rich_paragraph("<b>STATUT DU VOLUME</b>", st["guide_section"]),
            paragraph("Toutes les valeurs sont des propositions de conception. Elles préparent d’éventuels prototypes de gameplay à partir de la Gate 3, après validation des gates précédentes, mais ne décrivent pas encore un runtime Rust livré ou qualifié.", st["guide"]),
        ]
    frame.addFromList(story, pdf)
    if story:
        raise RuntimeError(f"Guide page {part} overflowed with {len(story)} flowables")


def draw_plate_page(
    pdf: canvas.Canvas,
    entry: dict[str, Any],
    plate: dict[str, Any],
    page_number: int,
    total_pages: int,
    allow_placeholders: bool,
    plate_index: int,
) -> None:
    if page_number % 2 != 0:
        raise RuntimeError(f"Plate page for {entry['creature_id']} must be even, got {page_number}")
    draw_page_base(pdf, page_number, total_pages, title=entry["classification"]["family"].upper())
    outer = 16 * mm
    inner = 20 * mm
    x = outer
    width = PAGE_W - outer - inner
    image_y = 39 * mm
    image_h = PAGE_H - 72 * mm
    art = load_art(ASSET_DIR / plate["image"], entry["name"], allow_placeholders)
    pdf.setFillColor(GREEN_DARK)
    pdf.roundRect(x - 2, image_y - 2, width + 4, image_h + 4, 2 * mm, stroke=0, fill=1)
    pdf.drawImage(fitted_reader(art, width, image_h), x, image_y, width, image_h, mask="auto")
    pdf.setStrokeColor(BRONZE)
    pdf.setLineWidth(1.1)
    pdf.roundRect(x - 2, image_y - 2, width + 4, image_h + 4, 2 * mm, stroke=1, fill=0)
    pdf.setFillColor(GREEN_DARK)
    pdf.roundRect(x, PAGE_H - 35 * mm, width, 20 * mm, 1.8 * mm, stroke=0, fill=1)
    pdf.setFillColor(WHITE)
    pdf.setFont("Helvetica-Bold", 20 if len(entry["name"]) < 25 else 17)
    pdf.drawString(x + 6 * mm, PAGE_H - 24 * mm, entry["name"].upper())
    pdf.setFont("Helvetica", 7.6)
    pdf.setFillColor(PARCHMENT_LIGHT)
    suffix = " · DOSSIER II" if plate_index else ""
    pdf.drawRightString(x + width - 6 * mm, PAGE_H - 24 * mm, entry["creature_id"] + suffix)
    pdf.setFillColor(PARCHMENT_LIGHT)
    pdf.setStrokeColor(BRONZE)
    pdf.roundRect(x, 16 * mm, width, 18 * mm, 1.8 * mm, stroke=1, fill=1)
    pdf.setFillColor(CHARCOAL)
    pdf.setFont("Times-Italic", 8.6)
    caption = plate["caption"]
    max_width = width - 12 * mm
    words = caption.split()
    lines: list[str] = []
    current = ""
    for word in words:
        candidate = f"{current} {word}".strip()
        if stringWidth(candidate, "Times-Italic", 8.6) <= max_width:
            current = candidate
        else:
            lines.append(current)
            current = word
    if current:
        lines.append(current)
    for index, line in enumerate(lines[:2]):
        pdf.drawString(x + 6 * mm, 27 * mm - index * 4.2 * mm, line)
    anchor = f"{entry['creature_id']}-plate-{plate_index + 1}"
    pdf.bookmarkPage(anchor)
    if plate_index == 0:
        pdf.addOutlineEntry(entry["name"], anchor, level=0, closed=False)


def draw_entry_header(pdf: canvas.Canvas, entry: dict[str, Any], page_number: int, total_pages: int, dossier: bool = False) -> None:
    draw_page_base(pdf, page_number, total_pages, title=entry["creature_id"])
    x = 20 * mm
    right = PAGE_W - 16 * mm
    pdf.setFillColor(GREEN_DARK)
    pdf.setFont("Helvetica-Bold", 19 if len(entry["name"]) < 27 else 16)
    title = entry["name"] + (" — DOSSIER MAJEUR" if dossier else "")
    pdf.drawString(x, PAGE_H - 24 * mm, title.upper())
    pdf.setFont("Times-Italic", 9.2)
    pdf.setFillColor(SLATE)
    pdf.drawString(x, PAGE_H - 30 * mm, entry["subtitle"])
    pdf.setStrokeColor(BRONZE)
    pdf.line(x, PAGE_H - 34 * mm, right, PAGE_H - 34 * mm)
    if not dossier:
        meta = entry["classification"]
        badges = [meta["nature"], meta["rarity"], meta["activity"], f"menace {meta['threat']}"]
        y = PAGE_H - 42 * mm
        gap = 2 * mm
        w = (right - x - gap * 3) / 4
        font_size = 6.1
        for index, badge in enumerate(badges):
            words = badge.upper().split()
            text = " ".join(words)
            while len(words) > 1 and stringWidth(text, "Helvetica-Bold", font_size) > w - 5 * mm:
                words.pop()
                text = " ".join(words) + "..."
            if stringWidth(text, "Helvetica-Bold", font_size) > w - 5 * mm:
                text = compact(text, 16)
            cursor = x + index * (w + gap)
            pdf.setFillColor(PARCHMENT_LIGHT)
            pdf.setStrokeColor(BRONZE)
            pdf.roundRect(cursor, y, w, 6.5 * mm, 1.5 * mm, stroke=1, fill=1)
            pdf.setFillColor(GREEN_DARK)
            pdf.setFont("Helvetica-Bold", font_size)
            pdf.drawCentredString(cursor + w / 2, y + 2.1 * mm, text)


def draw_two_column_story(
    pdf: canvas.Canvas,
    left_story: list[Any],
    right_story: list[Any],
    *,
    page_number: int,
    top_mm: float,
) -> None:
    inner_left = 20 * mm
    outer_right = 16 * mm
    gap = 5 * mm
    usable = PAGE_W - inner_left - outer_right
    col_w = (usable - gap) / 2
    y = 17 * mm
    top = PAGE_H - top_mm * mm
    height = top - y
    left_frame = Frame(inner_left, y, col_w, height, showBoundary=0, leftPadding=0, rightPadding=2, topPadding=0, bottomPadding=0)
    right_frame = Frame(inner_left + col_w + gap, y, col_w, height, showBoundary=0, leftPadding=2, rightPadding=0, topPadding=0, bottomPadding=0)
    left_frame.addFromList(left_story, pdf)
    right_frame.addFromList(right_story, pdf)
    if left_story or right_story:
        raise RuntimeError(
            f"Page {page_number} overflow: left={len(left_story)} flowables, right={len(right_story)} flowables"
        )


def draw_detail_page(
    pdf: canvas.Canvas,
    entry: dict[str, Any],
    page_number: int,
    total_pages: int,
    st: dict[str, ParagraphStyle],
) -> None:
    if page_number % 2 != 1:
        raise RuntimeError(f"Detail page for {entry['creature_id']} must be odd, got {page_number}")
    draw_entry_header(pdf, entry, page_number, total_pages)
    left, right = entry_column_stories(entry, st)
    draw_two_column_story(pdf, left, right, page_number=page_number, top_mm=50)


def draw_major_page(
    pdf: canvas.Canvas,
    entry: dict[str, Any],
    page_number: int,
    total_pages: int,
    st: dict[str, ParagraphStyle],
) -> None:
    if page_number % 2 != 1:
        raise RuntimeError(f"Major detail page for {entry['creature_id']} must be odd, got {page_number}")
    draw_entry_header(pdf, entry, page_number, total_pages, dossier=True)
    left, right = major_story(entry, st)
    draw_two_column_story(pdf, left, right, page_number=page_number, top_mm=39)


def draw_habitat_index(pdf: canvas.Canvas, entries: list[dict[str, Any]], page_number: int, total_pages: int, st: dict[str, ParagraphStyle]) -> None:
    draw_page_base(pdf, page_number, total_pages, title="INDEX DES MILIEUX")
    x = 18 * mm
    pdf.setFillColor(GREEN_DARK)
    pdf.setFont("Helvetica-Bold", 23)
    pdf.drawString(x, PAGE_H - 27 * mm, "Index des milieux et des traces")
    categories = [
        ("Forêts et lisières", ("forêt", "lisière", "bois", "futaie", "racine", "clairière")),
        ("Landes, tourbières et eaux", ("lande", "tourb", "marais", "eau", "berge", "étang", "rivière", "bassin", "vanne", "polder")),
        ("Cendres, ruines et souterrains", ("cendre", "ruine", "crypte", "souterrain", "voûte", "tombe", "brûlé", "incendi", "citerne", "aqueduc", "galerie")),
        ("Reliefs et espaces ouverts", ("falaise", "crête", "ravine", "pente", "plateau", "prairie", "champ", "roche", "steppe", "col", "défilé", "terrasse", "carrière", "pont")),
    ]
    by_category: dict[str, dict[str, list[str]]] = defaultdict(lambda: defaultdict(list))
    for entry in entries:
        for habitat in entry["habitats"]:
            lowered = habitat.casefold()
            category = "Autres milieux"
            for label, keywords in categories:
                if any(keyword in lowered for keyword in keywords):
                    category = label
                    break
            by_category[category][entry["name"]].append(habitat)
    story: list[Any] = []
    ordered_labels = [label for label, _ in categories] + ["Autres milieux"]
    for label in ordered_labels:
        creatures = by_category.get(label)
        if not creatures:
            continue
        story.append(section(label, st))
        for creature_name in sorted(creatures, key=str.casefold):
            story.append(labelled(creature_name, compact(" ; ".join(creatures[creature_name]), 185), st, small=True))
    gap = 6 * mm
    usable = PAGE_W - 38 * mm
    col_w = (usable - gap) / 2
    y = 18 * mm
    height = PAGE_H - 54 * mm
    first = Frame(x, y, col_w, height, showBoundary=0, leftPadding=0, rightPadding=2, topPadding=0, bottomPadding=0)
    second = Frame(x + col_w + gap, y, col_w, height, showBoundary=0, leftPadding=2, rightPadding=0, topPadding=0, bottomPadding=0)
    first.addFromList(story, pdf)
    second.addFromList(story, pdf)
    if story:
        raise RuntimeError("Habitat index overflow")
    pdf.bookmarkPage("habitat-index")
    pdf.addOutlineEntry("Index des milieux", "habitat-index", level=0, closed=False)


def draw_sources_page(pdf: canvas.Canvas, manifest: dict[str, Any], page_number: int, total_pages: int, st: dict[str, ParagraphStyle]) -> None:
    draw_page_base(pdf, page_number, total_pages, title="RÉFÉRENCES ET STATUT")
    x = 20 * mm
    pdf.setFillColor(GREEN_DARK)
    pdf.setFont("Helvetica-Bold", 23)
    pdf.drawString(x, PAGE_H - 27 * mm, "Références, statut et diffusion")
    story: list[Any] = [
        rich_paragraph("<b>AUTORITÉS ÉPINGLÉES</b>", st["guide_section"]),
        labelled("Ruleset", f"{manifest['ruleset']['id']} · SHA-256 {manifest['ruleset']['sha256']}", st),
        labelled("Manuel", f"{manifest['player_handbook']['id']} · SHA-256 {manifest['player_handbook']['sha256']}", st),
        labelled("Direction artistique", f"{manifest['design_system']['id']} · SHA-256 {manifest['design_system']['sha256']}", st),
        rich_paragraph("<b>LIGNÉE DU CONTENU</b>", st["guide_section"]),
        paragraph("Gardien des Cendres et Sentinelle de cendre reprennent la Crypte des Cendres historique. Brumaille, le Rôdeur corrompu et le Gardien des Racines proviennent de l’héritage Forgotten Forest et restent à revalider. Les autres profils sont des propositions originales de ce volume.", st["guide"]),
        paragraph("Les gobelins, gnolls et orcs présentés ici sont des peuples humanoïdes sapients. Leurs capacités décrivent un métier, un équipement, un entraînement ou une charge locale : elles n’impliquent ni hostilité, ni alignement, ni aptitude innée déterminée par l’ascendance.", st["guide"]),
        rich_paragraph("<b>LIMITES ACTUELLES</b>", st["guide_section"]),
        paragraph("Aucune de ces fiches n’est encore implémentée dans le runtime Rust. Les statistiques sont conçues pour des prototypes ultérieurs et devront être confrontées à des tests réels, visuels et systémiques avant promotion. Les anciens points de vie et modèles 3D proxy ne sont pas repris comme vérité mécanique ou visuelle.", st["guide"]),
        rich_paragraph("<b>ILLUSTRATIONS</b>", st["guide_section"]),
        paragraph("Les planches sont des créations originales générées pour ce volume. Leur manifeste conserve outil, prompt, dimensions, empreinte, texte alternatif et direction artistique. La diffusion externe reste soumise à la vérification des conditions associées à LicenseRef-OpenAI-Generated-Output.", st["guide"]),
        rich_paragraph("<b>USAGE INTERACTIF</b>", st["guide_section"]),
        paragraph("La visionneuse autonome est générée depuis les mêmes JSON. Elle permet recherche, filtres par famille et milieu, et révélation volontaire des informations secrètes ; elle n’est pas encore intégrée au jeu.", st["guide"]),
    ]
    frame = Frame(x, 18 * mm, PAGE_W - 40 * mm, PAGE_H - 54 * mm, showBoundary=0, leftPadding=0, rightPadding=0, topPadding=0, bottomPadding=0)
    frame.addFromList(story, pdf)
    if story:
        raise RuntimeError("Sources page overflow")
    pdf.bookmarkPage("sources")
    pdf.addOutlineEntry("Références et statut", "sources", level=0, closed=False)


def draw_back_cover(pdf: canvas.Canvas, manifest: dict[str, Any], allow_placeholders: bool) -> None:
    art = load_art(ASSET_DIR / "cover.png", "Bestiaire OpenRPG Forge", allow_placeholders)
    pdf.drawImage(muted_cover_reader(art, PAGE_W, PAGE_H), 0, 0, PAGE_W, PAGE_H, mask="auto")
    pdf.setFillColor(colors.Color(0.04, 0.12, 0.09, alpha=0.74))
    pdf.rect(0, 0, PAGE_W, PAGE_H, stroke=0, fill=1)
    pdf.setFillColor(WHITE)
    pdf.setFont("Helvetica-Bold", 17)
    pdf.drawCentredString(PAGE_W / 2, 150 * mm, "CE QUE L’ON SAIT N’EST JAMAIS TOUT")
    pdf.setStrokeColor(BRONZE)
    pdf.line(55 * mm, 142 * mm, PAGE_W - 55 * mm, 142 * mm)
    pdf.setFont("Times-Italic", 12)
    lines = [
        "Une trace révèle un passage.",
        "Un comportement révèle un besoin.",
        "Une faiblesse révèle une histoire.",
    ]
    for index, line in enumerate(lines):
        pdf.drawCentredString(PAGE_W / 2, (128 - index * 9) * mm, line)
    pdf.setFont("Helvetica", 7.5)
    pdf.drawCentredString(PAGE_W / 2, 19 * mm, manifest["bestiary_id"])


def build_pdf(manifest: dict[str, Any], entries: list[dict[str, Any]], allow_placeholders: bool) -> tuple[Path, int]:
    PDF_OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    TMP_DIR.mkdir(parents=True, exist_ok=True)
    output = PDF_OUTPUT_DIR / "Bestiaire_OpenRPG_Forge.pdf"
    total_pages = 1 + 2 + sum(4 if entry["major"] else 2 for entry in entries) + 3
    st = styles()
    pdf = canvas.Canvas(str(output), pagesize=A4, pageCompression=1)
    pdf.setTitle(manifest["title"])
    pdf.setSubject(manifest["subtitle"] + " · proposition non implémentée")
    pdf.setAuthor("OpenRPG Forge")
    pdf.setCreator("scripts/Build-Bestiary.py · ReportLab")
    pdf.setKeywords("OpenRPG Forge, bestiaire, jeu de rôle, créatures, gobelins, gnolls, orcs, conception")

    page_number = 1
    draw_cover(pdf, manifest, allow_placeholders)
    pdf.showPage()
    page_number += 1
    draw_guide_page(pdf, page_number, total_pages, st, 1)
    pdf.showPage()
    page_number += 1
    draw_guide_page(pdf, page_number, total_pages, st, 2)
    pdf.showPage()
    page_number += 1

    for entry in entries:
        draw_plate_page(pdf, entry, entry["plates"][0], page_number, total_pages, allow_placeholders, 0)
        pdf.showPage()
        page_number += 1
        draw_detail_page(pdf, entry, page_number, total_pages, st)
        pdf.showPage()
        page_number += 1
        if entry["major"]:
            draw_plate_page(pdf, entry, entry["plates"][1], page_number, total_pages, allow_placeholders, 1)
            pdf.showPage()
            page_number += 1
            draw_major_page(pdf, entry, page_number, total_pages, st)
            pdf.showPage()
            page_number += 1

    draw_habitat_index(pdf, entries, page_number, total_pages, st)
    pdf.showPage()
    page_number += 1
    draw_sources_page(pdf, manifest, page_number, total_pages, st)
    pdf.showPage()
    page_number += 1
    draw_back_cover(pdf, manifest, allow_placeholders)
    pdf.showPage()
    page_number += 1
    if page_number - 1 != total_pages:
        raise RuntimeError(f"Expected {total_pages} pages, produced {page_number - 1}")
    pdf.save()
    actual_pages = len(PdfReader(str(output)).pages)
    if actual_pages != total_pages:
        raise RuntimeError(f"pypdf counted {actual_pages} pages, expected {total_pages}")
    return output, total_pages


def serializable_entry(entry: dict[str, Any]) -> dict[str, Any]:
    return {key: value for key, value in entry.items() if not key.startswith("_")}


def build_viewer(manifest: dict[str, Any], entries: list[dict[str, Any]], allow_placeholders: bool) -> Path:
    viewer = OUTPUT_DIR / "interactive"
    assets = viewer / "assets"
    assets.mkdir(parents=True, exist_ok=True)
    combined = {"manifest": manifest, "entries": [serializable_entry(entry) for entry in entries]}
    (viewer / "bestiary.json").write_text(json.dumps(combined, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    (viewer / "data.js").write_text("window.OPENRPG_BESTIARY = " + json.dumps(combined, ensure_ascii=False) + ";\n", encoding="utf-8")
    for entry in entries:
        for plate in entry["plates"]:
            source = ASSET_DIR / plate["image"]
            target = assets / plate["image"]
            if source.is_file():
                shutil.copy2(source, target)
            elif allow_placeholders:
                placeholder_image(entry["name"]).save(target)
    cover = ASSET_DIR / "cover.png"
    if cover.is_file():
        shutil.copy2(cover, assets / "cover.png")
    elif allow_placeholders:
        placeholder_image("Bestiaire OpenRPG Forge").save(assets / "cover.png")
    (viewer / "index.html").write_text(VIEWER_HTML, encoding="utf-8")
    (viewer / "styles.css").write_text(VIEWER_CSS, encoding="utf-8")
    (viewer / "app.js").write_text(VIEWER_JS, encoding="utf-8")
    return viewer / "index.html"


def write_build_report(
    manifest: dict[str, Any],
    entries: list[dict[str, Any]],
    pdf_path: Path | None,
    page_count: int | None,
    viewer_path: Path | None,
) -> Path:
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    plates = []
    for entry in entries:
        for plate in entry["plates"]:
            path = ASSET_DIR / plate["image"]
            plates.append(
                {
                    "creature_id": entry["creature_id"],
                    "file": plate["image"],
                    "sha256": sha256(path) if path.is_file() else None,
                    "alt": plate["alt"],
                }
            )
    report = {
        "build_id": manifest["bestiary_id"],
        "generated_at": datetime.now(timezone.utc).isoformat(),
        "status": manifest["status"],
        "release_wave": manifest.get("release_wave"),
        "entry_count": len(entries),
        "major_entry_count": sum(1 for entry in entries if entry["major"]),
        "plate_count": len(plates),
        "pdf": None if pdf_path is None else {"path": str(pdf_path.relative_to(ROOT)), "sha256": sha256(pdf_path), "pages": page_count},
        "viewer": None if viewer_path is None else {"path": str(viewer_path.relative_to(ROOT))},
        "sources": {
            "ruleset": manifest["ruleset"],
            "player_handbook": manifest["player_handbook"],
            "design_system": manifest["design_system"],
        },
        "plates": plates,
    }
    path = OUTPUT_DIR / "build-report.json"
    path.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return path


VIEWER_HTML = """<!doctype html>
<html lang="fr">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>Bestiaire OpenRPG Forge</title>
  <link rel="stylesheet" href="styles.css">
</head>
<body>
  <header class="masthead">
    <div>
      <p class="eyebrow">OPENRPG FORGE · VOLUME I</p>
      <h1>Bestiaire</h1>
      <p>Lisières, eaux et cendres</p>
    </div>
    <p class="status">Proposition de conception<br>Runtime Rust non implémenté</p>
  </header>
  <div class="shell">
    <aside class="catalogue" aria-label="Catalogue des créatures">
      <label for="search">Rechercher</label>
      <input id="search" type="search" placeholder="Nom, milieu, trace…">
      <label for="family">Famille</label>
      <select id="family"><option value="">Toutes les familles</option></select>
      <label for="habitat">Milieu</label>
      <select id="habitat"><option value="">Tous les milieux</option></select>
      <nav id="creature-list"></nav>
    </aside>
    <main id="entry" tabindex="-1"></main>
  </div>
  <script src="data.js"></script>
  <script src="app.js"></script>
</body>
</html>
"""


VIEWER_CSS = """:root {
  --green: #274c3b; --green-dark: #173428; --bronze: #a66f36;
  --parchment: #f5eedc; --paper: #fbf8f0; --slate: #496573; --ink: #2d2d2a;
  color-scheme: light;
}
* { box-sizing: border-box; }
body { margin: 0; background: var(--parchment); color: var(--ink); font-family: Georgia, "Times New Roman", serif; }
.masthead { min-height: 13rem; padding: 2.5rem clamp(1.25rem, 5vw, 5rem); display: flex; flex-wrap: wrap; gap: 2rem; align-items: end; justify-content: space-between; color: white; background: linear-gradient(100deg, rgba(23,52,40,.98), rgba(39,76,59,.86)), url("assets/cover.png") center 42%/cover; border-bottom: .35rem solid var(--bronze); }
.masthead h1 { margin: .1rem 0; font: 700 clamp(2.8rem, 7vw, 5.5rem)/.9 Arial, sans-serif; letter-spacing: .03em; text-transform: uppercase; }
.eyebrow, .status { font: 700 .76rem/1.4 Arial, sans-serif; letter-spacing: .12em; text-transform: uppercase; }
.status { text-align: right; max-width: 20rem; }
.shell { display: grid; grid-template-columns: minmax(16rem, 23rem) 1fr; min-height: calc(100vh - 13rem); }
.catalogue { padding: 1.5rem; background: var(--green-dark); color: white; position: sticky; top: 0; max-height: 100vh; overflow: auto; }
.catalogue label { display: block; margin: .8rem 0 .35rem; font: 700 .72rem Arial, sans-serif; letter-spacing: .1em; text-transform: uppercase; }
.catalogue input, .catalogue select { width: 100%; padding: .72rem; border: 1px solid #b99a6c; border-radius: .35rem; background: #fffdf6; color: var(--ink); }
.catalogue nav { display: grid; gap: .45rem; margin-top: 1.2rem; }
.creature-button { padding: .75rem; border: 1px solid rgba(255,255,255,.18); border-radius: .35rem; background: rgba(255,255,255,.06); color: white; text-align: left; cursor: pointer; }
.creature-button:hover, .creature-button[aria-current="true"] { border-color: var(--bronze); background: rgba(166,111,54,.28); }
.creature-button strong, .creature-button span { display: block; }
.creature-button span { margin-top: .2rem; color: #d7dfda; font: .72rem Arial, sans-serif; }
#entry { padding: clamp(1.25rem, 4vw, 4rem); max-width: 95rem; }
.hero { display: grid; grid-template-columns: minmax(18rem, .95fr) minmax(20rem, 1.05fr); gap: clamp(1.25rem, 3vw, 3rem); align-items: start; }
.plates { display: grid; gap: 1.5rem; align-self: start; }
.plate { margin: 0; }
.plate img { display: block; width: auto; max-width: 100%; height: auto; max-height: 78vh; margin: 0 auto; object-fit: contain; background: var(--paper); border: .35rem solid var(--green-dark); box-shadow: 0 1rem 2rem rgba(24,42,34,.18); }
.plate figcaption { margin-top: .65rem; color: var(--slate); font-style: italic; }
.entry-head { border-bottom: 2px solid var(--bronze); margin-bottom: 1.2rem; }
.entry-head h2 { margin: .1rem 0 .35rem; color: var(--green-dark); font: 700 clamp(2rem, 5vw, 4.3rem)/.95 Arial, sans-serif; text-transform: uppercase; }
.entry-head .subtitle { font-size: 1.15rem; color: var(--slate); }
.badges { display: flex; flex-wrap: wrap; gap: .45rem; margin: 1rem 0; }
.badge { padding: .35rem .6rem; border: 1px solid var(--bronze); border-radius: 99rem; font: 700 .68rem Arial, sans-serif; text-transform: uppercase; }
.columns { columns: 2 19rem; column-gap: 2rem; }
.section { break-inside: avoid; margin: 0 0 1.2rem; padding: 1rem; background: var(--paper); border-top: 3px solid var(--green); box-shadow: 0 .25rem .8rem rgba(24,42,34,.08); }
.section h3 { margin: 0 0 .65rem; color: var(--green-dark); font: 700 .92rem Arial, sans-serif; letter-spacing: .08em; text-transform: uppercase; }
.section p, .section li { line-height: 1.5; }
.action { margin: .75rem 0; padding: .75rem; border-left: 3px solid var(--bronze); background: #f4eddd; }
.stats { display: grid; grid-template-columns: repeat(auto-fit, minmax(6.5rem, 1fr)); gap: .4rem; }
.stat { padding: .55rem; background: #e8e4da; font: .8rem Arial, sans-serif; }
details { margin: .65rem 0; border: 1px solid var(--bronze); padding: .75rem; background: #fffaf0; }
summary { cursor: pointer; color: var(--green-dark); font-weight: 700; }
.empty { opacity: .7; padding: 2rem 0; }
@media (max-width: 900px) { .masthead { align-items: flex-start; flex-direction: column; } .status { display: block; margin: 0; max-width: none; text-align: left; } .shell { grid-template-columns: 1fr; } .catalogue { position: relative; max-height: none; } .hero { grid-template-columns: 1fr; } }
"""


VIEWER_JS = r"""(() => {
  const data = window.OPENRPG_BESTIARY;
  const list = document.querySelector('#creature-list');
  const entryNode = document.querySelector('#entry');
  const search = document.querySelector('#search');
  const family = document.querySelector('#family');
  const habitat = document.querySelector('#habitat');
  let selected = data.entries[0]?.creature_id;
  const escape = (value) => String(value ?? '').replace(/[&<>'"]/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;',"'":'&#39;','"':'&quot;'}[c]));
  const join = (items) => items.map(escape).join(', ');
  const section = (title, content) => `<section class="section"><h3>${escape(title)}</h3>${content}</section>`;
  const families = [...new Set(data.entries.map(e => e.classification.family))].sort((a,b) => a.localeCompare(b, 'fr'));
  families.forEach(value => family.insertAdjacentHTML('beforeend', `<option>${escape(value)}</option>`));
  const habitats = [...new Set(data.entries.flatMap(e => e.habitats))].sort((a,b) => a.localeCompare(b, 'fr'));
  habitats.forEach(value => habitat.insertAdjacentHTML('beforeend', `<option>${escape(value)}</option>`));
  function visibleEntries() {
    const needle = search.value.trim().toLocaleLowerCase('fr');
    return data.entries.filter(e => {
      const haystack = [e.name, e.subtitle, ...e.habitats, ...e.signs, e.classification.family].join(' ').toLocaleLowerCase('fr');
      const tokens = haystack.split(/[^\p{L}\p{N}]+/u).filter(Boolean);
      const searchMatches = !needle || (needle.length <= 3 ? tokens.some(token => token.startsWith(needle)) : haystack.includes(needle));
      return searchMatches && (!family.value || e.classification.family === family.value) && (!habitat.value || e.habitats.includes(habitat.value));
    });
  }
  function renderList() {
    const entries = visibleEntries();
    list.innerHTML = entries.map(e => `<button class="creature-button" data-id="${escape(e.creature_id)}" aria-current="${e.creature_id === selected}"><strong>${escape(e.name)}</strong><span>${escape(e.classification.family)} · menace ${escape(e.classification.threat)}</span></button>`).join('') || '<p class="empty">Aucune créature ne correspond.</p>';
    list.querySelectorAll('button').forEach(button => button.addEventListener('click', () => { selected = button.dataset.id; renderList(); renderEntry(); }));
  }
  function actions(e) {
    return [...e.rules.attacks, ...e.rules.abilities].map(a => `<div class="action"><strong>${escape(a.name)}</strong> · ${escape(a.visibility)}<br><small>${escape(a.skill_id)} + ${escape(a.attribute_id)} · Impact ${a.impact} · ${escape(a.reach)} · Endurance ${a.stamina_cost}</small><p><b>Télégraphe.</b> ${escape(a.telegraph)} <b>Effet.</b> ${escape(a.effect)} <b>Contre-jeu.</b> ${escape(a.counterplay)}</p></div>`).join('');
  }
  function plates(e) {
    return e.plates.map((plate, index) => `<figure class="plate"><img src="assets/${escape(plate.image)}" alt="${escape(plate.alt)}"><figcaption>${e.plates.length > 1 ? `<b>Planche ${index + 1}/${e.plates.length}.</b> ` : ''}${escape(plate.caption)}</figcaption></figure>`).join('');
  }
  function renderEntry() {
    const e = data.entries.find(item => item.creature_id === selected) || visibleEntries()[0];
    if (!e) { entryNode.innerHTML = '<p class="empty">Sélectionnez une créature.</p>'; return; }
    selected = e.creature_id;
    const attrs = Object.entries(e.rules.attributes).map(([key, value]) => `<div class="stat"><b>${escape(key)}</b> ${value}</div>`).join('');
    const resources = Object.entries(e.rules.resources).map(([key, value]) => `<div class="stat"><b>${escape(key)}</b> ${value}</div>`).join('');
    const publicKnowledge = e.field_knowledge.filter(k => k.status !== 'secret').map(k => `<p><b>${escape(k.status)} · ${escape(k.skill_id)} ${k.difficulty}</b><br>${escape(k.fact)}</p>`).join('');
    const secrets = e.field_knowledge.filter(k => k.status === 'secret').map(k => `<p><b>${escape(k.skill_id)} ${k.difficulty}</b><br>${escape(k.fact)}</p>`).join('');
    const dossier = e.major_dossier ? section('Dossier majeur', `<p>${escape(e.major_dossier.arena)}</p><ol>${e.major_dossier.sequence.map(x => `<li>${escape(x)}</li>`).join('')}</ol><ul>${e.major_dossier.causal_opportunities.map(x => `<li>${escape(x)}</li>`).join('')}</ul><p><b>Après.</b> ${escape(e.major_dossier.aftermath)}</p>`) : '';
    entryNode.innerHTML = `<article class="hero"><div class="plates" aria-label="Planches de ${escape(e.name)}">${plates(e)}</div><div><header class="entry-head"><p class="eyebrow">${escape(e.creature_id)}</p><h2>${escape(e.name)}</h2><p class="subtitle">${escape(e.subtitle)}</p><div class="badges"><span class="badge">${escape(e.classification.nature)}</span><span class="badge">${escape(e.classification.rarity)}</span><span class="badge">menace ${escape(e.classification.threat)}</span>${e.major ? '<span class="badge">dossier majeur</span>' : ''}</div></header><div class="columns">${section('Zoologie', `<p><b>Silhouette.</b> ${escape(e.physique.silhouette)}</p><p><b>Locomotion.</b> ${join(e.physique.locomotion)}. ${escape(e.physique.center_of_gravity)}</p><p><b>Régime.</b> ${escape(e.ecology.diet)}</p><p><b>Cycle.</b> ${escape(e.ecology.lifecycle)}</p><p><b>Comportement.</b> ${escape(e.ecology.behavior)}</p><p><b>Niche.</b> ${escape(e.ecology.niche)}</p>`)}${section('Milieux et traces', `<p><b>Habitats.</b> ${join(e.habitats)}</p><ul>${e.signs.map(x => `<li>${escape(x)}</li>`).join('')}</ul>`)}${section('Attributs et ressources', `<div class="stats">${attrs}${resources}</div><p><b>Déplacement.</b> ${escape(e.rules.movement)}</p>`)}${section('Actions', actions(e))}${section('Connaissances', publicKnowledge + (secrets ? `<details><summary>Révéler le dossier complet</summary>${secrets}</details>` : ''))}${section('Interactions', e.rules.environmental_interactions.map(x => `<p><b>${escape(x.input)} → ${escape(x.result)}</b><br>${escape(x.chain)}</p>`).join(''))}${section('Issues sans combat', `<ul>${e.encounter_use.noncombat_options.map(x => `<li>${escape(x)}</li>`).join('')}</ul>`)}${dossier}</div></div></article>`;
  }
  search.addEventListener('input', () => { renderList(); const first = visibleEntries()[0]; if (first && !visibleEntries().some(e => e.creature_id === selected)) selected = first.creature_id; renderEntry(); });
  family.addEventListener('change', () => { renderList(); const first = visibleEntries()[0]; if (first && !visibleEntries().some(e => e.creature_id === selected)) selected = first.creature_id; renderEntry(); });
  habitat.addEventListener('change', () => { renderList(); const first = visibleEntries()[0]; if (first && !visibleEntries().some(e => e.creature_id === selected)) selected = first.creature_id; renderEntry(); });
  renderList(); renderEntry();
})();
"""


def main() -> int:
    args = parse_args()
    manifest, entries = load_bestiary()
    validate_bundle(manifest, entries, args.allow_placeholders)
    pdf_path: Path | None = None
    viewer_path: Path | None = None
    page_count: int | None = None
    if not args.viewer_only:
        pdf_path, page_count = build_pdf(manifest, entries, args.allow_placeholders)
    if not args.pdf_only:
        viewer_path = build_viewer(manifest, entries, args.allow_placeholders)
    report = write_build_report(manifest, entries, pdf_path, page_count, viewer_path)
    print(json.dumps({"pdf": str(pdf_path) if pdf_path else None, "pages": page_count, "viewer": str(viewer_path) if viewer_path else None, "report": str(report)}, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
