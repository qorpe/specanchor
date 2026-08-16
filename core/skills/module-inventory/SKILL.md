---
name: module-inventory
description: Produce the module inventory and dead-code list from the deterministic indexes. Every entry must resolve to a real symbol in the index; unresolvable entries are rejected.
---

# module-inventory

## Input

The C# index, the SQL index and the table read/write matrix for the whole
solution (this skill is the one exception to per-context scoping: an inventory
is by nature global). Never the raw repository.

## Output

`discovery/inventory.yaml` containing:

- `modules[]` — one entry per namespace/schema area: name, type count,
  procedures, triggers, jobs, and the tables it reads/writes (from the matrix).
- `dead_code[]` — the index's dead-code CANDIDATES, each with its file:line
  span, carried as candidates: the human confirms, the tool never deletes.
- `blind_spots[]` — copied verbatim from both indexes plus the
  data-access-coverage unresolved list. Declaring the gap honestly is worth
  more than claiming full coverage.

## Procedure

1. Group the C# index's namespaces and the SQL index's objects into modules.
   Where the matrix shows a table shared by two modules, record the shared
   table explicitly — data-level coupling is the finding, not a detail.
2. For each dead-code candidate, add the inbound-reference evidence (none) and
   the span, so a reviewer can jump to it in one click.
3. Do not editorialize: no recommendations, no refactoring advice. Inventory
   states what is; the decision layer is human.

## Self-validation — non-negotiable

Every module entry, dead-code entry and table name must exist in the indexes it
came from. An entry naming a symbol, table or object the indexes do not contain
is REJECTED and reported. Return only an inventory whose every line is
mechanically traceable back to the index.
