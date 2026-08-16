# specanchor

**Spec-anchored legacy modernization for regulated industries — with parity guarantees
and an auditable trail.** On .NET and SQL Server today.

specanchor is a toolchain plus a method. It extracts business rules from a legacy
codebase with resolvable source references, proves them against the running legacy
system with characterization tests, anchors them to specifications, and enforces the
result with deterministic CI gates. It sits beside the build and **never modifies
application code** — removing it means deleting two folders, and the application
still runs.

> The claim is not "AI does not make mistakes."
> The claim is "unvalidated output cannot pass the gate."

## The three team rules

1. No rule exists without a source reference. No `source_ref` → rejected, not flagged.
2. A specification with an open question does not enter a sprint.
3. Gates are never closed silently: every bypass is recorded, owned, and expires.

## Architecture in one paragraph

Everything that produces evidence is **deterministic** — the index (Roslyn + ScriptDom,
symbol-level, no model, no embeddings, no vector search), the characterization test
runner, the parity harness, and the gates (headless in CI). Everything that interprets
is an **agent** — self-validating skills that consume the index through MCP, scoped to
one bounded context at a time, and whose output always arrives as a merge request.
The LLM never sees the raw repository and never re-derives what the engine can answer
deterministically.

## Repository layout

```
core/
  schemas/        the six artefact schemas (rule, ledger-term, char-test, spec,
                  comparison-policy, data-access-coverage) — the project constitution
  index/csharp/   Roslyn provider          (build order #2)
  index/sql/      ScriptDom provider       (build order #3)
  skills/         self-validating skills   (build order #4-5)
  parity/         harness and scrubbers    (build order #6)
  gates/          analyzers and CI steps   (build order #7)
  cli/            specanchor CLI + MCP     (build order #8+)
rig/
  legacy-factoring/  the fake legacy system with four planted traps — the acceptance
                     test of the whole chain and the eval corpus for every skill.
                     See rig/legacy-factoring/TRAPS.md (the answer key).
docs/               the method: toolset spec, concept reference, playbooks,
                    team operating agreement
```

## Status

The PoC chain's deterministic spine is in and proven against the rig: the six
schemas, both index providers, the read/write matrix with its coverage artefact,
all four discovery skill contracts with the shared self-validation engine, and
the parity harness (Trap A — the 1-kuruş rounding divergence — is detected,
classified as rounding, and accepted only through a signed known-difference
entry, all by test). Next: the gates and the CLI/MCP surface, then the agent-run
of the skills with the rig's answer key as the eval set. Nothing here has
touched client code yet, by design.

## Relationship to Goldpath and to Spec Kit

- The method is carrier-independent: specanchor works against any .NET codebase,
  including a client's in-house framework. [Goldpath](https://github.com/qorpe) is the
  default *target* when the modern side is ours to choose — specanchor binds to it via
  profile data, never the other way around.
- GitHub Spec Kit is the optional *surface* (specify/plan/tasks/implement). Everything
  in this repository works with zero Spec Kit installed.

## License

Apache-2.0 — see [LICENSE](LICENSE).
