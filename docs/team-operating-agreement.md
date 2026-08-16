# Team Operating Agreement

**Settled in Sprint 0. Reviewed at the end of Sprint 3.**
**Version:** v0.2 — adds the light path and artefact length limits

This document exists so nobody has to guess. It covers four things: who does what, what changes in Scrum, how domain modelling works in practice, and the rules for working with agents.

Everything not in this document is unchanged from how the team already works.

---

## 1. Team

Small and senior. The work has shifted from producing code to judging output, and judgement does not scale by adding junior capacity.

**Two functions, assigned to existing people. No new job titles.**

| Function | Owns | Load |
|---|---|---|
| Domain Steward | The glossary. Decides whether a new term is genuinely new or a duplicate of an existing one under a different name. Blocks vocabulary drift. | Part-time, a senior team member |
| Spec Owner | Ensures each slice's specifications arrive with open questions closed. Runs the validation session with the domain expert. | Per slice, usually the BA or PO |

Everyone else stays in their current role. The architect owns the context map and the gate policy.

**One external dependency must be named, not assumed:** a domain expert with time actually blocked in their calendar. Half a day per slice. Without this the method has no decision layer and stalls in week two.

---

## 2. What changes in Scrum

Sprints, backlog, refinement, review and retro all stay. Two things change.

### Definition of Ready — a story cannot enter a sprint unless

- it is linked to a rule id
- that rule has a disposition (keep / change / retire)
- its specification contains no open bracket
- the domain expert has approved the specification

### Definition of Done — a story is not done unless

- the acceptance test was red before it was green
- the parity test passes on both legacy and new
- the boundary check is clean
- the drift gate is open
- the glossary is updated if a new term appeared

### Two paths — not every change gets a specification

Before Definition of Ready applies, ask one question: **does this change touch a rule?**

- **Full loop** — the change adds, alters or retires a business rule. Definition of Ready and Definition of Done apply in full.
- **Light path** — technical change with no rule impact: refactoring, performance work, library upgrades, logging. Straight to code, and all four gates still run.

The light path skips specification, never verification. A refactor that leaves behaviour unchanged is already proven by the existing parity tests.

Running the full loop on everything is how this method dies. A one-line bug fix does not need four acceptance criteria, and a team asked to produce them will stop taking the method seriously by the second sprint.

### Estimation

Story points stay. What is estimated changes: not the size of the code, but the amount of unresolved uncertainty. In refinement the question is *"how many open questions, how many rules are disputed"* — not *"how many endpoints"*. The team recalibrates within two sprints.

### Ceremonies

- **Refinement** — reviews the discovery track's output. Items failing Definition of Ready go back. This meeting does not get shorter; it is where the value is created.
- **Review** — the parity report goes on screen, not a UI demo.
- **Retro** — one fixed agenda item: how many times was a gate bypassed, and why. A rising number means the method is dying, and this is where it gets caught.

### Backlog

One backlog, two item types: discovery items (produce next slice's specifications) and delivery items (one or more rules each). Roughly 20–25% of capacity goes to the discovery track, decreasing after the third slice.

---

## 3. Domain modelling in practice

The client will not ask for DDD and does not need to hear the vocabulary. Internally the practices apply; externally the terms are translated.

| Internal | External |
|---|---|
| Bounded context | Business area / module boundary |
| Ubiquitous language | Term glossary |
| Aggregate | Consistency boundary |
| Event storming | Validation session |

**What is a ritual, and how often:**

- One workshop, half a day, in Sprint 0 only. Its purpose is to validate the draft context map extracted from code — not to produce rules.
- Per slice: a half-day validation session covering only the contradictions and the unexplained. Not a walkthrough.

**What is not a ritual:** the glossary, the boundaries, the aggregate decisions. These live as artefacts and as an automated boundary check. Nobody is asked to "practice DDD" — the structure puts things in the right place and the gate flags it when they are not.

**Boundaries are provisional until proven.** The Sprint 0 map is a hypothesis. Each slice confirms its own portion. When a later slice reveals an earlier boundary was wrong, the map is updated and an ADR is written. This is normal, not a defect — and it is only possible because the boundary check is automated.

---

## 4. Working with agents

**Three rules. These are the whole method as far as the team is concerned.**

1. **No rule exists without a source reference.** If it does not say where it came from, it does not go in the catalog.
2. **A specification with an open question does not enter a sprint.** Filling a gap with a plausible assumption is the most expensive mistake available.
3. **Gates are not closed silently.** If a gate must be bypassed, it is recorded, it has an owner, and it has an expiry date.

**Two commands in daily use:**

- `specanchor scaffold <rule-id>` — generates the acceptance test (red) and the code skeleton
- `specanchor gate` — runs all four gates locally, before opening a PR

Run `specanchor gate` before pushing. Seeing a gate go red for the first time in CI is how people come to resent gates; seeing it locally in two seconds is how they become habit.

**Confidence levels are not decoration.** `evidenced` means a test proved it against the legacy system. `inferred` means the agent concluded it and nothing has confirmed it. `disputed` means the evidence conflicts. Never treat an `inferred` rule as fact, however clean the output reads.

**Assume polished output is wrong until a gate says otherwise.** Agent output looks finished. That is the failure mode this whole method is built around.

---

## 5. What is deliberately not settled yet

Fixing these now would mean guessing. They are decided from observation, not up front:

| Open | Decided by |
|---|---|
| Adapter seam — which questions the gates ask the codebase | End of the first slice, from what the gates actually needed |
| Slice sizing | After two slices, from measured effort |
| Mutation score threshold | After the first run on the calculation core |
| Whether decision tables are executed by an engine or compiled to tests | First slice, not the PoC |
| Rule versioning when a rule changes mid-project | Before the second slice |

Nothing here blocks Sprint 1. Deciding them early would mean deciding them wrong.

---

## 6. Rollout order

Do not introduce all of this to the team at once. A four-part methodology launch invites resistance; a sequence of small changes does not.

1. **Sprint 0** — Scrum is unchanged. Introduce only the artefacts and the two commands.
2. **Sprint 1** — gates go into CI while the delivery track is still empty, so the first red gate costs nobody a deadline.
3. **Sprint 2** — Definition of Ready and Definition of Done take effect.
4. **Sprint 3 onward** — the vocabulary and boundary discipline settle on their own, through the glossary and the boundary check.

The team should experience this as *two new rules and one new command* — not as a new methodology. If it feels like a methodology rollout, it will be treated as one.


---

## 7. Artefact length limits

Long artefacts do not get read, and documentation nobody reads is already rotten.

| Artefact | Limit |
|---|---|
| Rule statement | One sentence |
| Rule card total | About 15 lines |
| Specification | One screen |
| ADR | One page |
| Glossary entry | Two sentences plus structured fields |

These are enforced by the tooling, not left to judgement. An over-long artefact is a generation failure in the same class as a rule without a source reference.

One related rule, worth stating plainly: **a rule statement may not contain code identifiers.** "Where contract type is 3" is not a business rule sentence; "for non-recourse contracts" is. This is checked automatically against the glossary's `aliases_in_code` entries.

The reason is not style. If the domain expert cannot read a specification and say "no, that is wrong", the approval gate has quietly stopped working — they will approve what they cannot judge.
