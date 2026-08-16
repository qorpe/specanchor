---
name: test-writer
description: Write ALL the tests a story needs on the target system, derived from the approved spec — every example row becomes a test, every edge in the catalog is covered, every test cites its rule id. Emits a coverage map so "all" is verified, not claimed.
---

# test-writer

## Input

- The APPROVED spec of one story: EARS/Gherkin criteria, DMN tables, and their
  filled example tables (a spec without examples cannot have entered review —
  if you receive one, reject the story back to the discovery track).
- The story's rule cards (open questions resolved; if any remain open, stop —
  filling a gap with a plausible assumption is the most expensive mistake).
- The context's edge-case catalog and the comparison policy (rounding modes,
  tolerances — these tell you which numeric behaviours deserve tests).
- The target codebase's existing test idioms (match them exactly).

## Output — the full set, by construction

| Class | Derived from | Rule |
|---|---|---|
| Acceptance | EVERY criterion and EVERY example-table row of the spec | one test per row, no sampling |
| Edge | the edge-case catalog entries touching this story's types | mandatory checklist, not inspiration |
| Negative | each validation/authorization clause in the spec | the forbidden path must be proven forbidden |
| Numeric/property | the comparison policy's rounding and tolerance fields | midpoint cases explicitly (banker's vs half-up is where money leaks) |
| Characterization (new side) | the story's CHAR tests re-targeted | `target: new`, same policy — parity's raw material |

Plus the **coverage map**: a table mapping every spec criterion / example row /
edge entry → the test that covers it. An unmapped row means the set is NOT all,
whatever it feels like.

## Procedure

1. Compile, don't compose: the spec's rows ARE the test cases; your job is
   faithful translation into the target's test idiom, not invention. Where you
   believe a case is missing from the spec, do NOT add a test for imagined
   behaviour — raise it to the discovery track as a spec gap.
2. Every test name and body cites its rule id; assertion messages speak domain
   language (the glossary's terms), so a red test tells the business what broke.
3. Tests assert BEHAVIOUR, never implementation details (no mock-verifying a
   private call chain; parity and the boundary gate own structure).
4. Prove the tests can fail: each new test must be red against the skeleton (or
   a deliberately broken behaviour) before it is green against the real one.
   A test that was never red proves nothing.

## Self-validation — non-negotiable

Return only when ALL hold:

- The coverage map has zero unmapped spec rows and zero unmapped edge entries;
  gaps you could not cover are LISTED as gaps, never silently dropped.
- Every test cites a rule id that exists in the catalog.
- The red-before-green evidence is recorded per test class.
- `specanchor gate` is clean over any artefacts you touched.
