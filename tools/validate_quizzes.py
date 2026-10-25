#!/usr/bin/env python3
"""Checks practice quizzes against schema/quiz-v1.schema.json and the content rules.

Usage: python3 tools/validate_quizzes.py [FILE ...]
Without arguments all quizzes/**/*.json are checked. Exit code 1 on errors.
"""
import json
import sys
from pathlib import Path

from jsonschema import Draft202012Validator

ROOT = Path(__file__).resolve().parent.parent
SCHEMA = json.loads((ROOT / "schema" / "quiz-v1.schema.json").read_text(encoding="utf-8"))
VALIDATOR = Draft202012Validator(SCHEMA)
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


def check(path):
    try:
        quiz = json.loads(path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as e:
        return [f"invalid JSON: {e}"]
    errors = [
        f"{'/'.join(map(str, e.absolute_path)) or '(root)'}: {e.message}"
        for e in sorted(VALIDATOR.iter_errors(quiz), key=lambda e: list(map(str, e.absolute_path)))
    ]
    # Content rules only make sense once the structure is valid
    return errors or content_errors(quiz)


def main(args):
    files = [Path(a) for a in args] or sorted((ROOT / "quizzes").rglob("*.json"))
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
    print(f"{len(files)} quiz(zes) checked, {failed} with errors.")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
