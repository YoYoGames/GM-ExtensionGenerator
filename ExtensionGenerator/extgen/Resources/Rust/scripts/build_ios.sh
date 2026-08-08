#!/usr/bin/env bash
# ##### extgen :: generated core (scripts/extgen) — customize scripts/build_ios.sh #####
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$ROOT/rust"

CRATE="${EXTGEN_CRATE_NAME}"
EXT="${EXTGEN_EXTENSION_NAME}"
PREFIX="__EXT_NATIVE__"

cargo build --release --target aarch64-apple-ios
cargo build --release --target aarch64-apple-ios-sim
cargo build --release --target x86_64-apple-ios

OUT="$ROOT/build/ios"
mkdir -p "$OUT"
LIB_DEVICE="target/aarch64-apple-ios/release/lib${CRATE}.a"
LIB_SIM_ARM="target/aarch64-apple-ios-sim/release/lib${CRATE}.a"
LIB_SIM_X64="target/x86_64-apple-ios/release/lib${CRATE}.a"

# Optional Variant B isolation: keep only FFI globals public.
# Uncomment and require llvm-objcopy when shipping multiple self-contained Rust extensions:
# isolate() {
#   local in="$1" out="$2"
#   local tmp
#   tmp="$(mktemp -d)"
#   cd "$tmp"
#   ar x "$in"
#   ld -r -o combined.o *.o
#   llvm-objcopy --wildcard --keep-global-symbol="${PREFIX}*" --localize-symbol='*' combined.o
#   ar rcs "$out" combined.o
#   cd - >/dev/null
#   rm -rf "$tmp"
# }

lipo -create "$LIB_SIM_ARM" "$LIB_SIM_X64" -output "$OUT/lib${CRATE}-sim.a" || cp "$LIB_SIM_ARM" "$OUT/lib${CRATE}-sim.a"

rm -rf "$OUT/${EXT}.xcframework"
xcodebuild -create-xcframework \
  -library "$LIB_DEVICE" \
  -library "$OUT/lib${CRATE}-sim.a" \
  -output "$OUT/${EXT}.xcframework"

echo "Built $OUT/${EXT}.xcframework"
echo "Note: for multiple Rust staticlibs in one app, run symbol isolation (Variant B) or use a rust_bridge crate."
