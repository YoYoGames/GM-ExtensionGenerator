#!/usr/bin/env bash
# ##### extgen :: generated core (scripts/extgen) — customize scripts/build_tvos.sh #####
# Dynamic XCFramework: cdylib → {EXT}_Rust.framework → zip → targets.tvos.outputFolder
# Usage: build_tvos.sh [--device-only|--sim-only] [--skip-zip]
# YY: tvosThirdPartyFrameworkEntries → {EXT}_Rust.xcframework embed:1
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$ROOT/rust"

export CARGO_TARGET_DIR="$ROOT/rust/target"

CRATE="${EXTGEN_CRATE_NAME}"
EXT="${EXTGEN_EXTENSION_NAME}"
DEST_REL="${EXTGEN_TVOS_OUTPUT_FOLDER}"
FRAMEWORK_NAME="${EXTGEN_IOS_FRAMEWORK_NAME}"
BUNDLE_ID="${EXTGEN_IOS_BUNDLE_ID}"
MIN_TVOS="${EXTGEN_TVOS_MIN_VERSION}"
INSTALL_NAME="@executable_path/Frameworks/${FRAMEWORK_NAME}.framework/${FRAMEWORK_NAME}"
DYLIB_BASENAME="lib${CRATE}.dylib"
ZIP_NAME="${FRAMEWORK_NAME}.zip"

BUILD_DEVICE=1
BUILD_SIM=1
SKIP_ZIP=0

while [[ $# -gt 0 ]]; do
  case "$1" in
    --device-only) BUILD_SIM=0; shift ;;
    --sim-only) BUILD_DEVICE=0; shift ;;
    --skip-zip) SKIP_ZIP=1; shift ;;
    --help|-h)
      echo "Usage: $0 [--device-only|--sim-only] [--skip-zip]"
      exit 0
      ;;
    *)
      echo "Unknown option: $1" >&2
      exit 1
      ;;
  esac
done

export TVOS_DEPLOYMENT_TARGET="$MIN_TVOS"
export CARGO_PROFILE_RELEASE_DEBUG=1
export CARGO_PROFILE_RELEASE_STRIP=none

if [[ "$(uname)" != "Darwin" ]]; then
  echo "ERROR: tvOS build requires macOS with Xcode." >&2
  exit 1
fi
if ! xcode-select -p &>/dev/null; then
  echo "ERROR: Xcode CLT not found. Run: xcode-select --install" >&2
  exit 1
fi

# tvOS has no reliable prebuilt std on stable (rustup target add fails).
# Build std from source via nightly -Zbuild-std.
if ! rustup toolchain list 2>/dev/null | grep -q 'nightly'; then
  echo "ERROR: nightly toolchain required for tvOS (-Zbuild-std). Install with:" >&2
  echo "  rustup toolchain install nightly --component rust-src" >&2
  exit 1
fi
if ! rustup component list --toolchain nightly 2>/dev/null | grep -q 'rust-src.*(installed)'; then
  echo "ERROR: rust-src missing on nightly. Install with:" >&2
  echo "  rustup component add rust-src --toolchain nightly" >&2
  exit 1
fi

# Overrides Cargo.toml panic=unwind; required with -Zbuild-std=std,panic_abort.
export CARGO_PROFILE_RELEASE_PANIC=abort
CARGO_TVOS=(cargo +nightly build -Zbuild-std=std,panic_abort)

write_framework_plist() {
  local plist_path="$1"
  local platform="$2" # AppleTVOS | AppleTVSimulator
  cat > "$plist_path" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
	<key>CFBundleDevelopmentRegion</key>
	<string>en</string>
	<key>CFBundleExecutable</key>
	<string>${FRAMEWORK_NAME}</string>
	<key>CFBundleIdentifier</key>
	<string>${BUNDLE_ID}</string>
	<key>CFBundleInfoDictionaryVersion</key>
	<string>6.0</string>
	<key>CFBundleName</key>
	<string>${FRAMEWORK_NAME}</string>
	<key>CFBundlePackageType</key>
	<string>FMWK</string>
	<key>CFBundleShortVersionString</key>
	<string>1.0</string>
	<key>CFBundleVersion</key>
	<string>1</string>
	<key>MinimumOSVersion</key>
	<string>${MIN_TVOS}</string>
	<key>CFBundleSupportedPlatforms</key>
	<array>
		<string>${platform}</string>
	</array>
</dict>
</plist>
EOF
}

LAST_FRAMEWORK_PATH=""
LAST_DSYM_PATH=""

make_framework_from_dylib() {
  local dylib_path="$1"
  local out_dir="$2"
  local platform="$3"

  if [[ ! -f "$dylib_path" ]]; then
    echo "ERROR: dylib not found: $dylib_path" >&2
    echo "Ensure Cargo.toml has crate-type including \"cdylib\"." >&2
    exit 1
  fi

  local fw="$out_dir/${FRAMEWORK_NAME}.framework"
  rm -rf "$fw" "$out_dir/${FRAMEWORK_NAME}.framework.dSYM"
  mkdir -p "$fw"
  cp "$dylib_path" "$fw/${FRAMEWORK_NAME}"
  chmod +w "$fw/${FRAMEWORK_NAME}"
  install_name_tool -id "$INSTALL_NAME" "$fw/${FRAMEWORK_NAME}"

  local id_out
  id_out="$(otool -D "$fw/${FRAMEWORK_NAME}" 2>/dev/null | tail -n 1 || true)"
  if [[ "$id_out" == *@rpath* ]]; then
    echo "ERROR: install name still uses @rpath: $id_out" >&2
    echo "Expected: $INSTALL_NAME" >&2
    exit 1
  fi

  write_framework_plist "$fw/Info.plist" "$platform"

  local dsym="$out_dir/${FRAMEWORK_NAME}.framework.dSYM"
  if dsymutil "$fw/${FRAMEWORK_NAME}" -o "$dsym" 2>/dev/null; then
    :
  else
    echo "WARNING: dsymutil failed for $fw"
    dsym=""
  fi
  codesign --force --sign - --timestamp=none "$fw" >/dev/null 2>&1 || true

  LAST_FRAMEWORK_PATH="$fw"
  LAST_DSYM_PATH="$dsym"
}

DEVICE_DYLIB=""
SIM_DYLIB=""

if [[ "$BUILD_DEVICE" -eq 1 ]]; then
  echo "--- Building cdylib aarch64-apple-tvos ---"
  "${CARGO_TVOS[@]}" --release --target aarch64-apple-tvos
  DEVICE_DYLIB="${CARGO_TARGET_DIR}/aarch64-apple-tvos/release/${DYLIB_BASENAME}"
fi

if [[ "$BUILD_SIM" -eq 1 ]]; then
  echo "--- Building cdylib aarch64-apple-tvos-sim ---"
  "${CARGO_TVOS[@]}" --release --target aarch64-apple-tvos-sim
  SIM_ARM="${CARGO_TARGET_DIR}/aarch64-apple-tvos-sim/release/${DYLIB_BASENAME}"

  mkdir -p "${CARGO_TARGET_DIR}/tvos-sim-release"
  SIM_DYLIB="${CARGO_TARGET_DIR}/tvos-sim-release/${DYLIB_BASENAME}"

  echo "--- Building cdylib x86_64-apple-tvos ---"
  if "${CARGO_TVOS[@]}" --release --target x86_64-apple-tvos; then
    lipo -create \
      "$SIM_ARM" \
      "${CARGO_TARGET_DIR}/x86_64-apple-tvos/release/${DYLIB_BASENAME}" \
      -output "$SIM_DYLIB"
  else
    echo "WARNING: x86_64-apple-tvos build failed; using arm64 sim only."
    cp -f "$SIM_ARM" "$SIM_DYLIB"
  fi
fi

FRAMEWORK_TMPDIR="$(mktemp -d)"
trap 'rm -rf "$FRAMEWORK_TMPDIR"' EXIT
DEVICE_FW_DIR="$FRAMEWORK_TMPDIR/device"
SIM_FW_DIR="$FRAMEWORK_TMPDIR/sim"
mkdir -p "$DEVICE_FW_DIR" "$SIM_FW_DIR"

XCFRAMEWORK_ARGS=()
if [[ "$BUILD_DEVICE" -eq 1 ]]; then
  make_framework_from_dylib "$DEVICE_DYLIB" "$DEVICE_FW_DIR" "AppleTVOS"
  XCFRAMEWORK_ARGS+=(-framework "$LAST_FRAMEWORK_PATH")
  if [[ -n "${LAST_DSYM_PATH}" && -d "${LAST_DSYM_PATH}" ]]; then
    XCFRAMEWORK_ARGS+=(-debug-symbols "$LAST_DSYM_PATH")
  fi
fi
if [[ "$BUILD_SIM" -eq 1 ]]; then
  make_framework_from_dylib "$SIM_DYLIB" "$SIM_FW_DIR" "AppleTVSimulator"
  XCFRAMEWORK_ARGS+=(-framework "$LAST_FRAMEWORK_PATH")
  if [[ -n "${LAST_DSYM_PATH}" && -d "${LAST_DSYM_PATH}" ]]; then
    XCFRAMEWORK_ARGS+=(-debug-symbols "$LAST_DSYM_PATH")
  fi
fi

xcodebuild -create-xcframework \
  "${XCFRAMEWORK_ARGS[@]}" \
  -output "$FRAMEWORK_TMPDIR/${FRAMEWORK_NAME}.xcframework"

if find "$FRAMEWORK_TMPDIR/${FRAMEWORK_NAME}.xcframework" -name '*.a' | grep -q .; then
  echo "WARNING: XCFramework contains .a — expected dynamic frameworks only."
fi

case "$DEST_REL" in
  /*|[A-Za-z]:*) DEST="$DEST_REL" ;;
  *) DEST="$ROOT/$DEST_REL" ;;
esac

if [[ "$SKIP_ZIP" -eq 1 ]]; then
  echo "Skipping zip. XCFramework: $FRAMEWORK_TMPDIR/${FRAMEWORK_NAME}.xcframework"
  trap - EXIT
else
  mkdir -p "$DEST"
  rm -f "$DEST/$ZIP_NAME"
  (cd "$FRAMEWORK_TMPDIR" && zip -r -q "$DEST/$ZIP_NAME" "${FRAMEWORK_NAME}.xcframework")
  echo "Deployed tvOS -> $DEST/$ZIP_NAME"
fi
