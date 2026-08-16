---
name: implement
description: Implement ONE story on the target codebase, to the method's standards — red-first from the scaffold, facts from MCP, traceability in every commit, gates green before the MR. The LEGACY source is never touched; the target is where you work.
---

# implement

## Input

- One story that passed Definition of Ready: its rule id(s), the APPROVED spec
  (zero open brackets), and the disposition decisions behind it.
- The context pack: the bounded context's glossary (aliases_in_code included),
  its rule cards, the boundary map, and the comparison policy.
- The scaffold output: the red acceptance test and the skeleton
  (`specanchor scaffold <rule-id>` — run it if it has not been run).
- The specanchor MCP tools for every question about the LEGACY system
  (`who_calls`, `table_access`, `sql_object`, `dead_code`). You never open the
  legacy source; if the index cannot answer, that is an open question for the
  discovery track, not a license to guess.

## Target codebase

You write code in the TARGET system only (Goldpath-generated, or the client's
own framework — the method does not care). Follow the target's existing
conventions exactly: its naming, its layering, its test idioms. On a Goldpath
target, use its generators first (`goldpath add feature` and friends) and write
by hand only what they do not produce.

## Procedure

1. **Red first.** Run the scaffolded acceptance test; confirm it is red. If it
   is green before you wrote anything, stop — something is wrong with the story.
2. Implement the behaviour the spec states — not more. A change that would touch
   a rule outside this story's rule ids is scope creep: STOP and send it back to
   the discovery track instead of absorbing it.
3. Use domain language from the glossary in every identifier you introduce; the
   boundary gate will hold you to the context map.
4. **Traceability:** every commit message carries the rule id(s). The chain
   business rule → spec → test → commit must survive you.
5. Run locally, in order: build · the acceptance test (now green) · the full
   test suite · `specanchor gate` · the parity slice if one exists for this
   capability. All green before an MR exists.
6. **Never weaken a test to pass.** A red characterization or parity test means
   the behaviour differs from legacy: either your code is wrong (fix it) or the
   difference is intended — then it goes to the known-differences register with
   a business signature, and only then may the comparison accept it.

## Self-validation — non-negotiable

An MR may only be opened when ALL hold, and the MR description states each:

- The acceptance test was red before implementation and is green after (say so
  explicitly — the reviewer cannot see history).
- `specanchor gate` is clean; build and full suite green.
- The diff touches only files this story's scope explains.
- Every commit references a rule id from the story.
- Zero suppressions added; if one was unavoidable it is recorded with owner and
  expiry per the team rules.

The MR goes to a named human. You never merge.
