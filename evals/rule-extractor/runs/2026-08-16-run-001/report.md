# rule-extractor eval — run 001 (2026-08-16)

**Setup:** agent executed the skill contract with `csharp-index.json`,
`sql-index.json`, `matrix.json`, the four SQL object bodies, `rule.schema.v1.json`
and the Temlik glossary entry as its ONLY inputs. Raw C# source forbidden.
Self-validation loop: the agent ran `specanchor gate` on its own output and
iterated until clean. Cards preserved under `cards/`.

**Result: gate green — 9 cards, 0 rejections. All 7 criteria met.**
The gate result was re-verified independently after the run.

## Scoring

| Criterion | Verdict | Evidence |
|---|---|---|
| E1 Trap B | ✅ | RULE-0002: bypass stated neutrally; open question "what is the third contract category (schema marks it `???`)? Is its exemption from the minimum floor intentional?" — not folded into the statement |
| E2 Trap C | ✅ | RULE-0009 from the dead-code candidate; first open question verbatim per contract: "this code appears unreachable — is the rule live?" |
| E3 Trigger | ✅ | RULE-0004, `kind: trigger`, rollback semantics captured, plus "limit is per single invoice, not cumulative exposure — intended?" |
| E4 Job | ✅ | RULE-0003, `kind: job`, weekday-02:00 + initial-status scope captured |
| E5 Trap A | ✅ | RULE-0001 raises the rounding question; RULE-0005 records the dual in-process/in-database computation and asks "which result is authoritative when they disagree?" — the exact handoff point to parity |
| E6 Self-validation | ✅ | One gate finding during the run (SA0202 — the glossary term appeared in no rule) fixed by rewording RULE-0006 and revalidating; zero silent drops |
| E7 Honesty | ✅ | Blind spots reported: C# index exposes no branch conditions/expression bodies; no writer of invoice status exists in the matrix (lifecycle undiscoverable); execution of the queued assignment writes invisible |

## Unplanned genuine findings (not in the answer key)

1. **The nightly job appends a new commission result per run with no dedup and
   no status change** — history table or defect? Nobody planted this as a trap;
   it is a real latent behaviour of the rig, found from the AST alone.
2. **Nothing in the system ever advances an invoice's status** — the lifecycle
   is undiscoverable from this context. True, and exactly the kind of finding
   Discovery Zero exists to surface.

## Improvement items fed back into the backlog

- The C# index should expose member branch conditions / expression surfaces so
  calculation internals can be extracted, not just located (REVISIONS #14).
- Literal flattening (`@pN`) inside quoted SQL context can read as if the
  placeholder text itself were persisted; the representation is correct for
  table resolution but worth a clarifying note in the index docs.
- The eval was orchestrated manually this run; the automated harness (agent-in-CI,
  nightly) remains owed under REVISIONS #7.
