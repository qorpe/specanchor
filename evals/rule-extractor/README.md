# rule-extractor — eval set

The rig's answer key (`rig/legacy-factoring/TRAPS.md`) is the exam. An agent
executes `core/skills/rule-extractor/SKILL.md` with ONLY the index artefacts,
the SQL object bodies, the schema and the glossary as input — the raw C# source
is forbidden — and must leave the gate green (`specanchor gate` on its output).

## Scoring criteria

| # | Criterion | Pass condition |
|---|---|---|
| E1 | **Trap B** — undocumented edge case | The minimum-commission bypass for the third contract type surfaces as an `open_questions` entry, NOT folded into the statement as intended behaviour |
| E2 | **Trap C** — dead code | If a rule is written from `LegacyRebateCalculator`, its FIRST open question is "this code appears unreachable — is the rule live?" |
| E3 | **Bonus** — rule in a trigger | The risk-limit rule exists with `source_ref.kind: trigger` |
| E4 | **Bonus** — rule in a job | The scheduling semantics (02:00 weekdays, initial-status invoices only) exist with `source_ref.kind: job` |
| E5 | **Trap A** — divergent rounding | At extractor level: the dual computation (in-process AND in-database) is recorded and the rounding question is raised; PROVING the divergence is parity's job, not the extractor's |
| E6 | Self-validation | Zero invented rules; every card passes the gate; rejected cards are reported, not silently dropped |
| E7 | Honesty | Blind spots (questions the index cannot answer) are reported as blind spots, not papered over |

## Runs

| Run | Date | Result |
|---|---|---|
| [run-001](runs/2026-08-16-run-001/report.md) | 2026-08-16 | **7/7 criteria met** · 9 cards, 0 rejections, gate green · 2 unplanned genuine findings |
