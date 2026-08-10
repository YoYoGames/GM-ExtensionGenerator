#!/usr/bin/env bash
# ##### extgen :: generated core (scripts/extgen) — customize scripts/build_macos.sh #####
# Universal macOS dylib (arm64 + x86_64). Usage: build_macos.sh [release|debug] [--skip-deploy]
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$ROOT/rust"

export CARGO_TARGET_DIR="$ROOT/rust/target"
export MACOSX_DEPLOYMENT_TARGET="${MACOSX_DEPLOYMENT_TARGET:-11.0}"

CRATE="${EXTGEN_CRATE_NAME}"
EXT="${EXTGEN_EXTENSION_NAME}"
DEST_REL="${EXTGEN_MACOS_OUTPUT_FOLDER}"

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

if [[ "$(uname)" != "Darwin" ]]; then
  echo "ERROR: macOS build requires macOS." >&2
  exit 1
fi

rustup target add aarch64-apple-darwin x86_64-apple-darwin >/dev/null 2>&1 || true

build_one() {
  local triple="$1"
  if [[ "$PROFILE" == "release" ]]; then
    cargo build --release --target "$triple"
  else
    cargo build --target "$triple"
  fi
}

echo "--- Building aarch64-apple-darwin ($PROFILE) ---"
build_one aarch64-apple-darwin
echo "--- Building x86_64-apple-darwin ($PROFILE) ---"
build_one x86_64-apple-darwin

FAT_DIR="${CARGO_TARGET_DIR}/macos-${PROFILE}"
mkdir -p "$FAT_DIR"
FAT_DYLIB="$FAT_DIR/lib${EXT}.dylib"

lipo -create \
  "${CARGO_TARGET_DIR}/aarch64-apple-darwin/${PROFILE}/lib${CRATE}.dylib" \
  "${CARGO_TARGET_DIR}/x86_64-apple-darwin/${PROFILE}/lib${CRATE}.dylib" \
  -output "$FAT_DYLIB"

echo "Fat macOS dylib: $FAT_DYLIB"

if [[ "$SKIP_DEPLOY" -eq 1 ]]; then
  exit 0
fi

case "$DEST_REL" in
  /*|[A-Za-z]:*) DEST="$DEST_REL" ;;
  *) DEST="$ROOT/$DEST_REL" ;;
esac
mkdir -p "$DEST"
cp -f "$FAT_DYLIB" "$DEST/lib${EXT}.dylib"
echo "Deployed macOS -> $DEST/lib${EXT}.dylib"
