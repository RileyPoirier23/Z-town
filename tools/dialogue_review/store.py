"""Reads and writes the reviewable content: dialogue/*.yaml lines and parody names
(data/brands.json, data/tv/shows.json). Shared by serve.py and export.py."""
import json
import os
import re

import yaml

GAME = os.path.normpath(os.path.join(os.path.dirname(__file__), "..", "..", "game"))
DIALOGUE = os.path.join(GAME, "dialogue")
BRANDS = os.path.join(GAME, "data", "brands.json")
SHOWS = os.path.join(GAME, "data", "tv", "shows.json")
STATUSES = ("draft", "approved", "rejected")
FIELDS = ("id", "speaker", "text", "context", "status", "notes")


def _q(s):
    # JSON strings are valid YAML double-quoted scalars, and keep emoji/accents readable
    return json.dumps(str(s), ensure_ascii=False)


def load_lines():
    out = []
    for name in sorted(os.listdir(DIALOGUE)):
        if not name.endswith(".yaml"):
            continue
        with open(os.path.join(DIALOGUE, name), encoding="utf-8") as f:
            doc = yaml.safe_load(f) or {}
        default = doc.get("speaker", "")
        for line in doc.get("lines", []) or []:
            out.append({
                "file": name,
                "id": line.get("id", ""),
                "speaker": line.get("speaker") or default,
                "text": line.get("text", ""),
                "context": line.get("context", ""),
                "status": line.get("status", "draft"),
                "notes": line.get("notes", ""),
                "placeholder": str(line.get("text", "")).lstrip().startswith("["),
            })
    return out


def _header(text):
    head = []
    for l in text.splitlines():
        if l.startswith("#"):
            head.append(l)
        else:
            break
    return head


def save_file(name, lines):
    """Rewrites one dialogue file in a stable format (keeps its top comment block)."""
    path = os.path.join(DIALOGUE, name)
    with open(path, encoding="utf-8") as f:
        raw = f.read()
    doc = yaml.safe_load(raw) or {}
    default = doc.get("speaker", "")
    out = _header(raw)
    if default:
        out.append(f"speaker: {default}")
    out.append("lines:")
    for i, l in enumerate(lines):
        if i:
            out.append("")
        out.append(f"  - id: {l['id']}")
        if l.get("speaker") and l["speaker"] != default:
            out.append(f"    speaker: {l['speaker']}")
        out.append(f"    text: {_q(l.get('text', ''))}")
        out.append(f"    context: {_q(l.get('context', ''))}")
        out.append(f"    status: {l.get('status', 'draft')}")
        if l.get("notes"):
            out.append(f"    notes: {_q(l['notes'])}")
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(out) + "\n")


def update_line(file, id, **changes):
    lines = [l for l in load_lines() if l["file"] == file]
    hit = next((l for l in lines if l["id"] == id), None)
    if hit is None:
        raise KeyError(f"{file}: {id}")
    for k in ("text", "status", "notes"):
        if k in changes and changes[k] is not None:
            if k == "status" and changes[k] not in STATUSES:
                raise ValueError(f"bad status {changes[k]}")
            hit[k] = changes[k]
    save_file(file, lines)
    return hit


def _read_json(path):
    with open(path, encoding="utf-8") as f:
        text = re.sub(r"^\s*//.*$", "", f.read(), flags=re.M)
    return json.loads(text)


def _write_json(path, items):
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write("[\n" + ",\n".join("  " + json.dumps(i, ensure_ascii=False) for i in items) + "\n]\n")


def load_names():
    names = []
    for b in _read_json(BRANDS):
        names.append({"kind": "brand", "id": b["id"], "name": b["name"], "status": b.get("status", "draft"),
                      "context": b.get("kind", ""), "notes": b.get("notes", "")})
    for s in _read_json(SHOWS):
        names.append({"kind": "show", "id": s["id"], "name": s["name"], "status": s.get("nameStatus", "draft"),
                      "context": s.get("blurb", ""), "notes": ""})
    return names


def update_name(kind, id, name=None, status=None, notes=None):
    if status is not None and status not in STATUSES:
        raise ValueError(f"bad status {status}")
    path, status_key = (BRANDS, "status") if kind == "brand" else (SHOWS, "nameStatus")
    items = _read_json(path)
    hit = next((i for i in items if i["id"] == id), None)
    if hit is None:
        raise KeyError(f"{kind}: {id}")
    if name is not None:
        hit["name"] = name
    if status is not None:
        hit[status_key] = status
    if notes is not None and kind == "brand":
        hit["notes"] = notes
    _write_json(path, items)
    return hit
