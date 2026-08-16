---
name: domain-ledger
description: Build the glossary (ubiquitous language) from the rule catalog and the index identifiers. A glossary without aliases_in_code is decoration; every alias must resolve to a real identifier and every term must appear in at least one rule.
---

# domain-ledger

## Input

- The rule catalog of one bounded context.
- The identifier inventory from both indexes (type, member, table, column,
  procedure, trigger names). Never the raw repository.

## Output

One entry per term per context, conforming to
`core/schemas/ledger-term.schema.v1.json`, written to
`discovery/<context>/language.yaml`.

## Procedure

1. For every domain concept the rules use, write a definition of at most two
   sentences; the structured fields carry the rest.
2. `aliases_in_code` is the point of the artefact: list every identifier the
   concept hides behind — classes, methods, tables, columns. One concept
   appearing under two names is ONE entry with both aliases, not two entries.
3. When the same word names a different concept in another context, fill
   `not_to_be_confused_with` naming the other context explicitly. This is how
   an agent (and a new team member) avoids the false-friend trap.
4. `status` is born `proposed`. Only the domain expert moves it to
   `confirmed_by_expert` — never you.

## Self-validation — non-negotiable

Run every entry through the artefact validator before returning:

- **SA0201 (alias resolves to no known identifier): remove the alias; if none
  remain, the entry is REJECTED** — a glossary term with no anchor in code is
  speculation, not vocabulary.
- **SA0202 (term appears in no rule): the entry is REJECTED** — the glossary
  serves the catalog; a term no rule uses does not belong in it yet.
- **SA0002 (schema violation): fix and revalidate.**

Return only entries with zero findings.
