#!/usr/bin/env python3
"""
Generate one markdown guide per language from a single source file.

Source files end with `.src.md`. Each one produces a guide per language,
next to it: `solution.src.md` -> `solution.csharp.md`, `solution.ts.md`.

In a source file:
- Text outside markers is kept in every generated guide.
- Text between `<!-- lang:csharp -->` and `<!-- /lang -->` is kept only
  in the C# guide. Several languages can share a block: `<!-- lang:csharp,ts -->`.
- `{{language}}` is replaced by the language name (C#, TypeScript).

Usage:
    python scripts/generate-guides.py            # generate every guide
    python scripts/generate-guides.py --check    # fail if a guide is out of date
    python scripts/generate-guides.py jour-1     # limit to a folder
"""

import argparse
import re
import sys
from pathlib import Path

LANGUAGES = {"csharp": "C#", "ts": "TypeScript"}
SOURCE_SUFFIX = ".src.md"

OPEN_MARKER = re.compile(r"^\s*<!--\s*lang:\s*([a-z,\s]+?)\s*-->\s*$")
CLOSE_MARKER = re.compile(r"^\s*<!--\s*/lang\s*-->\s*$")
EXTRA_BLANK_LINES = re.compile(r"\n{3,}")


class SourceError(Exception):
    pass


def render(source: Path, language: str) -> str:
    output = [f"<!-- Généré depuis {source.name}. Modifiez la source, pas ce fichier. -->\n\n"]
    block_languages = None
    block_start = 0

    for number, line in enumerate(source.read_text(encoding="utf-8").splitlines(keepends=True), start=1):
        if opening := OPEN_MARKER.match(line):
            if block_languages is not None:
                raise SourceError(f"{source}:{number}: bloc ouvert alors que celui de la ligne {block_start} n'est pas fermé")
            block_languages = {name.strip() for name in opening.group(1).split(",") if name.strip()}
            unknown = block_languages - LANGUAGES.keys()
            if unknown:
                raise SourceError(f"{source}:{number}: langage inconnu {sorted(unknown)}, attendus : {sorted(LANGUAGES)}")
            block_start = number
            continue

        if CLOSE_MARKER.match(line):
            if block_languages is None:
                raise SourceError(f"{source}:{number}: fermeture sans ouverture")
            block_languages = None
            continue

        if block_languages is None or language in block_languages:
            output.append(line.replace("{{language}}", LANGUAGES[language]))

    if block_languages is not None:
        raise SourceError(f"{source}:{block_start}: bloc jamais fermé")

    return EXTRA_BLANK_LINES.sub("\n\n", "".join(output))


def target_path(source: Path, language: str) -> Path:
    stem = source.name[: -len(SOURCE_SUFFIX)]
    return source.with_name(f"{stem}.{language}.md")


def main() -> int:
    parser = argparse.ArgumentParser(description="Generate per-language guides from *.src.md files.")
    parser.add_argument("root", nargs="?", default=".", help="folder to scan (default: current folder)")
    parser.add_argument("--check", action="store_true", help="do not write, fail if a guide is out of date")
    arguments = parser.parse_args()

    sources = sorted(Path(arguments.root).rglob(f"*{SOURCE_SUFFIX}"))
    if not sources:
        print("Aucun fichier source trouvé.")
        return 0

    outdated = []
    try:
        for source in sources:
            for language in LANGUAGES:
                target = target_path(source, language)
                content = render(source, language)
                current = target.read_text(encoding="utf-8") if target.exists() else None
                if content == current:
                    continue
                if arguments.check:
                    outdated.append(target)
                else:
                    target.write_text(content, encoding="utf-8")
                    print(f"Généré : {target}")
    except SourceError as error:
        print(f"Erreur : {error}", file=sys.stderr)
        return 1

    if outdated:
        print("Guides pas à jour, lancez `python scripts/generate-guides.py` :", file=sys.stderr)
        for target in outdated:
            print(f"  {target}", file=sys.stderr)
        return 1

    if arguments.check:
        print("Tous les guides sont à jour.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
