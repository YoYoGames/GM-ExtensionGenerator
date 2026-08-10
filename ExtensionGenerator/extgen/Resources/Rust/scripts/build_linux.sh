#!/usr/bin/env bash
# ##### extgen :: generated core (scripts/extgen) — customize scripts/build_linux.sh #####
# Linux x86_64 shared library. Usage: build_linux.sh [release|debug] [--skip-deploy]
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$ROOT/rust"

export CARGO_TARGET_DIR="$ROOT/rust/target"

CRATE="${EXTGEN_CRATE_NAME}"
EXT="${EXTGEN_EXTENSION_NAME}"
DEST_REL="${EXTGEN_LINUX_OUTPUT_FOLDER}"
TRIPLE="x86_64-unknown-linux-gnu"

PROFILE="release"
SKIP_DEPLOY=0
for arg in "$@"; do
  case "$arg" in
    release|Release|RELEASE) PROFILE="release" ;;
    debug|Debug|DEBUG) PROFILE="debug" ;;
    --skip-deploy) SKIP_DEPLOY=1 ;;
    --help|-h)
      echo "Usage: $0 [release|debug] [--skip-deploy]"
      exit 0
      ;;
    *)
      echo "Unknown option: $arg" >&2
      echo "Usage: $0 [release|debug] [--skip-deploy]" >&2
      exit 1
      ;;
  esac
done

if [[ "$(uname)" != "Linux" ]]; then
  echo "ERROR: Linux build requires Linux (or a Linux cross-toolchain)." >&2
  exit 1
fi

rustup target add "$TRIPLE" >/dev/null 2>&1 || true

CARGO_ARGS=(build --target "$TRIPLE")
if [[ "$PROFILE" == "release" ]]; then
  CARGO_ARGS+=(--release)
fi

cargo "${CARGO_ARGS[@]}"

SO="${CARGO_TARGET_DIR}/${TRIPLE}/${PROFILE}/lib${CRATE}.so"
if [[ ! -f "$SO" ]]; then
  echo "Missing $SO" >&2
  exit 1
fi

if [[ "$SKIP_DEPLOY" -eq 1 ]]; then
  echo "Built $SO (no deploy)"
  exit 0
fi

case "$DEST_REL" in
  /*|[A-Za-z]:*) DEST="$DEST_REL" ;;
  *) DEST="$ROOT/$DEST_REL" ;;
esac
mkdir -p "$DEST"
cp -f "$SO" "$DEST/lib${EXT}.so"
echo "Deployed Linux -> $DEST/lib${EXT}.so"
