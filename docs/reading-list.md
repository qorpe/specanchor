# Reading List — In Priority Order

**Version:** v0.2

Read top to bottom. Each item's position is deliberate: earlier items make later ones faster.

---

## 0. Before anything else

**Factoring domain**
Not a book — your own notes. Recourse vs non-recourse, domestic vs export, temlik and ihbar, cheque/note discounting, risk and limit management, commission and iskonto calculation, valör, BSMV, accounting treatment.
*Why first:* without it you cannot tell whether an extracted rule is correct, and that is exactly where you will be tested.
**Time:** 2 days

---

## 1. Vlad Khononov — *Learning Domain-Driven Design*
O'Reilly, 2021
Read the strategic half properly (bounded contexts, context mapping, subdomains). Skim the tactical half — you already know it.
*Why here:* your weak spot is boundary drawing, not aggregate implementation. This is the fastest route to it, and unlike older DDD books it addresses brownfield directly.
**Time:** 3 days

## 2. Birgitta Böckeler — *Understanding Spec-Driven Development: Kiro, spec-kit, and Tessl*
https://martinfowler.com/articles/exploring-gen-ai/sdd-3-tools.html
*Why here:* gives you the spec-first / spec-anchored / spec-as-source vocabulary you will use to position yourself, and names the gaps your method fills.
**Time:** 1 hour

## 3. Microsoft (Apoorv Gupta) — *Spec-Driven Development: A Spec-First Approach to AI-Native Engineering*
https://developer.microsoft.com/blog/spec-driven-development-ai-native-engineering/
*Why here:* the "translation loss" framing for your manifesto, plus the Spec Kit lifecycle as a reference point.
**Time:** 30 minutes

## 4. GitHub Spec Kit — documentation
https://github.com/github/spec-kit
Read three things specifically:
- `spec-driven.md` — the full methodology write-up
- the Extensions and Presets reference at https://github.github.io/spec-kit/ — this is the seam you will build on
- the Evolving Specs guide (`docs/guides/evolving-specs.md`) — their brownfield loop

*Why here:* you are building on its surface layer, so you need to know exactly what it gives you and where its guarantees stop.
**Time:** 2 hours

## 5. Nick Tune & Jean-Georges Perrin — *Architecture Modernization*
Manning, 2024
*Why here:* the closest book to your actual project — DDD applied to legacy, slicing strategy, team structure following domain boundaries. Read after Khononov, while your draft context map is in progress.
**Time:** 2 days

## 6. Michael Feathers — *Working Effectively with Legacy Code*
Read only the chapters on characterization testing and seams.
*Why here:* the technical basis of every parity claim you make to the client. Seams are what make tangled legacy testable at all.
**Time:** 1 day

## 7. Gojko Adzic — *Specification by Example*
*Why here:* there is no canonical SDD book. This is its closest ancestor and covers what matters — turning business rules into executable specifications and keeping them alive.
**Time:** 1.5 days

## 8. DMN specification — decision tables, hit policies, FEEL
https://www.omg.org/dmn/
*Why here:* study properly, do not skim. This carries the pricing, commission and limit rules, and it connects directly to your Camunda background.
**Time:** 1 day

---

## Short items — a few hours total

**Alistair Mavin — EARS (Easy Approach to Requirements Syntax)**
https://alistairmavin.com/ears/
Five sentence patterns. Highest return per hour on this whole list.
**Time:** 1 afternoon

**Michael Nygard — Architecture Decision Records**
https://adr.github.io
Format and rationale. Templates collected at the same site.
**Time:** 20 minutes

**Simon Brown — C4 model**
https://c4model.com
Four zoom levels for architecture diagrams.
**Time:** 30 minutes

**Martin Fowler — Strangler Fig Application**
https://martinfowler.com/bliki/StranglerFigApplication.html
**Time:** 20 minutes

**Scrum Guide**
https://scrumguides.org
Only for shared vocabulary with the team.
**Time:** 1 hour

---

## Skim, after the above

**Alberto Brandolini — *Introducing EventStorming*** (Leanpub)
Facilitation chapters only. This is a room skill; rehearse a session internally rather than reading further.

**Neal Ford, Rebecca Parsons, Patrick Kua — *Building Evolutionary Architectures***
The fitness-functions chapter. Conceptual basis of the boundary gate.

**Cyrille Martraire — *Living Documentation***
Vocabulary for the writing series. Not needed to start.

---

## Reference only — do not read cover to cover

**Eric Evans — *Domain-Driven Design*** (the blue book). Look things up in it.
**Vaughn Vernon — *Implementing Domain-Driven Design***. Reference for aggregate design rules.

---

## Deliberately not on this list

Prompt engineering · RAG and vector databases · fine-tuning · LLM gateway design.

None of these appear in your architecture. The index is deterministic and symbol-level; semantic similarity cannot produce an auditable rule catalog.

---

## Learn while building, not before

Roslyn · `Microsoft.SqlServer.TransactSql.ScriptDom` · Verify (snapshot testing) · Stryker.NET (mutation testing) · NetArchTest or ArchUnitNET (fitness functions).

These have immediate feedback — they either work or they do not. Pick each one up as you write the component that needs it, rather than setting aside separate study time.
