#!/usr/bin/env python3
"""Prüft Übungsproben gegen schema/probe-v1.schema.json und auf inhaltliche Fehler.

Aufruf: python3 tools/validate_proben.py [DATEI ...]
Ohne Argumente werden alle proben/**/*.json geprüft. Exit-Code 1 bei Fehlern.
"""
import json
import sys
from pathlib import Path

from jsonschema import Draft202012Validator

ROOT = Path(__file__).resolve().parent.parent
SCHEMA = json.loads((ROOT / "schema" / "probe-v1.schema.json").read_text(encoding="utf-8"))
VALIDATOR = Draft202012Validator(SCHEMA)
LUECKE = "___"


def semantic_errors(probe):
    """Regeln, die JSON Schema nicht ausdrücken kann."""
    errors = []
    seen = set()
    for i, a in enumerate(probe.get("aufgaben", [])):
        where = f"aufgaben[{i}] ({a.get('id', '?')})"
        if a.get("id") in seen:
            errors.append(f"{where}: id kommt doppelt vor")
        seen.add(a.get("id"))
        typ = a.get("typ")
        if typ == "luecke":
            gaps = a["text"].count(LUECKE)
            if gaps != len(a["luecken"]):
                errors.append(f"{where}: {gaps} Lücken (___) im Text, aber {len(a['luecken'])} Einträge in luecken")
            bank = a.get("wortspeicher")
            if bank:
                for j, l in enumerate(a["luecken"]):
                    if not any(x in bank for x in l["antworten"]):
                        errors.append(f"{where}: luecken[{j}] hat keine Antwort aus dem wortspeicher")
        elif typ == "zuordnen":
            for j, z in enumerate(a["zeilen"]):
                if z["antwort"] not in a["optionen"]:
                    errors.append(f"{where}: zeilen[{j}].antwort '{z['antwort']}' steht nicht in optionen")
        elif typ == "auswahl":
            for j, f in enumerate(a["fragen"]):
                richtig = f["richtig"] if isinstance(f["richtig"], list) else [f["richtig"]]
                if any(r >= len(f["optionen"]) for r in richtig):
                    errors.append(f"{where}: fragen[{j}].richtig zeigt auf eine Option, die es nicht gibt")
    return errors


def check(path):
    try:
        probe = json.loads(path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as e:
        return [f"kein gültiges JSON: {e}"]
    errors = [
        f"{'/'.join(map(str, e.absolute_path)) or '(Wurzel)'}: {e.message}"
        for e in sorted(VALIDATOR.iter_errors(probe), key=lambda e: list(map(str, e.absolute_path)))
    ]
    # Inhaltliche Prüfung nur, wenn die Struktur stimmt
    return errors or semantic_errors(probe)


def main(args):
    files = [Path(a) for a in args] or sorted((ROOT / "proben").rglob("*.json"))
    failed = 0
    for f in files:
        errors = check(f)
        if errors:
            failed += 1
            print(f"FEHLER {f}")
            for e in errors:
                print(f"  - {e}")
        else:
            print(f"ok     {f}")
    print(f"{len(files)} Probe(n) geprüft, {failed} mit Fehlern.")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
