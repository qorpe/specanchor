# Concept Reference & Build Brief

**Part 1** — every concept in one line: what it is, and where it appears in our method.
**Part 2** — the reading list, condensed.
**Part 3** — the brief to start building with Claude Code.
**Version:** v0.2 — adds SDD levels, memory bank distinction, translation loss, light path, formal coverage; build brief narrowed after the Spec Kit decision.

---

# Part 1 — Concept map

## Strategic DDD

| Concept | One line | Where it appears in our method |
|---|---|---|
| Bounded context | The boundary within which one model and one vocabulary hold | Agent's context scope, team ownership, and what the boundary gate enforces |
| Ubiquitous language | Terms with exactly one meaning inside a context | The Domain Ledger |
| Context map | The relationships between contexts | Slice ordering and integration decisions |
| Anticorruption layer | A translator that stops the legacy model leaking into the new one | Essential at every strangler cut |
| Aggregate | The consistency boundary — what changes together in one transaction | ADR decision, enforced by the boundary gate |
| Invariant | A statement that must always hold | Lifecycle rules and aggregate design |
| Domain event | Something that has happened, in past tense | Lifecycle transitions |
| Core / supporting / generic subdomain | Where differentiation lives versus where it does not | Slice prioritisation and build-versus-buy |

## Spec-driven development

| Concept | One line | Where it appears |
|---|---|---|
| Specification as source of truth | Code conforms to the spec, not the reverse | The whole method |
| Contract-first | The interface is agreed before implementation | OpenAPI, AsyncAPI |
| EARS | Five sentence patterns that remove ambiguity from acceptance criteria | Every spec |
| Gherkin | Given/When/Then scenarios | Acceptance tests |
| DMN | Decision tables with hit policies and the FEEL expression language | Calculation and pricing rules |
| Executable specification | A spec that runs as a test rather than sitting in a document | Rules compiled to acceptance tests |
| Spec drift | Code silently diverging from its specification — a failure mode, not a tool | What the drift gate exists to prevent |

## Spec-driven development — levels and vocabulary

| Concept | One line | Where we sit |
|---|---|---|
| Spec-first | A spec is written before the work, then discarded | Not us |
| **Spec-anchored** | The spec is kept and maintained as the feature evolves | **This is our level — use this word to position** |
| Spec-as-source | Only the spec is edited; humans never touch code | Not us, deliberately — it inherits the rigidity of model-driven development plus the nondeterminism of LLMs |
| Memory bank | Context relevant to every session: glossary, context map, principles | Domain Ledger, context map, constitution |
| Spec | Context relevant only to the work that changes one behaviour | Rules and their acceptance criteria |
| Translation loss | Meaning lost at each handoff: need → requirement → design → code → validation | What the artefact chain exists to prevent |
| Formal coverage | machine-verified artefacts / total artefacts | The measurable version of "our documentation stays alive" |

## Legacy and verification

| Concept | One line | Where it appears |
|---|---|---|
| Characterization test | Freezes what the legacy system currently does, without judging whether it is right | `specanchor verify` |
| Golden master | A frozen snapshot of legacy output used as the comparison baseline | Parity harness |
| Back-to-back / parallel run | Running old and new on the same input and comparing | The parity gate; "parallel run" is the term auditors know |
| Approval testing and scrubbers | Snapshot comparison with nondeterministic fields normalised away | Timestamps, sequences, generated ids |
| Mutation testing | Injecting faults to check whether the tests actually detect anything | Nightly, on the calculation core only |
| Kill rate | The share of injected faults the test suite catches | The mutation threshold |
| Seam | A place where behaviour can be changed without editing in place — how untestable legacy becomes testable | Required before characterization tests can run on tangled code |
| Strangler Fig | Replacing a system piece by piece while it keeps running | The migration strategy |
| Branch by abstraction | Routing between old and new implementations behind one interface | Slice cutover |
| The 7 Rs | Rehost, replatform, refactor, rearchitect, rebuild, replace, retain | Per-module disposition in Discovery Zero |
| CDC | Change data capture — streaming changes from the legacy database | Keeping data in sync during cutover |
| Outbox | Publishing events transactionally with the data change | Reliability during migration |
| Reconciliation | Queries proving no data was lost in transfer | Cutover evidence |
| Idempotency | The same operation applied twice produces one result | Where retries could create duplicate records |

## Architecture and quality

| Concept | One line | Where it appears |
|---|---|---|
| Fitness function | An automated test asserting an architectural property | The boundary gate |
| Architectural drift | Structure decaying away from its intended design | What fitness functions catch |
| ADR | A short record of one decision and its reasoning | Every boundary and aggregate decision |
| C4 model | Four zoom levels: context, container, component, code | Architecture diagrams |
| Build-time / runtime / data-level coupling | Three layers of dependency; the third is invisible in code and the most dangerous | The table read/write matrix |

## Process

| Concept | One line | Where it appears |
|---|---|---|
| Dual-track | Discovery runs one sprint ahead of delivery, same team | The sprint rhythm |
| Vertical slice | A thin end-to-end cut, never a layer | Slice definition |
| Definition of Ready / Done | The entry and exit conditions of a sprint item | Where the gates live in Scrum |
| Light path | A change touching no rule skips specification, never verification | Prevents the full loop being applied to a one-line fix |
| Confidence level | evidenced / inferred / disputed | Every rule |
| Disposition | keep / change / retire — set by a human, never by a tool | The decision gate |

## AI-native working

| Concept | One line | Why |
|---|---|---|
| Deterministic index over semantic search | Symbol-level precision beats similarity | An auditable catalog needs exact references, not resemblance |
| Context scoping | Give the agent one bounded context, not the whole system | Accuracy, and the practical payoff of domain boundaries |
| Self-validating skill | Every skill checks its own output before returning it | The commercial claim rests on the verification half |
| Human-in-the-loop gate | Specific points where a named person must decide | Regulatory accountability |
| Polished output is not verified output | Agent output reads as finished regardless of correctness | The failure mode the whole method is built around |

---

# Part 2 — Reading, condensed

**Read properly (about 8 days)**

1. Khononov, *Learning Domain-Driven Design* — strategic half in depth, tactical half skimmed
2. Tune, *Architecture Modernization* — DDD applied to legacy, slicing, team structure
3. Feathers, *Working Effectively with Legacy Code* — characterization testing and seams only
4. Adzic, *Specification by Example* — the closest thing to an SDD reference

**Skim (about 2 days)**

5. Brandolini, *Introducing EventStorming* — facilitation chapters
6. Ford et al., *Building Evolutionary Architectures* — fitness functions
7. Martraire, *Living Documentation* — vocabulary for the writing series

**Short items (a few hours total)**

EARS notation · ADR format (Nygard) · C4 model · Strangler Fig (Fowler) · Scrum Guide

**Study properly, not skim:** the DMN specification. It carries the pricing and limit rules.

**Reference only:** Evans (blue book), Vernon (*Implementing DDD*). Look things up; do not read front to back.

**Not on the list:** prompt engineering, RAG and vector databases, fine-tuning, LLM gateway design.

**Highest return of anything here:** the factoring domain itself. No technical concept substitutes for it.

---

# Part 3 — Build brief for Claude Code

Paste this as the opening context of the build session.

## Goal

A .NET toolchain that extracts business rules from a legacy codebase, proves them against the running legacy system, and enforces four gates in CI. Not a framework — it sits beside the build and never modifies application code.

## Non-negotiable constraints

- The application source tree is never modified.
- Every extracted rule carries a resolvable source reference. Rules without one are rejected, not flagged.
- Every skill validates its own output before returning it.
- Artefact schemas are versioned from day one (`schemaVersion: 1`).
- No embeddings, no vector search. The index is symbol-level and deterministic.
- Gates run headless in CI, with no agent present.
- Everywhere the gates reach into the codebase lives in one class. Not an abstraction — just not scattered.

## Build order

1. Artefact schemas — rule, ledger-term, char-test, spec, comparison-policy
2. Index, C# provider — Roslyn: type inventory, call graph, dead code, complexity. Two modes: syntax-only when the solution will not build, semantic when it will. Report unbuildable projects as blind spots rather than skipping silently.
3. Index, SQL provider — ScriptDom: schema, foreign keys, triggers, stored procedure bodies parsed to AST, scheduled jobs, parameter tables. Compare repository scripts against what is actually deployed in the database and report the differences.
4. Table read/write matrix — which module touches which table, from EF model, Dapper literals, procedure ASTs and ADO call sites
5. `rule-extractor` and `module-inventory` skills
6. `char-test-writer` skill — snapshot with scrubbers for nondeterministic fields
7. Parity harness — comparison policy, tolerances, known-difference register. Expect a large share of early failures to be decimal rounding differences between C# and T-SQL, not logic errors; classify them rather than treating them as defects.
8. The four gates, as an analyzer package plus CI steps
9. `specanchor gate` — run all gates locally

## Deliberately not building

Spec drafting and code scaffolding — Spec Kit, Kiro and Claude Code already do these well. LLM gateway. Vector search. Documentation portal. Adapter abstraction. Custom spec formats.

## Relationship to Spec Kit

The surface layer is borrowed, the verification layer is ours. Preset reshapes spec templates to require `rule_id`, `source_ref`, `confidence` and `disposition`; extension adds `index`, `discover`, `verify`, `parity`, `gate`.

Hard rule: **everything above must work with zero Spec Kit installed.** Spec Kit is a surface, not a source of truth. Do not let any schema, gate or artefact depend on it.

## First task

Build a fake legacy system to develop against: a small .NET application plus three or four stored procedures containing, on purpose, a commission calculation that rounds differently in C# and SQL, an undocumented edge case, a dead code block, and one concept appearing under two different names. Every later component is developed and tested against this before touching client code.
