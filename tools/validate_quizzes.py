#!/usr/bin/env python3
"""Checks quizzes (quiz/v1) and learning material (lesson/v1) against their schemas and content rules.

Usage: python3 tools/validate_quizzes.py [FILE ...]
Without arguments all quizzes/**/*.json and lessons/**/*.json are checked. The "format" field picks the
schema. Exit code 1 on errors.
"""
import json
import sys
from pathlib import Path

from jsonschema import Draft202012Validator

ROOT = Path(__file__).resolve().parent.parent
VALIDATORS = {
    name: Draft202012Validator(json.loads((ROOT / "schema" / f"{name.replace('/', '-')}.schema.json").read_text(encoding="utf-8")))
    for name in ("quiz/v1", "lesson/v1")
}
GAP = "___"


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
    return errors or (content_errors(document) if kind == "quiz/v1" else lesson_errors(document))


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
