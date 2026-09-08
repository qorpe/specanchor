# specanchor — working agreement

This file is for changing **specanchor itself**. It is not the method. The method — how a
team uses specanchor on a client's legacy system — lives in `docs/`, and the skills a team
runs live in `core/skills/`. Do not confuse the two: `core/skills/implement` tells an agent
how to implement a story on a *client's* target codebase. It is the product. It is not how
you change this repository.

## Read these first

They already say most of what a change needs to respect, and none of it is repeated here:

| Document | What it owns |
|---|---|
| `core/schemas/*.schema.v1.json` | **the constitution.** Six artefact schemas. Everything else obeys them |
| `README.md` | the three team rules, the architecture in one paragraph, the layout |
| `docs/toolset-spec.md` | what each component is and what it is not |
| `docs/team-operating-agreement.md` | Definition of Ready / Done, the two paths, the agent rules |
| `docs/REVISIONS.md` | **the ledger.** What is owed, where it lands, what shipped |
| `rig/legacy-factoring/TRAPS.md` | the answer key. Four planted traps the whole chain is judged against |

## The line that governs everything

> Everything that produces evidence is **deterministic**. Everything that interprets is an
> **agent**, and its output always arrives as a merge request.

A change that moves work across that line is an architectural change, not a refactor. Adding
a model call to the index, the parity harness or a gate is the one thing this repository
cannot do — the whole claim to a regulated client is that unvalidated output cannot pass the
gate, and a gate that interprets has nothing to be validated against.

Three consequences, all of them checkable:

1. **No embeddings, no vector search, no model in `core/index`, `core/parity`, `core/gates`.**
   Roslyn and ScriptDom answer symbol-level questions exactly; a nearest-neighbour guess is
   not the same kind of answer and must never be substituted for one.
2. **The engine never guesses.** An unresolvable reference is rejected, not flagged. A rule
   without `source_ref` is rejected, not flagged. This is a stated team rule, and it applies
   to every new check written here — including the scripts in `scripts/`.
3. **Never modify application code.** specanchor sits beside the build. Removing it means
   deleting two folders and the application still runs. A change that makes that untrue is a
   change to what the product is.

## Where the contract lives

There is no `PublicAPI.Shipped.txt` here. The surface consumers depend on is:

- the **six artefact schemas** in `core/schemas/` — a client's catalog is validated against
  them, so a required field added is a breaking change to every existing artefact
- the **CLI**: verbs, flags, exit codes (`0` clean · `1` findings · `2` usage or I/O error)
- the **MCP tools** served by `specanchor mcp` — the only way a skill reaches the index
- the **index output contract** — `Procedures/Triggers/Reads/Writes/BranchCount` and the
  C# type/member shape. `docs/REVISIONS.md` #16 plans an Oracle adapter that keeps this
  contract *exactly*; that promise is only worth something if the contract is treated as one
- the **skill contracts** in `core/skills/` and their self-validation engine

`scripts/docs-freshness.sh` is what stops the documents from drifting away from those. It is
this repository's answer to `cycle.md` §6: **it is the contract check.**

## The gates

CI runs six. Run them before offering a change:

```
dotnet test SpecAnchor.sln --nologo
dotnet run --project rig/legacy-factoring/src/FactoringApp        # the fake legacy still runs
dotnet run --project core/cli/SpecAnchor.Cli -- gate \
  --discovery rig/legacy-factoring/discovery --src rig/legacy-factoring/src \
  --sql rig/legacy-factoring/sql --schemas core/schemas            # the rig sample passes
./scripts/license-check.sh                                         # every dependency allowlisted
./scripts/docs-freshness.sh                                        # the docs still describe this
dotnet stryker                                                     # parity core, break at 75
```

The mutation gate earned its place: its first run scored 66.2% and exposed real gaps —
relative tolerances, the missing-record direction, the field union. "The tests pass" and "the
tests would notice" are different claims, and for a parity harness only the second one is
worth anything.

## The rig is the acceptance test

`rig/legacy-factoring` is not a fixture directory. It is a fake legacy system with four
planted traps and a written answer key, and the chain is judged by whether it finds them.
A change to detection that does not move a trap's result has not been shown to do anything.
When adding a capability, ask whether it needs a fifth trap — and if it does, plant it in
`TRAPS.md` with its expected finding *before* building the detector.

## Every artefact is in English

Client-side reviewers and auditors read this repository. A section they cannot read is a
section they cannot approve, which defeats the point of an auditable trail. The docs
freshness gate checks this; it caught `REVISIONS.md` §16.

## The delivery cycle

`.claude/cycle.md` carries the nine steps. `.claude/skills/specanchor-change/` is the path
through them for this repository. Neither restates the rules above — they point here.
