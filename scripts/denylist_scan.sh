#!/usr/bin/env bash
# denylist_scan.sh: fail if any tracked file, or any commit in history, matches a deny pattern.
#
# Two pattern sources:
#   generic: .denylist-generic.txt (committed; shapes such as tailnet IPs and token prefixes)
#   private: $AILAB_DENYLIST (newline-separated EREs) if set and non-empty, otherwise the file
#            ${AILAB_DENYLIST_FILE:-$HOME/.config/ailab/denylist.txt}. Never committed.
#
# Fails closed: if no private patterns are available the scan exits non-zero, unless
# AILAB_DENYLIST_OPTIONAL=1 (intended only for CI on pull requests from forks, which cannot
# read repository secrets).
#
# Private pattern text is never printed; matches are reported as location + pattern number.
# Exit codes: 0 clean, 1 match found, 2 configuration error.
set -euo pipefail

repo_root="$(git rev-parse --show-toplevel)"
cd "$repo_root"

generic_file=".denylist-generic.txt"
private_file="${AILAB_DENYLIST_FILE:-$HOME/.config/ailab/denylist.txt}"

# Read non-blank, non-comment lines (leading/trailing whitespace stripped) into a named array.
load_patterns() {
  local __target="$1" line
  while IFS= read -r line || [[ -n "$line" ]]; do
    line="${line#"${line%%[![:space:]]*}"}"
    line="${line%"${line##*[![:space:]]}"}"
    [[ -z "$line" || "$line" == \#* ]] && continue
    eval "$__target+=(\"\$line\")"
  done
}

generic=()
if [[ -f "$generic_file" ]]; then
  load_patterns generic < "$generic_file"
fi

private=()
private_source="none"
if [[ -n "${AILAB_DENYLIST:-}" ]]; then
  load_patterns private <<< "$AILAB_DENYLIST"
  private_source="AILAB_DENYLIST"
elif [[ -f "$private_file" ]]; then
  load_patterns private < "$private_file"
  private_source="private denylist file"
fi

if [[ ${#private[@]} -eq 0 ]]; then
  if [[ "${AILAB_DENYLIST_OPTIONAL:-}" == "1" ]]; then
    echo "denylist: WARNING no private patterns available; AILAB_DENYLIST_OPTIONAL=1 so continuing with generic patterns only." >&2
  else
    echo "denylist: ERROR no private patterns available (set AILAB_DENYLIST or create $private_file)." >&2
    echo "denylist: refusing to report clean without them (fail closed)." >&2
    exit 2
  fi
fi

# Validate every pattern up front so a typo cannot silently disable a check.
check_pattern() {
  local status=0
  printf '' | grep -E -i -q -e "$2" 2>/dev/null || status=$?
  if [[ $status -gt 1 ]]; then
    echo "denylist: ERROR $1 is not a valid extended regex." >&2
    exit 2
  fi
}
for i in "${!generic[@]}"; do check_pattern "generic pattern #$((i + 1))" "${generic[$i]}"; done
for i in "${!private[@]}"; do check_pattern "private pattern #$((i + 1))" "${private[$i]}"; done

workdir="$(mktemp -d)"
trap 'rm -rf "$workdir"' EXIT

# Tracked files, excluding the generic pattern file itself (it necessarily matches its own patterns).
git ls-files -z -- . ":(exclude)$generic_file" > "$workdir/files"

# Full history of every ref as patches, plus a parallel file mapping each patch line to commit:path.
git log -p --all --no-color --no-ext-diff --format='commit %H%n%B' -- . ":(exclude)$generic_file" > "$workdir/history"
awk '
  /^commit [0-9a-f]+$/ { sha = substr($2, 1, 12); path = "(message)"; print sha ":" path; next }
  /^diff --git a\//    { path = substr($0, index($0, " b/") + 3) }
  { print sha ":" path }
' "$workdir/history" > "$workdir/history.loc"

findings=0

# scan <kind> <number> <pattern>: prints locations only, never matched text.
scan() {
  local kind="$1" num="$2" pat="$3" label hits
  if [[ "$kind" == "private" ]]; then
    label="matched private pattern #$num"
  else
    label="matched generic pattern #$num ($pat)"
  fi

  if [[ -s "$workdir/files" ]]; then
    hits="$(xargs -0 grep -E -I -i -n -H -e "$pat" -- < "$workdir/files" 2>/dev/null | cut -d: -f1,2 || true)"
    if [[ -n "$hits" ]]; then
      while IFS= read -r loc; do
        echo "denylist: $loc: $label"
        findings=$((findings + 1))
      done <<< "$hits"
    fi
  fi

  hits="$(grep -E -a -i -n -e "$pat" -- "$workdir/history" 2>/dev/null | cut -d: -f1 || true)"
  if [[ -n "$hits" ]]; then
    while IFS= read -r line_no; do
      echo "denylist: history $(sed -n "${line_no}p" "$workdir/history.loc"): $label"
      findings=$((findings + 1))
    done <<< "$hits"
  fi
}

for i in "${!generic[@]}"; do scan generic "$((i + 1))" "${generic[$i]}"; done
for i in "${!private[@]}"; do scan private "$((i + 1))" "${private[$i]}"; done

if [[ $findings -gt 0 ]]; then
  echo "denylist: FAIL $findings match(es). Remove them from the tree AND rewrite history before pushing." >&2
  exit 1
fi

echo "denylist: clean (${#generic[@]} generic + ${#private[@]} private patterns from $private_source; tracked files + full history)."
