# EspTools

Bundled esptool binaries for the "Export to ESP32-S3" UI button. Released artifacts get this folder populated by the CI workflow (`.github/workflows/dotnet-build.yml`) which downloads the right esptool per target runtime from https://github.com/espressif/esptool/releases.

For local development you can drop one in manually:

| File | Source | Used on |
|------|--------|---------|
| `esptool.exe` | esptool-vX.X-windows-amd64.zip | Windows |
| `esptool` | esptool-vX.X-macos-arm64.tar.gz | macOS arm64 |
| `esptool` | esptool-vX.X-macos-amd64.tar.gz | macOS Intel |
| `esptool` | esptool-vX.X-linux-amd64.tar.gz | Linux x86_64 |
| `esptool` | esptool-vX.X-linux-aarch64.tar.gz | Linux ARM |

Binaries themselves aren't committed (.gitignore drops them). If this folder is empty the app still builds — the ESP32-S3 button just stays disabled with a "esptool not bundled" status.

Tested against esptool v5.x. esptool licence is GPLv2-or-later, compatible with TomodachiDrawer's GPLv3.
