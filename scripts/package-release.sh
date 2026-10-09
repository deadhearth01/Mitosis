#!/bin/bash
# Builds Mitosis.app and packages it for GitHub Releases: dist/Mitosis-<version>.zip and its .sha256.
# Usage: scripts/package-release.sh
set -euo pipefail
cd "$(dirname "$0")/.."

scripts/build-app.sh
VERSION="$(sed -n 's/.*public static let version = "\(.*\)".*/\1/p' Sources/MitosisCore/Mitosis.swift)"
ZIP="Mitosis-${VERSION}.zip"

rm -f "dist/${ZIP}" "dist/${ZIP}.sha256"
# ditto keeps the bundle's symlinks, permissions and signature intact.
ditto -c -k --sequesterRsrc --keepParent dist/Mitosis.app "dist/${ZIP}"
(cd dist && shasum -a 256 "${ZIP}" > "${ZIP}.sha256")

echo "Packaged dist/${ZIP} ($(du -h "dist/${ZIP}" | cut -f1))"
cat "dist/${ZIP}.sha256"
