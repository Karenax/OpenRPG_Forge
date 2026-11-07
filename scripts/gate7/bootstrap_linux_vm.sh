#!/usr/bin/env bash
set -euo pipefail
# Only the newly provisioned local guest, never the Windows development host.
test "$(hostname)" = forge-gate7-linux
test "$(id -un)" = forge
printf 'Gate 7 Linux bootstrap: %s\n' "$(date --utc --iso-8601=seconds)"
uname -a
cat /etc/os-release
sudo apt-get update
sudo env DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends \
  build-essential pkg-config curl ca-certificates unzip git \
  libwebkit2gtk-4.1-dev libxdo-dev libssl-dev libayatana-appindicator3-dev librsvg2-dev patchelf \
  libasound2-dev libudev-dev libx11-dev libxi-dev libxcursor-dev libxrandr-dev libxinerama-dev \
  libwayland-dev libxkbcommon-dev libxkbcommon-x11-0 libvulkan1 mesa-vulkan-drivers xvfb xauth
mkdir -p /home/forge/qualification-build/tmp /home/forge/qualification-build/tools
export TMPDIR=/home/forge/qualification-build/tmp
export CARGO_HOME=/home/forge/qualification-build/cargo-home
export RUSTUP_HOME=/home/forge/qualification-build/rustup
export CARGO_TARGET_DIR=/home/forge/qualification-build/cargo-target
export CARGO_INCREMENTAL=0
export CARGO_BUILD_JOBS=2
export PATH="$CARGO_HOME/bin:$PATH"
cd /home/forge/qualification-build/tools
if test ! -x "$CARGO_HOME/bin/rustup"; then
  curl --fail --location --output rustup-init https://static.rust-lang.org/rustup/dist/x86_64-unknown-linux-gnu/rustup-init
  curl --fail --location --output rustup-init.sha256 https://static.rust-lang.org/rustup/dist/x86_64-unknown-linux-gnu/rustup-init.sha256
  sha256sum --check rustup-init.sha256
  chmod u+x rustup-init
  ./rustup-init -y --no-modify-path --profile minimal --default-toolchain 1.98.0
fi
rustup component add clippy rustfmt rust-src --toolchain 1.98.0
rustup target add wasm32-unknown-unknown --toolchain 1.98.0
rustc +1.98.0 --version --verbose
cargo +1.98.0 --version
df -h /
