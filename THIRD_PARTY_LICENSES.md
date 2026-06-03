# Third-Party Licenses

This document lists third-party components bundled with TomodachiDrawer
release artifacts and their licensing terms.

## esptool

- **Name:** esptool
- **Version:** v5.2.0 — must match `ESPTOOL_VERSION` in
  [.github/workflows/dotnet-build.yml](.github/workflows/dotnet-build.yml).
  Bump both together when updating.
- **Upstream:** https://github.com/espressif/esptool
- **License:** GNU General Public License v2.0 or later (GPLv2-or-later)
- **Bundled with the release as:**
  - The platform-native `esptool` binary in `EspTools/`
  - A verbatim copy of the upstream LICENSE at `EspTools/LICENSE.esptool`
  - The corresponding source tarball at
    `EspTools/esptool-v5.2.0-source.tar.gz`

### Invocation model

esptool runs as a separate operating-system process. TomodachiDrawer
shells out to the bundled binary via
`System.Diagnostics.Process.Start` with `UseShellExecute=false`; no
esptool code is imported, linked, or loaded into the TomodachiDrawer
process. Under GPLv3 §5 paragraph 2 this is "mere aggregation" of
independent works on a distribution medium, not the creation of a
combined work.

esptool's GPLv2-or-later grant explicitly permits redistribution
under GPLv3 terms, so the aggregate's licensing is internally
consistent.

### Corresponding source

The corresponding source code for esptool is shipped in every release
archive at `EspTools/esptool-v5.2.0-source.tar.gz`, satisfying GPLv2
§3(a) / GPLv3 §6(a) directly. No separate written offer is required
while we ship the source alongside the binary.

<!-- TODO: If we ever stop shipping the corresponding source tarball
in the release and switch to offering it on request instead, replace
the paragraph above with a written-offer paragraph satisfying
GPLv2 §3(b) / GPLv3 §6(b). The offer must be valid for at least three
years (GPLv2) or as long as we provide spare parts / customer support
(GPLv3), and must name a delivery medium and an address to contact. -->
