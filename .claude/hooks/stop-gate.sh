#!/bin/sh
# Stop hook, MAINTAINER shape (goldpath delivery-cycle RFC, D4).
#
# The application-shaped gate builds an app and runs specdrift against its manifest. This
# repository has no manifest and is not an app — it is the toolchain. What it has instead
# is a rig with an answer key, and the check that matters most is whether the chain still
# finds the planted traps. That is the test suite, so the suite is the gate.
#
# Stryker (minutes), the licence gate (needs a restore) and the full rig gate run stay in
# CI. A hook slow enough to be resented is a hook that gets deleted, and a deleted gate is
# worse than an absent one because it is evidence the discipline does not work.
INPUT=$(cat)

case "$INPUT" in
  *'"stop_hook_active":true'* | *'"stop_hook_active": true'*) exit 0 ;;
esac

cd "${CLAUDE_PROJECT_DIR:-.}" || exit 0
command -v git >/dev/null 2>&1 || exit 0
git rev-parse --is-inside-work-tree >/dev/null 2>&1 || exit 0

CHANGED=$(git status --porcelain -- '*.cs' '*.csproj' '*.props' '*.json' '*.md' '*.sh' '*.sql' 2>/dev/null)
[ -z "$CHANGED" ] && exit 0

LOG=$(mktemp)

CODE_CHANGED=$(git status --porcelain -- '*.cs' '*.csproj' '*.props' '*.json' '*.sql' 2>/dev/null)
if [ -n "$CODE_CHANGED" ]; then
  if ! dotnet test SpecAnchor.sln --nologo -v quiet >"$LOG" 2>&1; then
    echo "stop-gate: the suite is red — the rig's traps are how this chain proves it works." >&2
    tail -n 40 "$LOG" >&2
    rm -f "$LOG"
    exit 2
  fi
fi

# The contract check named by cycle.md §6 and CLAUDE.md: the documents must still describe
# the repository — including the ledger, which is where owed work is tracked.
if [ -x scripts/docs-freshness.sh ]; then
  if ! ./scripts/docs-freshness.sh >"$LOG" 2>&1; then
    echo "stop-gate: scripts/docs-freshness.sh is red — the docs stopped telling the truth." >&2
    cat "$LOG" >&2
    rm -f "$LOG"
    exit 2
  fi
fi

rm -f "$LOG"
exit 0
