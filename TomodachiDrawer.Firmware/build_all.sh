#!/usr/bin/env bash
# Build TomodachiDrawer.Firmware for both RP2040 and RP2350 Zero boards.
# Run from the TomodachiDrawer.Firmware directory (or pass --clean to start fresh).

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
CLEAN=false
[[ "${1:-}" == "--clean" ]] && CLEAN=true

build_target() {
    local board="$1"
    local build_dir="$SCRIPT_DIR/$2"

    echo " Building for: ${board}"
    echo " Output dir:   ${build_dir}"

    if $CLEAN && [ -d "$build_dir" ]; then
        echo " Cleaning previous build..."
        rm -rf "$build_dir"
    fi

    mkdir -p "$build_dir"
    pushd "$build_dir"

    cmake -DPICO_BOARD="$board" ..
    make -j"$(nproc 2>/dev/null || echo 4)"

    popd

    echo ""
    echo " Built: ${build_dir}/TomodachiDrawer.Firmware.uf2"
    echo ""
}

build_target "waveshare_rp2040_zero" "build-rp2040"
build_target "waveshare_rp2350_zero" "build-rp2350"

echo " All builds complete!"
echo "   RP2040: build-rp2040/TomodachiDrawer.Firmware.uf2"
echo "   RP2350: build-rp2350/TomodachiDrawer.Firmware.uf2"

cp ./build-rp2040/TomodachiDrawer.Firmware.uf2 ../TomodachiDrawer.UI.Avalonia/TomodachiDrawer.Firmware.rp2040.uf2
cp ./build-rp2350/TomodachiDrawer.Firmware.uf2 ../TomodachiDrawer.UI.Avalonia/TomodachiDrawer.Firmware.rp2350.uf2

echo " Copied firmwares to UI project folder"
