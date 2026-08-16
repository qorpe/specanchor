---
name: char-test-writer
description: Turn one rule into a runnable characterization test against the RUNNING legacy system, and record the result either way. A test that did not execute does not exist; a failure that locates an ambiguity is a finding, not a defect.
---

# char-test-writer

## Input

Exactly one rule card, plus the comparison policy and access to the legacy
execution target (test environment or replay corpus). Never more than one rule
at a time.

## Output

- A runnable test (replay preferred, synthetic where history is thin).
- A char-test record conforming to `core/schemas/char-test.schema.v1.json`,
  written to `discovery/<context>/tests/CHAR-nnnn.yaml`, with `target: legacy`.

## Procedure

1. Choose the method honestly: `replay` when historical transactions exist for
   the rule's path, `synthetic` to cover branches history misses. Record the
   real `sample_size` — never round it up.
2. Apply the comparison policy: scrub timestamps, sequences and generated
   identifiers; compare under the declared tolerances, not byte equality.
3. **Execute the test against the running legacy system.** Record the result
   whether it passes or fails. A failing sample is often the discovery: link
   each failure to the rule's `open_questions` when it locates an ambiguity
   (e.g. "both failures are the records where the minimum is bypassed").
4. Expect a large share of early failures to be decimal rounding differences
   between C# and T-SQL, not logic. Classify them and route them toward the
   known-differences register — do not report them as defects.
5. You never update the rule's `confidence` yourself. The pipeline raises a
   rule to `evidenced` only when its test ran and passed; your job ends at the
   recorded result.

## Self-validation — non-negotiable

Run the record through the artefact validator before returning:

- **SA0301 (rule_id not in the catalog): the record is REJECTED.**
- **SA0302 (passed + failed ≠ sample_size): the record is REJECTED** — an
  arithmetic hole here means the run report cannot be trusted.
- **SA0002 (schema violation): fix and revalidate.**

A record is only returned when the validator returns zero findings AND the
test actually executed.
