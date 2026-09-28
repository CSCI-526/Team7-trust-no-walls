"""Builds design/Trust-No-Wall-Descriptive-Document.docx from design/DesignDocument.md.

The markdown file is the single source of truth, so the Word copy always matches the
GitHub copy. Supports the subset the document uses: #/##/### headings, paragraphs,
- bullets, 1. numbered items, pipe tables, **bold** spans, and ![alt](path) images.

Usage: python tools/build-descriptive-doc.py   (requires python-docx)
"""
import re
from pathlib import Path

from docx import Document
from docx.enum.table import WD_TABLE_ALIGNMENT
from docx.oxml import OxmlElement
from docx.oxml.ns import qn
from docx.shared import Inches, Pt

ROOT = Path(__file__).resolve().parent.parent
SRC = ROOT / "design" / "DesignDocument.md"
OUT = ROOT / "design" / "Trust-No-Wall-Descriptive-Document.docx"

BOLD = re.compile(r"\*\*(.+?)\*\*")
IMAGE = re.compile(r"!\[[^\]]*\]\(([^)]+)\)")


def add_runs(paragraph, text, size=None):
    pos = 0
    for m in BOLD.finditer(text):
        if m.start() > pos:
            r = paragraph.add_run(text[pos:m.start()])
            if size:
                r.font.size = Pt(size)
        r = paragraph.add_run(m.group(1))
        r.bold = True
        if size:
            r.font.size = Pt(size)
        pos = m.end()
    if pos < len(text):
        r = paragraph.add_run(text[pos:])
        if size:
            r.font.size = Pt(size)


def shade(cell, fill):
    tc_pr = cell._tc.get_or_add_tcPr()
    shd = OxmlElement("w:shd")
    shd.set(qn("w:val"), "clear")
    shd.set(qn("w:color"), "auto")
    shd.set(qn("w:fill"), fill)
    tc_pr.append(shd)


def add_table(doc, rows):
    cells = [[c.strip() for c in r.strip().strip("|").split("|")] for r in rows]
    header, body = cells[0], cells[2:]
    size = 9 if len(header) > 3 else 10
    t = doc.add_table(rows=1, cols=len(header))
    t.style = "Table Grid"
    t.alignment = WD_TABLE_ALIGNMENT.CENTER
    for i, h in enumerate(header):
        c = t.rows[0].cells[i]
        c.text = ""
        r = c.paragraphs[0].add_run(h)
        r.bold = True
        r.font.size = Pt(size)
        shade(c, "D9E2F3")
    for row in body:
        rc = t.add_row().cells
        for i, val in enumerate(row[: len(header)]):
            rc[i].text = ""
            add_runs(rc[i].paragraphs[0], val, size)
    doc.add_paragraph()


def build():
    doc = Document()
    doc.styles["Normal"].font.name = "Calibri"
    doc.styles["Normal"].font.size = Pt(11)
    for section in doc.sections:
        section.left_margin = section.right_margin = Inches(0.8)
        section.top_margin = section.bottom_margin = Inches(0.8)

    lines = SRC.read_text(encoding="utf-8").splitlines()
    i = 0
    while i < len(lines):
        line = lines[i].rstrip()
        if not line:
            i += 1
            continue
        if line.startswith("|"):
            block = []
            while i < len(lines) and lines[i].startswith("|"):
                block.append(lines[i])
                i += 1
            add_table(doc, block)
            continue
        m = IMAGE.fullmatch(line.strip())
        if m:
            doc.add_picture(str((SRC.parent / m.group(1)).resolve()), width=Inches(6.6))
        elif line.startswith("### "):
            doc.add_heading(line[4:], level=2)
        elif line.startswith("## "):
            doc.add_heading(line[3:], level=1)
        elif line.startswith("# "):
            doc.add_heading(line[2:], level=0)
        elif line.startswith("- "):
            add_runs(doc.add_paragraph(style="List Bullet"), line[2:])
        elif re.match(r"\d+\. ", line):
            add_runs(doc.add_paragraph(style="List Number"), line.split(". ", 1)[1])
        else:
            add_runs(doc.add_paragraph(), line)
        i += 1

    doc.save(OUT)
    print(f"wrote {OUT}")


if __name__ == "__main__":
    build()
