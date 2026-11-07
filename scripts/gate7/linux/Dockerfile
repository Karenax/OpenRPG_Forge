# Official Rust image, Linux/amd64 manifest resolved from Docker Hub on 2026-10-23.
FROM rust:1.98.0-bookworm@sha256:4e4a7e7939c17991ab35f2b8c2e67593980f771d28f6b1254b1850f860fd0c7f

RUN apt-get update && DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends \
    python3 nodejs pkg-config clang lld \
    libwebkit2gtk-4.1-dev libxdo-dev libssl-dev libayatana-appindicator3-dev librsvg2-dev patchelf \
    libasound2-dev libudev-dev libx11-dev libxi-dev libxcursor-dev libxrandr-dev libxinerama-dev \
    libwayland-dev libxkbcommon-dev libxkbcommon-x11-0 libvulkan1 mesa-vulkan-drivers xvfb xauth \
    && rm -rf /var/lib/apt/lists/* \
    && rustup component add clippy rustfmt rust-src --toolchain 1.98.0 \
    && rustup target add wasm32-unknown-unknown --toolchain 1.98.0 \
    && useradd --create-home --uid 1000 forge \
    && mkdir -p /qualification /incoming \
    && chown forge:forge /qualification /incoming

ENV CARGO_HOME=/qualification/cargo-home \
    CARGO_TARGET_DIR=/qualification/cargo-target \
    CARGO_INCREMENTAL=0 CARGO_BUILD_JOBS=2 TMPDIR=/qualification/tmp \
    PATH=/usr/local/cargo/bin:/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin
COPY run_qualification.py /usr/local/lib/forge-gate7/run_qualification.py
USER forge
ENTRYPOINT ["python3", "/usr/local/lib/forge-gate7/run_qualification.py"]
