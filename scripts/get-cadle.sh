#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
repo_root="$(cd -- "$script_dir/.." && pwd -P)"
lock_path="$repo_root/upstream/cadle.lock.json"
verify_only=0

if [[ "${1:-}" == "--verify-only" ]]; then
  verify_only=1
elif [[ $# -gt 0 ]]; then
  echo "usage: $0 [--verify-only]" >&2
  exit 64
fi

if [[ ! -f "$lock_path" ]]; then
  echo "missing Cadle lockfile: $lock_path" >&2
  exit 1
fi

read_lock() {
  node -e "const fs=require('fs'); const x=JSON.parse(fs.readFileSync(process.argv[1],'utf8')); const keys=process.argv[2].split('.'); let v=x; for(const k of keys){v=v[k];} process.stdout.write(String(v));" "$lock_path" "$1"
}

schema_version="$(read_lock schemaVersion)"
expected_url="$(read_lock repository.url)"
expected_commit="$(read_lock repository.commit)"
expected_tree="$(read_lock repository.tree)"
checkout_relative="$(read_lock checkout.path)"

if [[ "$schema_version" != "1" ]]; then
  echo "unsupported Cadle lock schemaVersion: $schema_version" >&2
  exit 1
fi
if [[ ! "$expected_commit" =~ ^[0-9a-f]{40}$ ]]; then
  echo "locked Cadle commit must be a full lowercase SHA-1" >&2
  exit 1
fi
if [[ ! "$expected_tree" =~ ^[0-9a-f]{40}$ ]]; then
  echo "locked Cadle tree must be a full lowercase SHA-1" >&2
  exit 1
fi
if [[ "$checkout_relative" != "upstream/cadle" ]]; then
  echo "unsupported Cadle checkout path: $checkout_relative" >&2
  exit 1
fi

target="$repo_root/$checkout_relative"
case "$target" in
  "$repo_root"/*) ;;
  *) echo "Cadle checkout escapes the Forge repository: $target" >&2; exit 1 ;;
esac

if [[ ! -e "$target" ]]; then
  if [[ "$verify_only" -eq 1 ]]; then
    echo "Cadle checkout is absent: $target" >&2
    exit 1
  fi
  mkdir -p -- "$target"
  git -C "$target" init --quiet
  git -C "$target" remote add origin "$expected_url"
fi

if [[ ! -d "$target/.git" ]]; then
  echo "Cadle target exists but is not a standalone Git checkout: $target" >&2
  exit 1
fi

actual_url="$(git -C "$target" remote get-url origin)"
if [[ "$actual_url" != "$expected_url" ]]; then
  echo "Cadle remote substitution detected. Expected '$expected_url', found '$actual_url'." >&2
  exit 1
fi

dirty_before="$(git -C "$target" status --porcelain)"
if [[ -n "$dirty_before" ]]; then
  echo "Cadle checkout is dirty; refusing to overwrite it:" >&2
  printf '%s\n' "$dirty_before" >&2
  exit 1
fi

if [[ "$verify_only" -eq 0 ]]; then
  git -C "$target" fetch --no-tags --depth=1 origin "$expected_commit"
  fetched_commit="$(git -C "$target" rev-parse FETCH_HEAD)"
  if [[ "$fetched_commit" != "$expected_commit" ]]; then
    echo "fetched Cadle commit '$fetched_commit' does not match lock '$expected_commit'" >&2
    exit 1
  fi
  current_commit="$(git -C "$target" rev-parse HEAD 2>/dev/null || true)"
  if [[ "$current_commit" != "$expected_commit" ]]; then
    git -C "$target" switch --detach "$expected_commit"
  fi
fi

head_commit="$(git -C "$target" rev-parse HEAD)"
if [[ "$head_commit" != "$expected_commit" ]]; then
  echo "Cadle HEAD '$head_commit' does not match lock '$expected_commit'" >&2
  exit 1
fi
if git -C "$target" symbolic-ref --quiet HEAD >/dev/null 2>&1; then
  echo "Cadle must use a detached HEAD" >&2
  exit 1
fi
dirty_after="$(git -C "$target" status --porcelain)"
if [[ -n "$dirty_after" ]]; then
  echo "Cadle checkout changed during acquisition:" >&2
  printf '%s\n' "$dirty_after" >&2
  exit 1
fi

tree="$(git -C "$target" rev-parse 'HEAD^{tree}')"
if [[ "$tree" != "$expected_tree" ]]; then
  echo "Cadle tree '$tree' does not match lock '$expected_tree'" >&2
  exit 1
fi
printf 'CADLE_ACQUISITION_PASS commit=%s tree=%s path=%s\n' "$head_commit" "$tree" "$target"
