# Discovery PoC Playbook

**Scope:** one real module, two weeks, run before the transformation scales.
**Goal:** prove three things — rules can be extracted with traceable sources, extraction can be *validated* rather than trusted, and one rule can travel the full loop from legacy code to a green test on new code.
**Version:** v0.1

---

## Part 1 — What you produce

Four artefacts. Everything else waits.

### 1.1 Business Rule Catalog

One file per rule, or one file per context with one block per rule. Machine-readable header, human-readable body.

**Schema**

| Field | Meaning |
|---|---|
| `rule_id` | Stable identity. Travels to spec, test, commit. Never reused. |
| `context` | Bounded context this rule belongs to |
| `statement` | One sentence, in domain language, no code terms |
| `source_ref` | File:line, stored procedure, or config key. Multiple allowed. |
| `confidence` | `evidenced` / `inferred` / `disputed` |
| `evidence` | Test ID that proves it, or why it could not be proven |
| `open_questions` | What a domain expert must answer |
| `disposition` | `keep` / `change` / `retire` — filled during the workshop, not before |

**Worked example**

```yaml
rule_id: RULE-0042
context: Factoring.Pricing
statement: >
  For a domestic recourse factoring transaction, commission is calculated as
  the invoice amount multiplied by the customer's commission rate, and is
  never lower than the minimum commission defined on the customer's contract.
source_ref:
  - src/Factoring.Core/Pricing/CommissionCalculator.cs:812-847
  - dbo.usp_CalculateCommission (lines 34-61)
confidence: evidenced
evidence: CHAR-0042 passes against legacy on 240 historical transactions
open_questions:
  - Rounding is half-up in C# but banker's rounding in the stored procedure.
    Which is authoritative? Undocumented. Produces a 1 kuruş delta.
  - Minimum commission is bypassed when contract_type = 3. No rule found
    anywhere explaining why. Nobody asked so far could explain it.
disposition: null   # filled in the validation workshop
```

The `open_questions` block is the most valuable thing in the entire artefact. Do not clean it up — it is the evidence of what nobody knew.

### 1.2 Domain Ledger

The glossary. One entry per term, per bounded context.

```yaml
term: Temlik
context: Factoring.Assignment
definition: >
  Transfer of a receivable from the supplier to the factor, taking legal
  effect when notification reaches the debtor.
aliases_in_code: [Assignment, Transfer, Devir]
not_to_be_confused_with: >
  Devir (Factoring.Accounting) — carrying a balance to the next period.
  Same Turkish word, different concept, different context.
source_ref: src/Factoring.Core/Assignment/AssignmentService.cs:120
status: confirmed_by_expert
```

The `aliases_in_code` and `not_to_be_confused_with` fields are what make this usable by an agent later. A glossary without them is decoration.

### 1.3 Characterization Test

Freezes legacy behaviour. Written against the **legacy system**, not the new one.

```yaml
test_id: CHAR-0042
rule_id: RULE-0042
target: legacy
method: >
  Replay 240 historical transactions from the last 6 months through
  usp_CalculateCommission and compare against the stored commission values.
result: 238 pass, 2 fail
failures: >
  Both failures are contract_type = 3 records where the minimum commission
  was bypassed. This is the behaviour described in RULE-0042.open_questions.
```

Note what happened here: the test did not just confirm the rule, it **located the ambiguity precisely**. That is the demo.

### 1.4 Gap & Ambiguity Register

One list, ranked by risk. Three columns: what is unclear, what breaks if we guess wrong, who can answer it.

This is the artefact you put on screen first in the closing session.

---

## Part 2 — The one rule that travels the full loop

Pick this rule in week 1. Criteria: calculation-based, few dependencies, business-meaningful.

**Stage 1 — extracted**
`RULE-0042`, source-referenced, confidence `inferred`.

**Stage 2 — validated**
`CHAR-0042` runs against legacy. Confidence becomes `evidenced`. Two failures become open questions.

**Stage 3 — specified**
Acceptance criteria in EARS notation:

> When a domestic recourse factoring transaction is priced, the system shall calculate commission as invoice amount × contract commission rate.
> When the calculated commission is lower than the contract minimum commission, the system shall apply the contract minimum commission instead.
> Where contract type is 3, the system shall **[OPEN — pending expert decision]**.

The bracketed gap stays visible in the spec. Do not resolve it yourself. A spec that honestly carries its unknowns is the whole argument.

**Stage 4 — designed**
Which aggregate owns the commission calculation, why pricing is a separate bounded context from assignment, recorded as an ADR. One page.

**Stage 5 — implemented**
New code. Commit message references `RULE-0042`.

**Stage 6 — proven**
The same characterization test, now run against the new implementation. Green. Legacy and new behave identically.

Put the six stages on one slide with the same ID running through all of them. That slide is the PoC.

---

## Part 3 — Skill contracts

Four skills. Each declares input, output, and — non-negotiable — its own validation step.

| Skill | Input | Output | Self-validation |
|---|---|---|---|
| `module-inventory` | Roslyn index, DB schema, job config | inventory file, dead-code list | every entry resolves to a real symbol |
| `rule-extractor` | Roslyn index + SP bodies for one module | Business Rule Catalog | every rule has a resolvable `source_ref`; no `source_ref` → rule is rejected |
| `domain-ledger` | rule catalog + identifier names | glossary | every term appears in at least one rule |
| `characterization-test-writer` | one rule | runnable test against legacy | test executes; result recorded, pass or fail |

**Design rule:** a skill that produces without validating is not part of this method. The commercial claim rests entirely on the validation half.

**Do not feed agents the raw repository.** Build the deterministic index first:
- Roslyn: type inventory, call graph, dead code, complexity hotspots
- Dependency graph: project and layer boundaries
- Database: schema, FKs, triggers, and **stored procedure bodies**
- Scheduled jobs and configuration

In a legacy factoring system a large share of the business logic will be in stored procedures. An extraction that reads only C# will look competent and be wrong.

---

## Part 4 — Two-week plan

**Week 0 (start today, runs in parallel)**
- Model access and data-boundary approval — the long pole; weeks, not days
- Read-only repo access, DB schema access, test environment
- A named domain expert allocated, with time blocked
- Two or three candidate modules offered to the client; **let them choose**

**Week 1**
- Day 1–2: deterministic index over the chosen module
- Day 3–4: rule extraction; every rule source-referenced
- Day 5: pick the rule that will travel the full loop; draft the domain ledger

**Week 2**
- Day 1–2: characterization tests; assign confidence levels
- Day 3: half-day validation workshop — **only on the red items**, not a walkthrough
- Day 4: run the chosen rule through stages 3–6
- Day 5: closing session

**Closing session, in this order**
1. The module — chosen by them
2. Raw inputs and how stale the existing documentation turned out to be
3. Rule count, source references, confidence distribution
4. Contradictions and unexplained behaviours ← spend the most time here
5. `RULE-0042` travelling all six stages
6. Slicing plan and how it fits their sprint flow

Do not state a delivery-duration number. Not in this session.

---

## Part 5 — What to study

Ranked by what will actually be tested in the room.

1. **Factoring domain** — recourse vs non-recourse, domestic vs export, assignment and debtor notification, cheque/note discounting, risk and limit management, commission and interest calculation, accounting treatment. Highest return per hour by a wide margin.
2. **Turkish regulatory frame** — factoring under Law 6361, BDDK reporting obligations, retention requirements. One output of discovery should be a list of regulation-driven constraints.
3. **Characterization testing** (Feathers) — the technical basis of every parity claim you make.
4. **Event Storming facilitation** — a room skill, not a reading skill. Rehearse one internally before facing the client's experts.
5. **Strategic DDD** — context boundaries and context mapping. Harder than tactical DDD and the source of your differentiation.
6. **EARS notation** — an afternoon. Removes ambiguity from acceptance criteria and looks rigorous to an auditor.

You do not need to study the AI tooling side. That is already your strength.

---

## Part 6 — Failure modes to watch

- **Extraction that reads only application code.** Stored procedures and scheduled jobs hold rules. Miss them and the catalog is confidently incomplete.
- **A model that looks too clean.** People approve polished output without arguing. Open the workshop with "break this," not "approve this," and show one example where the extraction was wrong.
- **Resolving open questions yourself.** The unanswered list is the deliverable, not a defect.
- **Skipping the working test.** Discovery without a running characterization test is a Word document — and that is exactly what the first attempt produced.
- **Committing to a number before discovery ends.** The behaviour that sank the program the first time.
