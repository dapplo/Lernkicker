#!/usr/bin/env python3
"""Checks quizzes (quiz/v1) and learning material (lesson/v1) against their schemas and content rules.

Usage: python3 tools/validate_quizzes.py [FILE ...]
Without arguments all quizzes/**/*.json and lessons/**/*.json are checked. The "format" field picks the
schema. Pictures named in "src" must exist next to the JSON file (same rules as the upload). Exit code 1 on errors.
"""
import json
import sys
import unicodedata
import xml.etree.ElementTree as ElementTree
from pathlib import Path

from jsonschema import Draft202012Validator

ROOT = Path(__file__).resolve().parent.parent
VALIDATORS = {
    name: Draft202012Validator(json.loads((ROOT / "schema" / f"{name.replace('/', '-')}.schema.json").read_text(encoding="utf-8")))
    for name in ("quiz/v1", "lesson/v1")
}
GAP = "___"
IMAGE_KEYS = ("image", "frontImage", "backImage")
MAX_GRID_SIZE = 14
MAX_IMAGE_BYTES = 2 * 1024 * 1024
MAX_TOTAL_IMAGE_BYTES = 20 * 1024 * 1024


def content_errors(quiz):
    """Rules JSON Schema cannot express (same as QuizRules in Lernkicker.Domain)."""
    errors = []
    seen = set()
    for i, e in enumerate(quiz.get("exercises", [])):
        where = f"exercises/{i}"
        if e.get("id") in seen:
            errors.append(f"{where}/id: duplicate id '{e.get('id')}'")
        seen.add(e.get("id"))
        kind = e.get("type")
        if kind == "cloze":
            gaps = e["text"].count(GAP)
            if gaps != len(e["gaps"]):
                errors.append(f"{where}/gaps: text has {gaps} gaps (___) but gaps has {len(e['gaps'])} entries")
            bank = e.get("wordBank")
            if bank:
                for j, g in enumerate(e["gaps"]):
                    if not any(a in bank for a in g["answers"]):
                        errors.append(f"{where}/gaps/{j}: no answer is in wordBank")
        elif kind == "matching":
            for j, r in enumerate(e["rows"]):
                if r["answer"] not in e["options"]:
                    errors.append(f"{where}/rows/{j}/answer: '{r['answer']}' is not one of the options")
        elif kind == "multipleChoice":
            for j, q in enumerate(e["questions"]):
                correct = q["correct"] if isinstance(q["correct"], list) else [q["correct"]]
                if any(c >= len(q["options"]) for c in correct):
                    errors.append(f"{where}/questions/{j}/correct: points to an option that does not exist")
        elif kind == "fallingWords":
            for j, w in enumerate(e["words"]):
                if w["answer"] not in e["options"]:
                    errors.append(f"{where}/words/{j}/answer: '{w['answer']}' is not one of the options")
        elif kind == "wordSearch":
            longest = e.get("size", MAX_GRID_SIZE)
            seen_words = set()
            for j, w in enumerate(e["words"]):
                letters = unicodedata.normalize("NFC", w)
                if not letters.isalpha():
                    errors.append(f"{where}/words/{j}: '{w}' may only contain letters")
                elif len(letters) > longest:
                    errors.append(f"{where}/words/{j}: '{w}' is longer than the grid ({longest} letters)")
                if letters.lower() in seen_words:
                    errors.append(f"{where}/words/{j}: '{w}' is in the word list more than once")
                seen_words.add(letters.lower())
    return errors


def lesson_errors(lesson):
    """Rules JSON Schema cannot express (same as LessonRules in Lernkicker.Domain)."""
    errors = []
    seen = set()
    for i, b in enumerate(lesson.get("blocks", [])):
        if b.get("id") in seen:
            errors.append(f"blocks/{i}/id: duplicate id '{b.get('id')}'")
        seen.add(b.get("id"))
    return errors


def images(node, where=""):
    """Every picture object with its JSON path (same as Quiz.Images and Lesson.Images)."""
    if isinstance(node, dict):
        for key, value in node.items():
            at = f"{where}/{key}" if where else key
            if key in IMAGE_KEYS and isinstance(value, dict):
                yield at, value
            else:
                yield from images(value, at)
    elif isinstance(node, list):
        for i, value in enumerate(node):
            yield from images(value, f"{where}/{i}")


def is_svg(data):
    """An SVG document (the upload removes scripts and outside links from it, see SvgSanitizer)."""
    try:
        return ElementTree.fromstring(data).tag == "{http://www.w3.org/2000/svg}svg"
    except ElementTree.ParseError:
        return False


def is_picture(head):
    """PNG, JPEG, GIF or WebP by the first bytes (same as ImageFormats.Detect)."""
    return (head.startswith(b"\x89PNG\r\n\x1a\n") or head.startswith(b"\xff\xd8\xff")
            or head[:6] in (b"GIF87a", b"GIF89a") or (head[:4] == b"RIFF" and head[8:12] == b"WEBP"))


def image_errors(document, folder):
    errors = []
    total = 0
    for where, image in images(document):
        file = folder / image["src"]
        if not file.is_file():
            errors.append(f"{where}/src: file '{image['src']}' not found next to the JSON file")
            continue
        data = file.read_bytes()
        total += len(data)
        if not is_picture(data[:12]) and not is_svg(data):
            errors.append(f"{where}/src: '{image['src']}' is not a PNG, JPEG, GIF, WebP or SVG picture")
        if len(data) > MAX_IMAGE_BYTES:
            errors.append(f"{where}/src: '{image['src']}' is larger than 2 MB")
    if total > MAX_TOTAL_IMAGE_BYTES:
        errors.append("pictures together are larger than 20 MB")
    return errors


def check(path):
    try:
        document = json.loads(path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as e:
        return [f"invalid JSON: {e}"]
    kind = document.get("format") if isinstance(document, dict) else None
    validator = VALIDATORS.get(kind)
    if validator is None:
        return [f"format: must be one of {', '.join(VALIDATORS)}, not {kind!r}"]
    errors = [
        f"{'/'.join(map(str, e.absolute_path)) or '(root)'}: {e.message}"
        for e in sorted(validator.iter_errors(document), key=lambda e: list(map(str, e.absolute_path)))
    ]
    # Content rules only make sense once the structure is valid
    if errors:
        return errors
    rules = content_errors(document) if kind == "quiz/v1" else lesson_errors(document)
    return rules + image_errors(document, path.parent)


def main(args):
    files = [Path(a) for a in args] or sorted([*(ROOT / "quizzes").rglob("*.json"), *(ROOT / "lessons").rglob("*.json")])
    failed = 0
    for f in files:
        errors = check(f)
        if errors:
            failed += 1
            print(f"ERROR {f}")
            for e in errors:
                print(f"  - {e}")
        else:
            print(f"ok    {f}")
    print(f"{len(files)} file(s) checked, {failed} with errors.")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
