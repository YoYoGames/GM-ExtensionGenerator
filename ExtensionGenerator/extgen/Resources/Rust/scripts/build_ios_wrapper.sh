#!/usr/bin/env bash
# ##### extgen :: user entrypoint (IfMissing — customize freely) #####
# Regenerated core lives in scripts/extgen/ — this wrapper is yours.
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
export CARGO_TARGET_DIR="${SCRIPT_DIR}/../rust/target"
exec "$SCRIPT_DIR/extgen/build_ios.sh" "$@"
