#!/usr/bin/env bash
# ##### extgen :: generated core (scripts/extgen) — customize scripts/build_android.sh #####
# Usage: build_android.sh [release|debug]   (default: release)
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$ROOT/rust"

export CARGO_TARGET_DIR="$ROOT/rust/target"

CRATE="${EXTGEN_CRATE_NAME}"
EXT="${EXTGEN_EXTENSION_NAME}"
DEST_REL="${EXTGEN_ANDROID_OUTPUT_FOLDER}"
ANDROID_API="${CARGO_NDK_PLATFORM:-21}"

PROFILE="release"
case "${1:-release}" in
  release|Release|RELEASE) PROFILE="release" ;;
  debug|Debug|DEBUG) PROFILE="debug" ;;
  *)
    echo "Usage: $0 [release|debug]" >&2
    exit 1
    ;;
esac

if ! command -v cargo-ndk >/dev/null 2>&1; then
  echo "cargo-ndk is required (cargo install cargo-ndk)" >&2
  exit 1
fi

# ABIs: arm64-v8a, armeabi-v7a, x86_64. API via -P (default 21).
NDK_ARGS=(ndk -t arm64-v8a -t armeabi-v7a -t x86_64 -P "$ANDROID_API" build)
if [[ "$PROFILE" == "release" ]]; then
  cargo "${NDK_ARGS[@]}" --release
else
  cargo "${NDK_ARGS[@]}"
fi

case "$DEST_REL" in
  /*|[A-Za-z]:*) ANDROID_OUT="$DEST_REL" ;;
  *) ANDROID_OUT="$ROOT/$DEST_REL" ;;
esac
DEST_BASE="$ANDROID_OUT/libs"

copy_abi() {
  local abi="$1"
  local triple="$2"
  local so="${CARGO_TARGET_DIR}/${triple}/${PROFILE}/lib${CRATE}.so"
  mkdir -p "$DEST_BASE/$abi"
  if [[ -f "$so" ]]; then
    cp -f "$so" "$DEST_BASE/$abi/lib${EXT}.so"
    echo "Deployed $abi ($PROFILE) -> $DEST_BASE/$abi/lib${EXT}.so"
  else
    echo "Missing $so" >&2
    exit 1
  fi
}

copy_abi arm64-v8a aarch64-linux-android
copy_abi armeabi-v7a armv7-linux-androideabi
copy_abi x86_64 x86_64-linux-android
