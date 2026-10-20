# Answer Key — Planted Traps

This rig is the toolchain's acceptance test and the skills' eval corpus. Every component
in the chain (index → rule-extractor → domain-ledger → char-test-writer → parity) is
proven against it before touching client code. **A discovery run that misses any trap
below is a failing run.**

Do not "fix" these traps. They are the product.

## Trap A — Divergent rounding (C# vs T-SQL)

- `src/FactoringApp/Pricing/CommissionCalculator.cs` uses `Math.Round(x, 2)` —
  .NET default is **banker's rounding** (`MidpointRounding.ToEven`).
- `sql/002_usp_CalculateCommission.sql` uses `ROUND(x, 2)` — T-SQL rounds
  **half away from zero**.
- Guaranteed divergence case (a true midpoint at the 3rd decimal with an EVEN cent
  below it): **amount 4010.00 × rate 0.0125 = 50.125 → C# rounds to 50.12 (ToEven),
  SQL rounds to 50.13 (away from zero)**. Sample invoice 1004 in `Program.cs` is
  exactly this case. Decoys that agree in both engines (e.g. .375 midpoints, which
  round up in both) must NOT be reported as divergences.
- Expected outcome: parity harness detects it, classifies it as a rounding-class
  difference, and routes it to the known-differences register (KD entry), NOT a defect.

## Trap B — Undocumented edge case

- Both `CommissionCalculator.Calculate` and `usp_CalculateCommission` skip the minimum
  commission when `contractType == 3`. No comment, no documentation anywhere explains
  what contract type 3 is or why the minimum is bypassed.
- Expected outcome: rule-extractor surfaces it as an `open_questions` entry on the
  commission rule; char-test locates the exact failing records. It must NOT be silently
  folded into the rule statement as if it were intended behaviour.

## Trap C — Dead code

- `src/FactoringApp/Pricing/LegacyRebateCalculator.cs` is public and referenced by
  nothing. (Year-end rebate program cancelled years ago; code never deleted.)
- Expected outcome: index lists it as a dead-code candidate; module-inventory reports
  it; rule-extractor must NOT produce a rule from it without flagging it as dead.

## Trap D — One concept, two names (and a false friend)

- The assignment concept ("Temlik") appears as `AssignmentService.RegisterAssignment`
  AND `TransferHelper.RegisterTransfer`, writing to table `TemlikKayit`
  (column `IhbarTarihi` = notification date).
- False friend: `Accounting/CarryOverService.Devir` — the Turkish word "Devir" is used
  colloquially for both assignment and period carry-over, but this is a DIFFERENT
  concept in a DIFFERENT context.
- Expected outcome: domain-ledger produces ONE term (Temlik, context: Assignment) with
  `aliases_in_code: [AssignmentService, RegisterAssignment, TransferHelper,
  RegisterTransfer, TemlikKayit]` and a `not_to_be_confused_with` pointing at
  CarryOverService.Devir (Accounting). Two separate glossary entries — not one, not three.

## Trap E — A spec that answers what nobody decided

- `trap-specimens/SPEC-0002.yaml` is a DMN decision table for the commission rule
  (RULE-0042). Its contract-type-3 row states "no minimum commission" as plain
  behaviour, and its rounding row states half-to-even, with no `[OPEN — ...]` bracket
  anywhere. It counts its own brackets honestly (`open_brackets: 0`) and even carries an
  `approved_by`. It reads as finished.
- But RULE-0042 still has two open questions (Trap A's rounding, Trap B's contract type
  3) and `disposition: null`: no domain expert has decided either. The spec has quietly
  turned Trap B's unexplained legacy behaviour into the requirement, which is exactly
  what the discovery playbook forbids ("Do not resolve it yourself").
- Its clauses `[C1]`–`[C3]` are cited by no characterization test, so nothing proves
  them — the rule → spec → test chain is broken at the second link.
- The specimen lives OUTSIDE `discovery/` on purpose: the rig's discovery sample must
  stay green, and the honest spec for the same rule, `discovery/specs/SPEC-0001.yaml`,
  keeps both questions as open brackets and is cited clause by clause by CHAR-0042.
- Expected outcome: with the specimen dropped into `discovery/specs/`, the spec gate
  REJECTS it twice — once because it carries fewer open brackets (0) than its rule has
  undecided open questions (2), and once per clause no test cites. Rejected, not
  flagged, and not merely "not ready": an expert's signature on the spec does not
  answer a rule's open question — the rule's `disposition` and `decided_by` do.
  SPEC-0001, meanwhile, passes every gate and is reported NOT READY for a sprint (open
  brackets, no approval, no disposition), which is the correct state for it.

## Bonus — Rule hiding outside application code

- `sql/003_trg_Invoice_RiskLimit.sql`: the risk-limit check lives in an AFTER INSERT
  trigger, not in C#. An extraction that reads only application code misses it.
- `sql/004_job_NightlyCommissionRecalc.sql`: scheduling semantics (02:00 weekdays,
  only 'created' invoices) live in a job, not in code.
- Expected outcome: both appear in the rule catalog with `source_ref.kind: trigger|job`.
