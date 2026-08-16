# Preparation Plan

**Purpose:** what to read, in what order, and what to produce, before the first slice starts.
**Version:** v0.1

---

## Part 1 — Reading, in order

Two weeks of reading, done alongside building. Not a prerequisite to starting.

### Tier 1 — read these (about 8 days)

**1. Vlad Khononov — *Learning Domain-Driven Design* (O'Reilly, 2021)**
The best single entry point. Covers strategic design (bounded contexts, context mapping) and tactical patterns, and unlike older DDD books it treats legacy and brownfield situations directly. Read the strategic half properly; skim the tactical half — that part you already know.

**2. Nick Tune & Jean-Georges Perrin — *Architecture Modernization* (Manning, 2024)**
The closest thing to a book about this exact project: DDD applied to legacy modernization, slicing strategy, and how team structure follows domain boundaries. Read after Khononov, while the context map draft is in progress.

**3. Michael Feathers — *Working Effectively with Legacy Code***
Read the characterization-testing chapters, not the whole book. This is the technical basis of every parity claim being made to the client.

**4. Gojko Adzic — *Specification by Example***
There is no canonical book on spec-driven development. This is its closest ancestor and it covers the part that matters — turning business rules into executable specifications and keeping them alive. Read it as the SDD reference.

### Tier 2 — read selectively (about 2 days)

**5. Alberto Brandolini — *Introducing EventStorming***
Read the facilitation chapters only. The goal is running the sessions, not mastering the notation.

**6. Cyrille Martraire — *Living Documentation***
Skim. Confirms the thesis and supplies vocabulary for the writing series. Not needed to start.

**7. Neal Ford et al. — *Building Evolutionary Architectures***
The fitness-functions chapter. This is the conceptual basis of the boundary gate.

### Tier 3 — reference only, do not read cover to cover

- **Eric Evans — *Domain-Driven Design* (the blue book).** Look things up in it. Reading it front to back costs months and the strategic material is available faster elsewhere.
- **Vaughn Vernon — *Implementing Domain-Driven Design*.** Reference for aggregate design rules.

### Short items — a few hours total

- EARS notation (Mavin et al., *Easy Approach to Requirements Syntax*) — an afternoon, high return
- ADR format (Michael Nygard's original article) — 20 minutes
- C4 model (c4model.com) — 30 minutes
- Strangler Fig (Martin Fowler's article) — 20 minutes
- DMN specification — decision tables, hit policies, FEEL. Study this one properly; it carries the factoring calculation rules.
- Scrum Guide — one hour, purely for shared vocabulary with the team

### Not on the list

Prompt engineering, RAG and vector databases, fine-tuning, LLM gateway design. The index is deterministic and symbol-level; semantic similarity cannot produce an auditable rule catalog.

---

## Part 2 — What to produce

### Already exists (from prior work)

| Artefact | Use |
|---|---|
| Objection handling set | Before any client meeting where AI capability is questioned |
| Discovery PoC playbook | Artefact schemas, worked rule example, two-week plan |
| Team operating agreement | Handed to the team in Sprint 0 |
| Method architecture diagrams | Method plane, slice cycle, dual-track sprints, layered system |

### Still to produce

**1. Factoring domain notes — highest priority**
A personal glossary written before touching client code: recourse vs non-recourse, domestic vs export, assignment and debtor notification, cheque and note discounting, risk and limit management, commission and interest calculation, accounting treatment. This becomes the seed of the Domain Ledger, so write it in the ledger's format from the start rather than as loose notes.

**2. Draft context map**
Drawn while reading, revised after the code index runs. Marked `proposed`, never `accepted`. This is the input to the Sprint 0 workshop — the workshop validates it rather than starting from a blank wall.

**3. Manager presentation — five slides**
Problem · approach (the method plane) · what will be built, with effort and timeline · what is needed (access, expert, PoC time) · what will be measured. No manifesto, no naming, no product ideas, no duration promises.

**4. Fake legacy system**
A small .NET application plus three or four stored procedures, containing on purpose: a commission calculation that rounds differently in C# and SQL, an undocumented edge case, a dead code block, and one concept appearing under two names. Run the entire pipeline against it. This is both the safest place to learn and the first demo available before client access arrives.

**5. Method on a page**
One page, for yourself: the six stages, the four gates, the three team rules. Everything else derives from it. If it does not fit on one page, the method is too complicated to hold in a room.

---

## Part 3 — Two-week schedule

| Days | Reading | Building |
|---|---|---|
| 1–3 | Khononov, strategic half | Draft context map on paper |
| 4–5 | Factoring domain | Domain notes in ledger format |
| 6–7 | Tune, *Architecture Modernization* | Slice list and ordering |
| 8 | Feathers, characterization chapters | Roslyn simulation: call graph plus one analyzer |
| 9–10 | Adzic, *Specification by Example* | Fake legacy system |
| 11–12 | EARS, DMN, ADR, C4 | Run the pipeline against the fake legacy |
| 13–14 | Brandolini, facilitation | Manager presentation, method on a page |

Access requests go out on day one and run in the background throughout. They are the critical path — nothing on this schedule depends on them, and everything after it does.
