# Doc set — pending v0.5 revisions

The seven documents were imported with the `ansdm → specanchor` rename applied.
The following content revisions are owed before the doc set is called v0.5.
Each item lands in the named document; nothing is postponed without an entry here.

| # | Revision | Lands in |
|---|---|---|
| 1 | **Scaffold decision**: narrow `specanchor scaffold <rule-id>` (red acceptance test + empty skeleton from a rule id — deterministic, ours) stays in the CLI; free-form code generation remains delegated to the surface (Spec Kit / agent). Resolves the contradiction between toolset-spec §7 ("not built") and §9 step 8 + team-operating-agreement §4. | toolset-spec §7/§9, team-operating-agreement §4 |
| 2 | **Gate language fixed**: "4 PR gates (touch, boundary, drift, parity+mutation) + 2 catalog-production checks (source-ref, statement-quality)" everywhere; drop the ambiguous "four gates" vs six-row table mismatch. | toolset-spec §6 |
| 3 | **Cutover components**: reconciliation runner (composed from db-compare), cutover-evidence artefact schema, shadow-run metrics — foundation §9 step 5–6 made concrete. | toolset-spec §1/§2 (new component + schema) |
| 4 | **Dependency equalization**: Mockifyr composition for the parity harness (record & replay, email/SMS capture); external systems must be equalized before legacy/new outputs are comparable. | toolset-spec §5 |
| 5 | **Masking pipeline**: deterministic masking policy as a first-class artefact + D0 step (same input → same pseudonym, or parity breaks); data-handling annex reference. | toolset-spec §2/§11 |
| 6 | **diff-triage skill**: AI pre-classifies parity failures (parity violation / deviation candidate / normalize), human decides. Missing from the skill table. | toolset-spec §4 |
| 7 | **AI pack spec**: full-lifecycle skill catalog (boundary-modeler, mapping-assistant, spec-drafter preset, diff-triage, staleness-review), context-pack contract, MCP surface as a queryable graph, eval strategy fed by rig/TRAPS.md. | new doc: ai-pack-spec.md |
| 8 | **Method lifecycle doc**: D0–D6 phase map, dual-track rhythm, roles/RACI, the three human gates mapped (ADR-style), spec change management (versioning, supersedes, breaking-change policy). | new doc: method-lifecycle.md |
| 9 | **House-YAML justifications**: one-page "standards considered, why none fit" record for lifecycle/state YAML, calendar spec, mapping spec, permission matrix. | toolset-spec §12 appendix |
| 10 | **SBOM + signed provenance** in CI from day one (CRA Art. 14 obligations apply from 2026-09-11). | new: CI pipeline definition |
