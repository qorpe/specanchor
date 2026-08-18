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
| 11 | **MSBuildWorkspace loader**: the C# index currently builds an ad-hoc semantic compilation (all .cs under a root + runtime references + synthesized implicit usings). Correct for the rig and single-project trees; real client solutions (multi-project, conditions, NuGet references, source generators) need an MSBuildWorkspace-backed loader with per-project blind-spot reporting, per toolset-spec §3. The loader is a seam: the walkers stay unchanged. | core/index/csharp |
| 12 | **Mutation + license gates on this repo**: ~~mutation~~ SHIPPED — Stryker on the parity core, break 75, score 94.8%, in CI on every push (first run caught real gaps at 66.2%: relative tolerances, missing-record direction, field union — tests added). The **license gate** (dependency license allowlist script) is still owed. | CI pipeline |
| 13 | **Live execution adapters**: the rig's parity tests reproduce T-SQL semantics in-process; a real engagement needs a replay runner against a running SQL Server (read-only SqlClient, masked corpus) and a runner for the legacy application side. The comparator and policy are unaffected — runners only produce ParityRecord sets. | core/parity |
| 14 | **Member body surface in the C# index**: SHIPPED 2026-08-17 — every member now carries BranchConditions (if/while/ternary/switch conditions as written, syntax order, whitespace-collapsed, capped per entry; conditions only, never bodies). Trap B's bypass condition is extractable from C# by test. Found by eval run-001; the automated agent-in-CI harness is still owed under #7. | core/index/csharp |
| 15 | **Spec Kit surface**: verified 2026-08-16 — spec-kit now ships presets (`specify preset add`), extensions (`specify extension add`) and bundles (`specify bundle install`) as FIRST-CLASS mechanisms, exactly the seam toolset-spec §16 designed for. Build the specanchor preset (templates requiring rule_id/source_ref/confidence/disposition/zero open brackets), the extension (index/discover/verify/parity/gate commands calling our CLI) and the bundle during the REHEARSAL, so the templates are shaped by real use; the bank's Python+uv procurement answer decides whether the leg ships to the engagement or the thin CLI shell replaces it. The zero-Spec-Kit rule stands: everything works without it. | toolset-spec §16, new: surface/spec-kit/ |

## 16 · Oracle PL/SQL index adapter'ı — hazırlık planı (2026-08-18)

Tetik: hedef engagement'ın DB motoru sorusu (H1 #1). CDC izi ilişkisel DB'yi kesinleştirdi;
motor Oracle çıkarsa: SqlIndexer çıktı sözleşmesi AYNEN korunarak (Procedures/Triggers/
Reads/Writes/BranchCount) ANTLR PL/SQL dilbilgisiyle adapter yazılır — çekirdek ~1-2 hafta,
keşfin D0'una paralel; keşfi bloklamaz (ilk haftalar C#+doküman+davranış ayaklarında).
Kapsam raporu adapter gelene dek SQL ayağını "bekliyor" olarak dürüstçe gösterir.
Package/standalone prosedür dağılımı H1'de sorulur (efor modeli için). db-compare Oracle
desteği (kendi v2 planı) ile aynı takvim penceresine hizalanır.
