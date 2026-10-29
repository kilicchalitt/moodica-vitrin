#!/usr/bin/env python3
"""Vitrindeki Markdown dosyalarının göreli bağlantılarını ve sayfa içi çapaları denetler (vitrin CI'ı).

Kullanım:  python .github/scripts/baglanti_denetimi.py [kök]   — kırık bağlantı varsa 1 ile çıkar.
"""
from __future__ import annotations

import re
import sys
import unicodedata
from pathlib import Path

LINK = re.compile(r"!?\[[^\]]*\]\(([^)\s]+)\)")
ANCHOR = re.compile(r'<a id="([^"]+)">')
HEADING = re.compile(r"^#{1,6}\s+(.+?)\s*$", re.MULTILINE)


def slug(heading: str) -> str:
    """GitHub'ın başlık çapası: küçük harf, noktalama silinir, boşluk tire olur."""
    text = unicodedata.normalize("NFC", heading).strip().lower()
    text = re.sub(r"[^\w\- ]", "", text)
    return text.replace(" ", "-")


def anchors(text: str) -> set[str]:
    return {slug(h) for h in HEADING.findall(text)} | set(ANCHOR.findall(text))


def check(root: Path) -> list[str]:
    errors = []
    for md in sorted(root.rglob("*.md")):
        if ".git" in md.parts:
            continue
        text = md.read_text(encoding="utf-8")
        for target in LINK.findall(text):
            if re.match(r"^[a-z]+:", target):
                continue  # http(s), mailto
            path, _, fragment = target.partition("#")
            file = (md.parent / path).resolve() if path else md
            if path and not file.exists():
                errors.append(f"{md.relative_to(root)}: dosya yok: {target}")
            elif fragment and file.suffix == ".md" and fragment not in anchors(file.read_text(encoding="utf-8")):
                errors.append(f"{md.relative_to(root)}: çapa yok: {target}")
    return errors


def main() -> int:
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")
    root = Path(sys.argv[1] if len(sys.argv) > 1 else ".").resolve()
    errors = check(root)
    for error in errors:
        print(error)
    print(f"{'kırık bağlantı: ' + str(len(errors)) if errors else 'bütün bağlantılar geçerli'}")
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
