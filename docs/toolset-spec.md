# Toolset Specification

**Purpose:** the complete component specification for the AI-native spec-driven toolchain, sufficient to build from.
**Scope:** Layer A (portable core) in full. Layer B and C are sketched only — they are outputs of the first slice, not inputs.
**Version:** v0.4 — adds Spec Kit layering (§16), consolidated metrics (§17); CLI narrowed in §7

---

## 1. Component map

| # | Component | Layer | Kind | Needed for PoC |
|---|---|---|---|---|
| 1 | Artefact schemas | A | JSON Schema files | Yes |
| 2 | Index — C# provider | A | Library (Roslyn) | Yes |
| 3 | Index — SQL provider | A | Library (ScriptDom) | Yes |
| 4 | Skill set (four) | A | Markdown + schema + CLI calls | Yes |
| 5 | Parity harness | A | Library + runner | Yes |
| 6 | Gates (four) | A | Analyzer package + CI steps | After PoC |
| 7 | CLI | A | .NET global tool | Yes (partial) |
| 8 | Capability accessor | B seed | Single class, no abstraction | Yes |
| 9 | Adapters | B | Separate packages | No |
| 10 | Codegen from spec | C | Goldpath-only | No |

Everything the PoC needs is in one repository, one solution. Packaging happens after the PoC.

---

## 2. Artefact schemas

Fix these on day one. Every other component references them, and changing them later means migrating every artefact produced so far.

All schemas carry `schemaVersion: 1`.

### rule

```
rule_id          string, immutable, RULE-nnnn
version          integer, bumped when the rule's meaning changes
schemaVersion    integer
context          string, bounded context name
statement        string, domain language, no code terms
source_ref[]     { file, line_start, line_end } or { object, kind: procedure|trigger|job }
confidence       evidenced | inferred | disputed
evidence         test_id, or reason it could not be proven
open_questions[] free text, each independently closable
disposition      null | keep | change | retire
decided_by       person, set when disposition is set
supersedes       rule_id, when a rule replaces another
```

**Invariants, enforced by the producing skill:** no rule without at least one resolvable `source_ref`; `confidence: evidenced` only when `evidence` names a test that actually ran and passed.

### ledger-term

```
term                    string
context                 string
definition              string
aliases_in_code[]       identifiers as they appear in source
not_to_be_confused_with string, cross-context collisions
source_ref[]
status                  proposed | confirmed_by_expert
```

`aliases_in_code` and `not_to_be_confused_with` are what make the glossary usable by an agent. Without them it is decoration.

### char-test

```
test_id      CHAR-nnnn
rule_id      the rule this proves
target       legacy | new
method       replay | synthetic | recorded_traffic
sample_size  integer
result       { passed, failed }
failures     description, linked to open_questions where relevant
tolerance    reference to the comparison policy used
```

### spec

```
spec_id       SPEC-nnnn
rule_ids[]    one or more
notation      ears | gherkin | dmn
body          the criteria
open_brackets integer — must be 0 before a spec is releasable
approved_by   domain expert, required
```

### comparison policy

```
excluded_fields[]     timestamps, sequences, generated identifiers
tolerances[]          { field, type: absolute|relative, value }
rounding              expected mode per field group
known_differences[]   { id, description, accepted_by, date, rule_id }
```

The known-differences register is what makes parity honest. Demanding a byte-exact match produces a gate that gets bypassed within two weeks.

---

## 3. Index

Deterministic. No model involved. Its output is what the agents read instead of raw source.

### C# provider (Roslyn)

Input: solution path.
Output:

- type and member inventory with file/line spans
- call graph (`SymbolFinder`)
- dead code candidates — public entry points with no inbound references
- project and namespace map
- complexity hotspots

Notes: use `MSBuildWorkspace`; semantic model, not syntax-only. Symbol-level precision is the entire point — this is why no embedding or vector search appears anywhere in this design.

### SQL provider (ScriptDom)

Input: connection string (read-only) plus script directory.
Output:

- schema, keys, indexes
- trigger inventory with bodies
- **stored procedure bodies parsed to AST** — `IF`/`CASE` branches, arithmetic expressions, table reads and writes
- scheduled job inventory

This provider is not optional. In a legacy factoring system a large share of the business logic lives in stored procedures; an extraction that reads only C# will look competent and be wrong.

### Combined output

A single index artefact joining both: which procedure is called from which service, which table each writes, which job triggers it.

---

## 4. Skills

Each skill: markdown instruction plus schema plus a CLI call. No product-specific syntax; a thin wrapper per surface if one is needed.

| Skill | Input | Output | Self-validation |
|---|---|---|---|
| `module-inventory` | index | inventory, dead code list | every entry resolves to a real symbol |
| `rule-extractor` | index + procedure ASTs, scoped to one context | rule catalog | rules without a resolvable `source_ref` are rejected, not flagged |
| `domain-ledger` | rule catalog + identifiers | glossary | every term appears in at least one rule |
| `char-test-writer` | one rule | runnable test against legacy | the test executed; its result is recorded either way |

**Design rule:** a skill that produces without validating is not part of this toolset. The commercial claim rests entirely on the validation half.

**Context scoping:** agents are given one bounded context at a time — that context's glossary, rules, and related procedures. Not the whole index. This improves accuracy and is the practical payoff of domain boundaries.

---

## 5. Parity harness

The most valuable component. Two modes:

**Replay** — historical transactions from production data (masked) are pushed through legacy and new, outputs compared under the comparison policy.

**Synthetic** — generated inputs covering rule branches, used where historical coverage is thin.

Snapshot handling uses Verify-style scrubbers: timestamps, generated identifiers and sequences are normalised before comparison, or excluded by policy.

**Decimal behaviour is a first-class concern.** C# and T-SQL can round differently on the same expression (`MidpointRounding.ToEven` versus away-from-zero, `decimal` versus `float`). Expect a large share of early parity failures to come from this, not from logic. Detect it, classify it, and route it to the known-differences register rather than treating it as a defect.

Output: a parity report — sample size, pass/fail, failures grouped by cause, coverage of rules under test.

---

## 6. Gates

| Gate | Compares | Implementation | Runs |
|---|---|---|---|
| Source reference | `source_ref` against real symbols | CLI check | on catalog production |
| Statement quality | rule statement against glossary `aliases_in_code` — a rule sentence may not contain code identifiers | CLI check | on catalog production |
| Touch | if a rule's `source_ref` changed in a PR but the rule and its test did not | CLI check in CI | every PR |
| Boundary | code dependencies against context map | NetArchTest / ArchUnitNET | every PR |
| Drift | code against spec — signatures, fields, rule branches | analyzer + CLI | every PR |
| Parity + mutation | legacy versus new output; mutant kill rate | harness + Stryker.NET | parity every PR, mutation nightly |

**Distribution:** as a NuGet analyzer package plus a shared CI template. Analyzers surface violations in the IDE for free — no separate editor plugin.

**Mutation scope:** the domain and calculation core only. Repository-wide mutation testing makes CI take hours and gets switched off in month three.

**Bypass:** recorded, owned, and given an expiry. Never silent.

---

## 7. CLI

Narrowed deliberately. Everything the editing surface already does well is left to it.

```
specanchor index                      build the deterministic index
specanchor discover --context <name>  rules, glossary, gaps
specanchor verify <rule-id>           generate a characterization test and run it
specanchor decide --context <name>    fill dispositions, list open questions
specanchor model --context <name>     propose boundaries and aggregates, draft ADR
specanchor gate                       run all gates locally
specanchor parity --slice <name>      run the parity harness
specanchor mutate --scope domain      mutation run
specanchor report                     metrics
```

**Not built:** spec drafting and code scaffolding. Spec Kit, Kiro and Claude Code all do this competently; rebuilding it spends effort on the one part nobody would pay for.

`specanchor gate` determines adoption more than any other command. A gate first seen red in CI breeds resentment; the same gate seen locally in two seconds becomes habit.

`specanchor model` proposes; it never decides. Its output is born `proposed`, and no code is scaffolded in a context whose boundaries are not `accepted`.

## 8. Repository layout

In the toolset repository:

```
core/
  schemas/        rule, ledger-term, char-test, spec, comparison-policy
  index/csharp/   Roslyn provider
  index/sql/      ScriptDom provider
  skills/         four skills
  parity/         harness and scrubbers
  gates/          analyzers and CI steps
  cli/
```

In a consuming project:

```
.specanchor/config.yaml        adapter, endpoint, comparison policy, thresholds
discovery/<context>/      rules, glossary, tests, gaps
src/                      untouched
```

`src/` never changes. That is the concrete proof of being a toolchain rather than a framework: removing the tool means deleting two folders, and the application still runs.

---

## 9. Build order

1. Artefact schemas — day one, versioned
2. Index: C# provider
3. Index: SQL provider
4. `rule-extractor` + `module-inventory`
5. `char-test-writer`
6. Parity harness with comparison policy
7. `specanchor gate` and the four gates
8. `specanchor scaffold`
9. Remaining commands

Steps 1–6 are the PoC. Run all of them against the fake legacy system before touching client code.

Write gates before scaffold. Generating code before the gates exist means generating unverified code — the exact thing this design is built to prevent.

---

## 10. Deliberately out of scope

| Not building | Why |
|---|---|
| LLM gateway | The bank's decision, likely already made at group level. Stay endpoint-agnostic instead |
| Vector search over code | Symbol-level precision beats semantic similarity, and an auditable catalog needs precision |
| Living docs portal | After the third slice, not before |
| Adapter abstraction | An output of the first slice. Designing the seam before observing what the gates ask produces the wrong seam |
| Codegen from spec | Layer C, after adapters |
| Custom spec format | Standard notations only — portable, and familiar to auditors |

**Cheap insurance now:** keep every place the gates reach into the codebase inside a single class. Not an abstraction — just not scattered. That class becomes the adapter when the time comes.

---

## 11. Data access coverage — the largest risk in the index

A real legacy .NET system does not use one data access technology. It has layers deposited over fifteen years, and each one hides business rules differently. Treating this casually produces a catalog that is confidently incomplete.

### Recognizers required

| Technology | Where rules hide | How to extract |
|---|---|---|
| EF Core | LINQ expressions, query filters, `SaveChanges` overrides, value converters | Roslyn semantic model; resolve `DbContext` model and entity configuration |
| EF6 / LINQ to SQL | EDMX / DBML XML mappings | Parse the XML model files alongside the code |
| NHibernate | HBM XML or Fluent mappings | Parse mapping files; Fluent needs Roslyn |
| Dapper | Raw SQL in string literals | Extract literals via Roslyn, then parse each as SQL |
| ADO.NET direct | `SqlCommand` with `CommandType.StoredProcedure` | Link call site to procedure name — often a constant, config key, or resource string |
| Typed DataSets | `.xsd` with embedded queries | Parse the dataset schema; common in older enterprise .NET |
| String-concatenated SQL | Built at runtime | Partially resolvable only; see runtime capture below |
| Stored procedures | Branches, arithmetic, cursors | AST parsing |
| Dynamic SQL inside procedures | `sp_executesql`, `EXECUTE IMMEDIATE` | Not statically resolvable; runtime capture only |

**Database engine matters and must be settled before writing the SQL provider.** `Microsoft.SqlServer.TransactSql.ScriptDom` parses T-SQL only. If the legacy runs on Oracle, PL/SQL packages need a different parser entirely (ANTLR-based grammar), and package-level structure — packages, package bodies, global state — changes how rules are located. Confirm the engine, and the presence of packages versus standalone procedures, before estimating this component.

### Runtime capture — the completeness safety net

Static analysis cannot resolve dynamic SQL, reflection-based mapping, or configuration-driven procedure selection. Do not pretend otherwise. Capture what actually executes:

- database-side query capture (Extended Events, trace, or the Oracle equivalent) over a representative period
- an EF Core command interceptor and an ADO.NET wrapper in a non-production environment
- correlate captured statements back to call sites where possible

This has a second payoff: the captured workload becomes the replay corpus for the parity harness. Real production traffic is a far better parity input than synthetic cases.

### Coverage artefact

Add a fifth artefact schema:

```
data_access_coverage
  call_sites_total          integer
  call_sites_resolved       integer
  by_technology[]           { technology, total, resolved }
  unresolved[]              { file, line, reason }
  runtime_only[]            statements seen at runtime with no static origin
```

Declaring the gap honestly is worth more than claiming full coverage. When the client asks how confident the analysis is, this artefact is the answer — and an unresolved call site in a factoring calculation path is itself a finding worth reporting.

---

## 12. Specification coverage — the four notations are not sufficient

OpenAPI, AsyncAPI, JSON Schema and DMN cover interfaces, message contracts, data shapes and decision logic. A factoring system contains at least four kinds of rule that none of them express.

### Additional specification types

| Concern | Example in factoring | Notation |
|---|---|---|
| Lifecycle and state | A receivable moves through created, assigned, notified, collected, overdue, written off — with legal effects at each transition | State/transition YAML of your own; keep it simple, statechart semantics are more than is needed |
| Temporal and scheduling | Value dates, cut-off times, business calendar, batch windows, maturity calculation | Explicit calendar and schedule spec; do not leave these inside cron expressions and job code |
| Data migration mapping | Legacy field to new field, transformation, defaulting, and what happens to values that fit neither | Mapping spec — the single most under-specified artefact in modernization work |
| Authorization | Who may approve a limit increase, who may waive commission, four-eyes requirements | Permission matrix |
| Numeric policy | Rounding mode, precision, currency handling per field group | The comparison policy already defined; promote it to a first-class spec |
| Regulatory constraint | Reporting obligations and retention requirements | Constraint register, linked to the rules it restricts |

**Do not force a heavy standard where none fits.** For lifecycle, mapping and permissions, a small versioned YAML schema of your own is better than bending SCXML or a policy language into shape. The standards matter where auditors and other teams read them — interfaces, data, decisions. The rest can be house format, provided it is machine-readable and gated.

### Documentation that does not rot — three honest tiers

The drift gate cannot verify everything. Claiming otherwise is the same overclaim being removed from the manifesto.

**Tier 1 — machine-verified.** OpenAPI, JSON Schema, DMN, state machines, EARS criteria compiled to tests. The drift gate compares these to code directly. A mismatch blocks the merge. Aim to put as much as possible in this tier.

**Tier 2 — link-verified.** Glossary terms against identifiers, rules against their `source_ref`, ADRs against the components they govern. The gate cannot judge whether the prose is still true, but it can verify that every reference still resolves. A broken reference blocks; stale wording does not.

**Tier 3 — staleness-flagged.** ADR rationale, architecture narrative, the reasoning behind a decision. No mechanism can verify these. Instead, record which code paths each one references; when those paths change materially, raise a review task automatically. Not verification — but far better than silence, which is how documentation dies.

Tell the client this three-tier split plainly. "Everything is automatically verified" is not credible to a senior architect; "here is exactly what we verify, what we link-check, and what we flag for human review" is.

---

## 13. Right-sizing — two paths, not one

Running the full loop for every change is the failure mode observed in every SDD tool review: a small bug becomes four user stories and sixteen acceptance criteria, and the team abandons the method by the second sprint.

**The deciding question: does this change touch a `rule_id`?**

| Path | When | Steps |
|---|---|---|
| Full loop | The change adds, alters or retires a business rule | rule → verify → decide → spec → code → gates |
| Light path | Technical change with no rule impact — refactoring, performance, library upgrade, logging | code → gates |

The light path still passes all four gates. What it skips is specification, not verification. A refactor that leaves behaviour unchanged is proven by the existing parity tests; it does not need a new spec.

This distinction belongs in the Definition of Ready as an explicit question, not as an unwritten judgement call.

---

## 14. Notation policy — prefer the most formal notation that fits

Only machine-readable artefacts can be gated. Prose cannot be verified, only flagged. So the share of the specification surface expressed formally directly determines how much of the documentation stays alive.

| Content | Notation | Verifiability |
|---|---|---|
| HTTP interfaces | OpenAPI | Fully machine-verified against implementation |
| Events and messages | AsyncAPI | Fully machine-verified |
| Data shapes and constraints | JSON Schema | Fully machine-verified |
| Calculation and decision rules | DMN decision tables | Executed, or compiled to tests |
| Lifecycle and state | State/transition YAML | Illegal transitions fail a generated test |
| Behavioural acceptance criteria | EARS, Gherkin | Constrained natural language — compiled to acceptance tests |
| Rule statement | One sentence in a structured field | Human-judged; `source_ref` link-checked |
| Glossary definition | Short prose in a structured field | `aliases_in_code` link-checked |
| ADR rationale | Free prose | Staleness-flagged only |

**Do not force a formal notation where the content does not fit.** An ADR's reasoning written as YAML is worse than an ADR's reasoning written as a paragraph. The goal is not maximum formality everywhere; it is choosing the most formal notation each piece of content genuinely supports.

**Do not invent notations either.** Where a standard exists — OpenAPI, AsyncAPI, JSON Schema, DMN — use it. It is portable, tool-supported, and familiar to auditors. A small house YAML schema is acceptable only where no standard fits, as with lifecycle, field mapping and permissions.

### Formal coverage ratio

Report this as a metric alongside the others:

```
formal_coverage = machine-verified artefacts / total artefacts
```

It answers the question "how much of your documentation actually stays alive" with a number rather than a claim. Track it per context; a falling ratio means prose is accumulating faster than specification.

---

## 15. Artefact length limits — enforced, not advised

Verbose artefacts are the second failure mode of SDD tooling: reviewers face a pile of repetitive markdown and conclude they would rather read the code. Documentation nobody reads is already rotten, whether or not it is accurate.

| Artefact | Limit |
|---|---|
| Rule statement | One sentence |
| Rule card total | Roughly 15 lines |
| Specification | One screen |
| ADR | One page |
| Glossary entry | Two sentences plus structured fields |

Exceeding these is a generation-time failure, not a style preference. A skill that produces an over-long artefact has failed its own validation step, the same as one producing a rule without a source reference.


---

## 16. Relationship to GitHub Spec Kit

Spec Kit provides the surface layer — constitution, specify, clarify, plan, tasks, implement — plus integrations with 30+ coding agents. Rebuilding that layer would spend months to arrive somewhere worse, and it is not where the differentiation lives.

What it does not provide is the entire differentiator: no legacy index, no source-referenced rule extraction, no confidence levels, no characterization testing, no parity, no mutation scoring, and no deterministic gate that blocks a merge. Its checklists and consistency analysis are interpreted by an agent, which is help, not a guarantee.

### The split

| Layer | Owner |
|---|---|
| Constitution — carries the three team rules and the gate policy | Spec Kit |
| specify / clarify / plan / tasks / implement | Spec Kit |
| **Preset** — reshapes spec templates to require `rule_id`, `source_ref`, `confidence`, `disposition`, zero open brackets | **Ours** |
| **Extension** — adds `index`, `discover`, `verify`, `parity`, `gate` | **Ours** |
| **Bundle** — one-command install of the whole set | **Ours** |
| CI gates | **Ours, and independent of Spec Kit entirely** |

### The rule that keeps this cheap

**Artefacts, schemas, index, parity harness and gates must work with zero Spec Kit installed.** Spec Kit is a surface, not a source of truth. Preset and extension are thin wrappers around markdown instructions, schemas and CLI calls.

The test: if Spec Kit disappeared tomorrow, the loss should be about one week of rewrapping. If that answer ever grows to months, the coupling has gone too far.

### Language

The core stays .NET. Roslyn, ScriptDom, analyzer-packaged gates, Stryker.NET and NetArchTest have no equivalent elsewhere, and the target codebases are .NET. Spec Kit is Python, which is irrelevant — an extension command is a script that invokes a binary, and neither side cares what the other is written in.

**Open question to settle early:** whether the bank's security and procurement processes permit a Python plus uv install chain. If not, the Spec Kit leg is dropped and a thin CLI shell replaces it — the .NET core is unaffected either way.

### Positioning

The method is language-independent and belongs in the manifesto without a single mention of .NET. The toolchain targets .NET today, and the index is provider-structured so a second language is an added provider rather than a rewrite. The proof is .NET, factoring, banking.

> Regulated-industry legacy modernization with parity guarantees and an auditable trail — on .NET and SQL Server or Oracle today.

Not "an AI toolchain for .NET". The buyer's problem is risk, not language.

---

## 17. Metrics — the full set

Report these together. They are the answer to "how do we know this is working" and the honest alternative to claiming completeness.

| Metric | Meaning | Reported |
|---|---|---|
| Evidenced rule ratio | rules proven by a passing characterization test / total | per slice |
| Unexplained transaction rate | production transactions the rule set cannot predict — direct evidence of missing rules | per slice, iterate until it falls |
| Data access coverage | call sites resolved / total, by technology | Discovery Zero and per slice |
| Compilation coverage | projects analysed semantically / total | Discovery Zero |
| Formal coverage | machine-verified artefacts / total artefacts | per context |
| Parity coverage | share of migrated behaviour protected by a parity test | per slice |
| Open question age | anything unanswered beyond 10 days is the real project risk | weekly |
| Gate bypass count | rising means the method is dying | every retro |
| Discovery effort per slice | should fall from the third slice onward | per slice |

The last one is the evidence behind "each sprint gets faster" — a measured curve, not a promise.
