# AI Delivery — Objection Handling Set

**Purpose:** ready answers for the questions that will come up when an enterprise client (bank, insurer, regulated institution) asks *"what is your AI capability?"*
**Audience:** you, before the room. Not a client handout.
**Version:** v0.1 — update after every client meeting with the questions that actually got asked.

---

## 0. The one-line position

> We do not sell AI capability. We sell a **delivery method that makes AI-generated work auditable** — every business rule traceable to source, every behaviour provable against the legacy system, every merge approved by a named human.

If you only remember one thing in the room, remember that the client is not buying speed. They are buying **removal of the risk that this program fails again.**

**Three claims you are allowed to make. Nothing beyond them:**

1. Discovery and documentation recovery get dramatically faster — months of senior-engineer reading becomes supervised agent work.
2. Business-rule validation, cutover and regulatory sign-off do **not** get faster. Those run at human speed.
3. What is produced is traceable end to end: business rule → specification → test → commit.

Saying claim 2 out loud, unprompted, is the single highest-trust move available to you. Everyone who promised "50% faster on everything" has already been in that room, and the program still failed.

---

## 1. Data, security, and confidentiality

**Q: Does our source code leave our environment? Where does it go?**
Have the deployment topology drawn before the meeting, with three options priced:
- (a) our environment, client data under contractual controls
- (b) client VPC / tenant — agents run inside their boundary
- (c) fully on-premise with self-hosted models

State which you recommend for their risk class and why. Never answer "we'll look into it" — that ends the meeting.

**Q: Is our code used to train models?**
Answer with the concrete commercial terms of whichever provider is in your topology, not a general reassurance. Bring the retention and training-exclusion terms in writing. If you cannot state them precisely, say you will send them in writing within 24 hours — and do.

**Q: What about customer PII in the databases we profile?**
Data profiling runs on schema and statistical distributions, not on record contents. Where record-level inspection is genuinely needed, it happens in a masked or synthetic copy inside the client environment. Bring this as a written data-handling annex.

**Q: Which of our systems do you connect to, and with what rights?**
Bring the access matrix: system, read/write, purpose, duration. Everything read-only unless there is a stated reason. Security will be in the room — this table is what they came for.

**Q: What if the model provider changes terms or is unavailable?**
The method is model-independent. Models are a replaceable component; the artefacts, gates and standards are not. This is a genuine differentiator — say it plainly.

---

## 2. Accountability and audit

**Q: If AI wrote the code, who is responsible for it?**
Code comes from the agent; responsibility stays with a named human. Every merge has an approver recorded against a specification. No artefact reaches production without a human acceptance gate.

**Q: What do I show an auditor / the regulator?**
The traceability chain: business rule → specification → test → commit. When the examiner asks "where is this rule enforced?", it is one lookup — not an archaeology project. Point out that in a conventional project this chain does not exist at all; this is a **net gain in auditability**, not a new risk.

**Q: How do we know the AI did not invent a business rule?**
Two mechanisms:
- **Confidence levels.** Every extracted rule is labelled *evidenced* / *inferred* / *needs validation*. Nothing is presented as fact that has not been proven.
- **Characterization tests.** A rule is only marked *evidenced* when a test derived from it passes against the running legacy system. If the agent invented it, the test fails.

The claim is not "AI does not hallucinate." The claim is "unvalidated output cannot pass the gate."

**Q: How do you prevent the new system from silently diverging from the specification over time?**
Spec–code divergence (spec drift) is treated as a blocking merge condition, not a documentation hygiene issue. The gate runs in CI. This is also what keeps the documentation alive after handover, which is the failure mode that destroys most modernization programs.

**Q: Can you prove the new system behaves like the old one?**
That is what the parity specifications are for. Legacy behaviour is frozen as characterization tests before anything is rewritten; the new slice runs against the same tests. Where feasible, shadow-run both and compare outputs on production traffic.

---

## 3. Method and delivery

**Q: What exactly do we get at the end of discovery?**
Name the artefacts. Do not describe a process:
System Inventory · Domain Ledger (ubiquitous language) · Business Rule Catalog (source-referenced) · Context Map · Data Model & Data Quality Report · Gap & Ambiguity Register · Confidence Report · Parity Test Strategy + first working test set · Migration Strategy & Risk Register.

**Q: How is this different from what any consultancy would do?**
Three differences, in this order:
1. **Evidence-first domain discovery.** The workshop does not start at a blank wall. It starts with a model extracted from the actual code, with gaps and contradictions already marked in red. Their experts spend their time on the contradictions, not on recall.
2. **Everything traceable to source.** Every rule carries its origin (`FactoringService.cs:812`), not a consultant's recollection.
3. **Executable standards.** The method is not a slide deck; each acceptance gate is implemented and runs in the pipeline.

**Q: Are you replacing our people / our Agile process?**
No. Sprints, backlog, ceremonies stay. What changes is the artefact flow underneath them: the specification, not the ticket, becomes the durable unit — and the rules and tests survive after the team rotates.

**Q: What happens when your team leaves?**
The artefacts are in their repository, not in our heads: domain glossary, rule catalog, tests, ADRs. Then say the hard thing out loud — *the previous attempt failed partly because the knowledge lived in people and the people left.* This is the sentence that wins the room.

**Q: Which standards do you use? Did you invent this?**
Nothing invented. Event Storming · DDD strategic patterns · EARS acceptance criteria · Gherkin · C4 · ADR · characterization testing · Strangler Fig · fitness functions. If they have an enterprise-architecture function, map the resulting bounded contexts onto BIAN service domains — that turns "the consultant's model" into "a model that sits on an industry reference."

---

## 4. Commercial and risk

**Q: You claim three years of work in two. Prove it.**
Do not defend the number as a number. Decompose it:
- discovery and documentation recovery — large, demonstrable compression
- dead-rule elimination — scope typically shrinks once rules nobody can justify are removed
- rule validation, cutover, regulatory approval — no compression claimed

Then refuse to commit to the figure before discovery. Offer instead: *"After the discovery phase we will give you a defensible plan with a defensible number. Committing to a number today is exactly what went wrong last time."*

**Q: Why should we believe you, given this failed before?**
Do not distance yourself from the failure — use it. Ask what the post-mortem said, and if there wasn't one, note that as finding number one. Position the discovery phase as the thing that was missing: the previous attempt started building before anyone could state what the system actually does.

**Q: What do you want us to commit to right now?**
A paid discovery phase of 4–6 weeks on one real slice. Not the transformation. Small commitment, concrete artefacts, evidence before scale. Anything larger is the wrong ask for a program with a failure in its history.

**Q: Can we just buy the AI tools and do this ourselves?**
Yes — and they should own the tools. The tools are not what is being sold. What is being sold is the method that makes the tools' output acceptable to an auditor, plus the skills that implement it. Offer to transfer it: the goal is that their team runs the method after handover.

---

## 5. Traps to avoid in the room

- Listing agent names, skill names, MCP names. It reduces you to a tool vendor and invites a comparison you cannot win.
- Calling anything an "AI agent that does analysis." Talk about the artefact produced and how it is verified.
- Accepting a scope or a date before discovery.
- Claiming AI does not make mistakes. Claim instead that mistakes cannot pass the gate.
- Presenting slides when you could present findings from their own codebase.

---

## 6. The move that beats any deck

Sign the NDA, take one small but real module, and run discovery on it before the meeting. Then put on screen:

> "In this module we extracted 47 business rules. Six contradict your documentation. Three could not be explained by anyone we asked."

That last list is the whole argument. It is the evidence for why the program failed the first time, the proof that the method works, and the justification for the discovery phase — in one slide.
