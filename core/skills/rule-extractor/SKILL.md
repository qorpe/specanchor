---
name: rule-extractor
description: Extract source-referenced business rules from the deterministic index of ONE bounded context. Never reads the raw repository; every rule card must survive the artefact validator or it is rejected.
---

# rule-extractor

## Input

- The C# index, the SQL index and the table read/write matrix for **one bounded
  context only** — never the whole system. You are given the context's glossary
  (if one exists yet) and its related procedures.
- You never read raw source files directly. If the index does not answer a
  question, that is a blind spot to report, not a gap to fill by guessing.

## Output

One rule card per rule, conforming to `core/schemas/rule.schema.v1.json`, written
to `discovery/<context>/rules/RULE-nnnn.yaml`.

## Procedure

1. Walk the context's members by descending cyclomatic complexity; branches are
   where rules live. Cover procedures, triggers AND job scripts from the SQL
   index — a factoring system keeps a large share of its logic there.
2. For every candidate rule, write ONE sentence in domain language. No code
   identifiers: "for non-recourse contracts", never "where contractType is 3".
3. Attach every `source_ref` you used: file + line span from the index, or
   object + kind (procedure | trigger | job) for SQL objects.
4. Anything you cannot explain from the evidence goes into `open_questions` —
   verbatim, precise, one entry per independently closable question. Do NOT
   resolve an ambiguity with a plausible assumption; the unanswered list is a
   deliverable, not a defect.
5. Set `confidence: inferred` for everything. Only the characterization loop
   may raise a rule to `evidenced`. Never set `disposition` — that is a human
   decision made in the validation session.
6. If the index marks a symbol as a dead-code candidate, you may still record
   the rule but its first open question must be "this code appears unreachable —
   is the rule live?".

## Self-validation — non-negotiable

Run every card through the artefact validator (`ArtefactValidator.ValidateRule`)
with the context's indexes and glossary aliases before returning:

- **SA0101/SA0102/SA0104 (source_ref does not resolve): the card is REJECTED.**
  Remove it from the catalog and list it in the run report with the finding.
- **SA0103 (code identifier in the statement): rewrite the sentence, revalidate.**
- **SA0002 (schema violation): fix the card, revalidate.**

A card is only returned when the validator returns zero findings. Length limits
are validation, not style: statement one sentence, card ~15 lines total.
