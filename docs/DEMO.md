# The PoC demo — ten minutes, live, no slides

Everything below runs against the rig, offline, from the repo root. Every command has
been verified; the red moments are deliberate and reversible. Narration cues in *italics*.

## 0. The cast (30s)

Open `rig/legacy-factoring/` in the editor. *"A miniature legacy factoring system —
application code plus stored procedures, with the classic pathologies planted on purpose:
a calculation that rounds differently in C# and SQL, an undocumented edge case, dead code,
one concept under two names, and rules hiding in a trigger and a nightly job. The answer
key is written down (`TRAPS.md`) — so every claim you are about to see is graded, not
narrated."*

## 1. The deterministic index (1 min)

```bash
dotnet run --project core/cli/SpecAnchor.Cli -- index --src rig/legacy-factoring/src --sql rig/legacy-factoring/sql --out /tmp/specanchor-demo
```

*"No model, no embeddings — Roslyn and ScriptDom, the compiler's own parsers. Note the
last number: coverage 1/1 — the tool tells you how much of the data access it resolved.
On a real system that number will NOT be 100%, and that honesty is the product."* Open
`/tmp/specanchor-demo/matrix.json` — point at the C# member writing to `TemlikKayit`:
*"which code touches which table, across languages, with file:line evidence."*

## 2. The gate — green in about a second (1 min)

```bash
dotnet run --project core/cli/SpecAnchor.Cli -- gate --discovery rig/legacy-factoring/discovery --src rig/legacy-factoring/src --sql rig/legacy-factoring/sql --schemas core/schemas
```

*"Seven gates over the rule catalog: every source reference resolves to a real symbol,
no code identifier leaks into a business sentence, every 'evidenced' claim cites a test
that exists, the arithmetic of every test record adds up. Locally, in a second — the
same code CI runs, so local and CI cannot disagree."*

## 3. The gate goes red — on purpose (1 min)

```bash
echo "rig/legacy-factoring/src/FactoringApp/Pricing/CommissionCalculator.cs" > /tmp/changed.txt
dotnet run --project core/cli/SpecAnchor.Cli -- gate --discovery rig/legacy-factoring/discovery --src rig/legacy-factoring/src --sql rig/legacy-factoring/sql --schemas core/schemas --changed /tmp/changed.txt
```

*"I just told the gate this PR changes the commission calculator. Watch: the TOUCH gate
blocks — the code a rule points at changed, but the rule and its test did not. This is
how a catalog stays alive instead of rotting the day after the consultant leaves."*

## 4. The rule cards an agent actually produced (2 min)

Open `evals/rule-extractor/runs/2026-08-16-run-001/report.md` and one card.

*"These nine cards were written by an agent that was FORBIDDEN from reading the C#
source — it saw only the index and the SQL bodies, and it had to run the gate on its own
output until clean. Graded against the answer key: 7/7. The undocumented contract-type
exemption surfaced as an open question, not silently folded into a rule. And it found two
things nobody planted: the nightly job appends results with no dedup, and nothing in the
system ever advances an invoice's status. That is discovery — the unknowns list is the
deliverable."*

## 5. Parity — the one-kuruş story (2 min)

```bash
dotnet test tests/SpecAnchor.Parity.Tests --nologo
```

Open `tests/SpecAnchor.Parity.Tests/RigParityTests.cs` at the Trap A test.

*"The new side runs the rig's real calculator; the legacy side reproduces the stored
procedure's rounding. Invoice 1004: 50.13 vs 50.12 — one kuruş. The harness classifies it
as a ROUNDING difference, not a defect. It stays red until the business signs a
known-difference entry — name, date, rule — and then it is a visible, recorded decision.
Silent differences are how money leaks; signed ones are how audits pass."*

## 6. Red-first development (1 min)

```bash
dotnet run --project core/cli/SpecAnchor.Cli -- scaffold --rule rig/legacy-factoring/discovery/rules/RULE-0042.yaml --out /tmp/specanchor-demo/scaffold
cat /tmp/specanchor-demo/scaffold/RULE-0042.Acceptance.cs
```

*"Development starts from the rule: a deliberately failing acceptance test carrying the
rule id and its open-question count. The implement and test-writer skill contracts take
it from here — every test compiled from the approved spec's example rows, a coverage map
proving nothing was skipped — and the MR cannot open until this red is green."*

## 7. Close (30s)

*"Sixteen commits, 74 acceptance tests bound to a written answer key, a 94.8% mutation
score on the comparison engine, license and touch gates in CI, and one agent-run graded
7/7. Nothing here has touched client code — by design: the next step is rehearsing the
full migration on this rig, then a two-week discovery PoC on one module the client
chooses."*

## Do not say

- Any duration or price for a real engagement (discovery first — always).
- "It works on any codebase" — today it is proven on the rig; real solutions need the
  MSBuildWorkspace loader and the data-access recognizers (REVISIONS #11, #14).
- "AI doesn't make mistakes" — the claim is that unvalidated output cannot pass the gate.
