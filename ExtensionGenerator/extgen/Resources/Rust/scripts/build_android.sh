#!/usr/bin/env bash
# ##### extgen :: generated core (scripts/extgen) — customize scripts/build_android.sh #####
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$ROOT/rust"

CRATE="${EXTGEN_CRATE_NAME}"
EXT="${EXTGEN_EXTENSION_NAME}"

if ! command -v cargo-ndk >/dev/null 2>&1; then
  echo "cargo-ndk is required (cargo install cargo-ndk)" >&2
  exit 1
fi

cargo ndk -t arm64-v8a -t armeabi-v7a -t x86_64 build --release

DEST_BASE="$ROOT/source/${EXT}_gml/extensions/${EXT}/AndroidSource/libs"

copy_abi() {
  local abi="$1"
  local triple="$2"
  local so="target/${triple}/release/lib${CRATE}.so"
  mkdir -p "$DEST_BASE/$abi"
  if [[ -f "$so" ]]; then
    cp -f "$so" "$DEST_BASE/$abi/lib${EXT}.so"
    echo "Deployed $abi"
  else
    echo "Missing $so" >&2
    exit 1
  fi
}

copy_abi arm64-v8a aarch64-linux-android
copy_abi armeabi-v7a armv7-linux-androideabi
copy_abi x86_64 x86_64-linux-android
