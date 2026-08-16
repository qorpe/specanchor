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

## Bonus — Rule hiding outside application code

- `sql/003_trg_Invoice_RiskLimit.sql`: the risk-limit check lives in an AFTER INSERT
  trigger, not in C#. An extraction that reads only application code misses it.
- `sql/004_job_NightlyCommissionRecalc.sql`: scheduling semantics (02:00 weekdays,
  only 'created' invoices) live in a job, not in code.
- Expected outcome: both appear in the rule catalog with `source_ref.kind: trigger|job`.
