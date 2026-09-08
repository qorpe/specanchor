#!/usr/bin/env bash
# Docs freshness: this repository's documents must still describe this repository.
#
# specanchor's whole argument to a regulated client is that unvalidated output cannot
# pass the gate. A doc set that quietly disagrees with the code is unvalidated output.
# The documents here are not commentary — the schemas are the constitution, REVISIONS.md
# is the ledger of what is owed, and the README's status is what a reader believes about
# how far the chain has been built. Each gate below checks one of those against reality.
#
# Exit 0 clean, 1 stale.
set -uo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
STATUS=0

python3 - "$ROOT" <<'PY'
import pathlib, re, sys

root = pathlib.Path(sys.argv[1])
readme = (root / "README.md").read_text()
revisions = (root / "docs/REVISIONS.md").read_text()
cli = (root / "core/cli/SpecAnchor.Cli/Program.cs").read_text()
stryker = (root / "stryker-config.json").read_text()

bad = []
NUMBERS = {
    "one": 1, "two": 2, "three": 3, "four": 4, "five": 5,
    "six": 6, "seven": 7, "eight": 8, "nine": 9, "ten": 10,
}


def fail(gate, message):
    bad.append(f"  [{gate}] {message}")


# G1 — the counted claims. A README that says "the six artefact schemas" is making a
# checkable statement; when a seventh lands, the sentence becomes wrong silently.
#
# Only claims the repository actually RECORDS are gated. "All four discovery skill
# contracts" is not one of them: six directories live under core/skills and nothing in
# the tree says which of them are discovery skills rather than delivery skills, so a
# gate counting directories would report a drift that is not there. It was written that
# way once and did exactly that. The engine never guesses — that rule applies to this
# script as much as to the indexers.
counts = [
    ("artefact schemas", len(list((root / "core/schemas").glob("*.schema.v1.json"))),
     r"the (\w+) artefact schemas"),
    ("planted traps in the rig",
     len(re.findall(r"^## Trap [A-Z]", (root / "rig/legacy-factoring/TRAPS.md").read_text(), re.M)),
     r"(\w+) planted traps"),
]
for label, actual, pattern in counts:
    for word in re.findall(pattern, readme):
        claimed = NUMBERS.get(word.lower())
        if claimed is None:
            fail("G1", f"README writes '{word}' where a count of {label} belongs — not a number this gate can check.")
        elif claimed != actual:
            fail("G1", f"README claims {word} ({claimed}) {label}; the repository has {actual}.")

# G2 — nothing the ledger calls owed has quietly shipped. A ledger that still asks for
# work already done is how a team builds the same thing twice, or trusts a row that lies.
# Scoped to ONE table row: `.` must not cross a newline here, or "license gate" on
# row 12 pairs with "is still owed" on row 14 and reports a drift that is not there.
owed = {
    "the license gate": ("scripts/license-check.sh", r"license gate[^\n]*?is still owed"),
}
for what, (artefact, pattern) in owed.items():
    if re.search(pattern, revisions, re.I) and (root / artefact).exists():
        fail("G2", f"docs/REVISIONS.md still calls {what} owed, but {artefact} exists and CI runs it.")

# G3 — the README's "Next:" is not something already built. This is the sentence a reader
# uses to judge how far the chain got, so a stale one misstates the product's maturity.
nxt = re.search(r"\bNext: (.+?)\.", readme, re.S)
if nxt:
    claimed_next = " ".join(nxt.group(1).split())
    shipped = {
        "the gates": (root / "core/gates/SpecAnchor.Gates").exists(),
        "the CLI": (root / "core/cli/SpecAnchor.Cli").exists(),
        "MCP": '"mcp"' in cli or 'case "mcp"' in cli,
    }
    for phrase, is_built in shipped.items():
        bare = phrase.replace("the ", "")
        if is_built and re.search(rf"\b{re.escape(bare)}\b", claimed_next, re.I):
            fail("G3", f"README's 'Next:' still names {phrase}, which is built and exercised in CI.")

# G4 — every CLI verb the code dispatches is a verb the docs know about.
verbs = set(re.findall(r'case "([a-z][a-z-]*)":', cli))
docs_text = readme + "\n".join(p.read_text() for p in (root / "docs").glob("*.md"))
undocumented = sorted(v for v in verbs if not re.search(rf"specanchor {re.escape(v)}\b", docs_text))
if undocumented:
    fail("G4", f"the CLI dispatches {', '.join(undocumented)} but no document names `specanchor <verb>` for it.")

# G5 — a mutation threshold quoted in prose is the one stryker enforces.
configured = re.search(r'"break"\s*:\s*([0-9]+)', stryker)
for doc in [root / "README.md", root / "docs/REVISIONS.md", root / "CLAUDE.md"]:
    if not doc.exists():
        continue
    for quoted in re.findall(r"break (?:at )?([0-9]{2})\b", doc.read_text(), re.I):
        if configured and quoted != configured.group(1):
            fail("G5", f"{doc.name} quotes a mutation break of {quoted}; stryker-config.json enforces {configured.group(1)}.")

# G6 — the artefacts are written in English. This repository is read by client-side
# reviewers and auditors who do not share the team's first language; a section they
# cannot read is a section they cannot approve, which defeats the auditable trail.
TURKISH = re.compile(r"[çğışöüÇĞİŞÖÜ]")
COMMON = re.compile(
    r"\b(ve|için|ile|olarak|bir|bu|şu|değil|gibi|ama|çünkü|sonra|önce|"
    r"yapılır|yazılır|edilir|olur|var|yok|kadar|daha|hem|ya da)\b", re.I)
# Judged per document, not per line: a Turkish sentence wraps across several short lines
# and no single one of them carries enough signal. A stray loan word is not a finding —
# the README says "1-kuruş" and is entirely English — so a document needs BOTH the
# diacritics and three common Turkish function words before this fires.
for doc in sorted(root.glob("*.md")) + sorted((root / "docs").glob("*.md")):
    lines = doc.read_text().splitlines()
    text = "\n".join(lines)
    if TURKISH.search(text) and len(COMMON.findall(text)) >= 3:
        first = next((i for i, l in enumerate(lines, 1) if TURKISH.search(l) and COMMON.search(l)), 1)
        rel = doc.relative_to(root)
        fail("G6", f"{rel}:{first} is not written in English — every artefact in this repository is.")

# G7 — the README's CLI block matches the code's Usage block flag for flag. Naming the
# verb is not enough: the first draft of this README documented `index` without its
# required --out and called scaffold's --rule a rule id when it takes a file path. Both
# read plausibly and both would have sent a user into an exit-2 they could not explain.
usage = re.search(r'const string Usage = """(.*?)""";', cli, re.S)
if usage:
    def invocations(text):
        found = {}
        for line in text.splitlines():
            m = re.match(r"\s*specanchor ([a-z][a-z-]*)(.*)$", line)
            if m:
                found[m.group(1)] = " ".join(m.group(2).split())
        return found

    from_code = invocations(usage.group(1))
    from_readme = invocations(readme)
    for verb, flags in from_code.items():
        if verb not in from_readme:
            fail("G7", f"the README's CLI block does not show `specanchor {verb}`.")
        elif from_readme[verb] != flags:
            fail("G7", f"`specanchor {verb}` — README shows '{from_readme[verb]}', "
                       f"the CLI's own usage says '{flags}'.")

if bad:
    print("docs-freshness: STALE")
    print("\n".join(bad))
    sys.exit(1)
print("docs-freshness: the documents still describe the repository (7 gates).")
PY
STATUS=$(( STATUS + $? ))

exit $(( STATUS > 0 ? 1 : 0 ))
