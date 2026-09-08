---
name: specanchor-change
description: Run a change to SPECANCHOR ITSELF through the delivery cycle — an index provider, the parity harness, a gate, the CLI or MCP surface, a schema, a skill contract, the rig. Use when working inside the specanchor repository. NOT for running the method on a client codebase — that is what core/skills/ is for.
---

# specanchor-change — the maintainer's path through the cycle

You are changing the toolchain, not running it. The distinction matters more here than in
most repositories, because `core/skills/` is full of skills that look like they apply to
you and do not: `implement` and `test-writer` tell an *agent on an engagement* how to work
on a *client's* target codebase. They are the product. This skill is how the product gets
built.

This skill enforces the SEQUENCE in `.claude/cycle.md`. It carries no rules of its own —
they live in `CLAUDE.md`, which points at the schemas (the constitution), the operating
agreement and `docs/REVISIONS.md` (the ledger). If a rule seems missing, that is a finding
to report, not a gap to fill from taste.

## Before anything else

1. `CLAUDE.md` — the deterministic/agent line, the contract surface, the gates
2. `docs/REVISIONS.md` — whether what you are about to build is already owed, and to which
   document it lands in. Building an owed item without closing its row leaves the ledger
   lying
3. `rig/legacy-factoring/TRAPS.md` — the answer key you will be judged against
4. `.claude/cycle.md` — the nine steps

## What is different here, and why

**The rig is the test, not a fixture.** Four planted traps with a written answer key. A
change to any detection path is evidence-free until it moves a trap's result: the trap that
was missed is now found, or the trap that was found is still found after a refactor. Cycle
§4 — write the test, then put the fault back and watch it go red — is literally what
TRAPS.md is for. If a new capability needs a fifth trap, plant it with its expected finding
*first*, then build the detector.

**A schema change is a breaking change to every existing artefact.** The six files in
`core/schemas/` validate catalogs that already exist on engagements. Adding a required field
invalidates all of them retroactively. Adding an optional one does not. Know which you are
doing and say so in the merge request.

**A new check is rejected, never flagged.** The three team rules are not advisory. If a
check cannot decide, the answer is to make it decidable or to leave it out — a warning
nobody can action is the "silently closed gate" the README promises does not exist here.

**The ledger is part of the change.** `docs/REVISIONS.md` records what is owed and what
shipped. Shipping an owed item and leaving its row open is how the same thing gets built
twice; the docs freshness gate now catches the specific case it was caught in.

**Determinism is testable, so test it.** Anything that could vary between runs —
enumeration order, a dictionary walk, a path separator, a timestamp in an artefact — is
tested by running twice and comparing, not by reading the code and deciding it looks fine.

## Step 6, concretely

`cycle.md` §6 says run this repository's own contract check. Here that is:

```
dotnet test SpecAnchor.sln --nologo
./scripts/docs-freshness.sh                                   # the docs still describe this
dotnet run --project core/cli/SpecAnchor.Cli -- gate \
  --discovery rig/legacy-factoring/discovery --src rig/legacy-factoring/src \
  --sql rig/legacy-factoring/sql --schemas core/schemas
./scripts/license-check.sh                                    # only if the graph changed
dotnet stryker                                                # if you touched core/parity
```

Docs freshness is not paperwork here. The README is what a client reads to judge how far
the chain has been built, and it has already been wrong in both directions — claiming work
as owed that had shipped, and claiming CLI flags that do not exist.

## Step 7, concretely

There is no screen. "Run it for real" means running the built CLI from a shell against the
rig, as a team on an engagement would, and reading the exit code:

```
dotnet run --project core/cli/SpecAnchor.Cli -- index \
  --src rig/legacy-factoring/src --sql rig/legacy-factoring/sql --out /tmp/idx
echo "exit=$?"          # 0 clean · 1 findings · 2 usage or I/O error — the contract
```

For an MCP change, start `specanchor mcp` and call the tool over stdio. For a skill-contract
change, run the skill against the rig and check its output against `TRAPS.md`. A capability
that works in the test host and not from a shell has not been delivered.

## Never

- Put a model call, an embedding or a similarity search anywhere in `core/index`,
  `core/parity` or `core/gates`. That is the line the product is sold on.
- Flag what should be rejected.
- Modify application code from a gate or a skill. specanchor sits beside the build.
- Ship an owed item and leave its `docs/REVISIONS.md` row open.
- Write an artefact in anything but English.
- Close a defect you could not reproduce.
