#!/usr/bin/env bash
# ##### extgen :: generated core (scripts/extgen) — customize scripts/build_linux.sh #####
# Linux shared library (x86_64 / aarch64). Usage: build_linux.sh [release|debug] [x86_64|aarch64|arm64] [--skip-deploy]
# Cross-compile (e.g. x86_64 WSL → aarch64) needs the matching GNU cross toolchain.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$ROOT/rust"

export CARGO_TARGET_DIR="$ROOT/rust/target"

CRATE="${EXTGEN_CRATE_NAME}"
EXT="${EXTGEN_EXTENSION_NAME}"
DEST_REL="${EXTGEN_LINUX_OUTPUT_FOLDER}"

host_arch="$(uname -m)"
case "$host_arch" in
  x86_64|amd64) HOST_ARCH="x86_64" ;;
  aarch64|arm64) HOST_ARCH="aarch64" ;;
  *)
    echo "ERROR: unsupported host arch '$host_arch' (expected x86_64 or aarch64)." >&2
    exit 1
    ;;
esac
ARCH="$HOST_ARCH"

PROFILE="release"
SKIP_DEPLOY=0
for arg in "$@"; do
  case "$arg" in
    release|Release|RELEASE) PROFILE="release" ;;
    debug|Debug|DEBUG) PROFILE="debug" ;;
    x86_64|amd64) ARCH="x86_64" ;;
    aarch64|arm64) ARCH="aarch64" ;;
    --skip-deploy) SKIP_DEPLOY=1 ;;
    --help|-h)
      echo "Usage: $0 [release|debug] [x86_64|aarch64|arm64] [--skip-deploy]"
      exit 0
      ;;
    *)
      echo "Unknown option: $arg" >&2
      echo "Usage: $0 [release|debug] [x86_64|aarch64|arm64] [--skip-deploy]" >&2
      exit 1
      ;;
  esac
done

case "$ARCH" in
  x86_64) TRIPLE="x86_64-unknown-linux-gnu" ;;
  aarch64) TRIPLE="aarch64-unknown-linux-gnu" ;;
esac

if [[ "$(uname)" != "Linux" ]]; then
  echo "ERROR: Linux build requires Linux (or a Linux cross-toolchain)." >&2
  exit 1
fi

# Cross-compile: host linker (x86_64 rust-lld/cc) cannot link aarch64 objects.
if [[ "$ARCH" != "$HOST_ARCH" ]]; then
  case "$TRIPLE" in
    aarch64-unknown-linux-gnu) CROSS_CC_CANDIDATES=(aarch64-linux-gnu-gcc aarch64-linux-gnu-cc) ;;
    x86_64-unknown-linux-gnu) CROSS_CC_CANDIDATES=(x86_64-linux-gnu-gcc x86_64-linux-gnu-cc) ;;
  esac
  CROSS_CC=""
  for cand in "${CROSS_CC_CANDIDATES[@]}"; do
    if command -v "$cand" >/dev/null 2>&1; then
      CROSS_CC="$cand"
      break
    fi
  done
  if [[ -z "$CROSS_CC" ]]; then
    echo "ERROR: cross-compile $HOST_ARCH → $ARCH needs a GNU cross linker." >&2
    if [[ "$ARCH" == "aarch64" ]]; then
      echo "  Debian/Ubuntu/WSL:  sudo apt install gcc-aarch64-linux-gnu" >&2
    else
      echo "  Debian/Ubuntu/WSL:  sudo apt install gcc-x86-64-linux-gnu" >&2
    fi
    exit 1
  fi
  # CARGO_TARGET_<triple>_LINKER (triple uppercased, - → _)
  linker_env="CARGO_TARGET_$(echo "$TRIPLE" | tr 'a-z-' 'A-Z_')_LINKER"
  export "$linker_env=$CROSS_CC"
  echo "Cross-compile: using linker $CROSS_CC ($linker_env)"
fi

rustup target add "$TRIPLE" >/dev/null 2>&1 || true

CARGO_ARGS=(build --target "$TRIPLE")
if [[ "$PROFILE" == "release" ]]; then
  CARGO_ARGS+=(--release)
fi

echo "--- Building $TRIPLE ($PROFILE) ---"
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
echo "Deployed Linux ($ARCH) -> $DEST/lib${EXT}.so"
