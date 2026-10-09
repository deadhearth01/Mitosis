#!/bin/bash
# Builds dist/Mitosis.app: the app, the `mitosis` CLI and the launch stub, ad-hoc signed.
# Usage: scripts/build-app.sh [--debug]
set -euo pipefail
cd "$(dirname "$0")/.."

CONFIG="release"
if [ "${1:-}" = "--debug" ]; then CONFIG="debug"; fi

for product in MitosisApp mitosis LaunchStub MitosisRouter; do
  swift build -c "${CONFIG}" --product "${product}"
done
BIN="$(swift build -c "${CONFIG}" --show-bin-path)"
VERSION="$(sed -n 's/.*public static let version = "\(.*\)".*/\1/p' Sources/MitosisCore/Mitosis.swift)"
BUILD_NUMBER="$(git rev-list --count HEAD 2>/dev/null || echo 1)"

APP="dist/Mitosis.app"
rm -rf "${APP}"
mkdir -p "${APP}/Contents/MacOS" "${APP}/Contents/Helpers" "${APP}/Contents/Resources"

cp "${BIN}/MitosisApp" "${APP}/Contents/MacOS/Mitosis"
cp "${BIN}/mitosis" "${BIN}/LaunchStub" "${BIN}/MitosisRouter" "${APP}/Contents/Helpers/"
cp -R "${BIN}/MitosisCore_MitosisCore.bundle" "${BIN}/MitosisCore_MitosisUI.bundle" "${APP}/Contents/Resources/"

# App icon. Preferred: the Icon Composer source compiled by actool (Xcode 26+), which macOS 26+ shows full size.
# Fallback for older Xcode: a classic .icns from the approved 1024 px master.
WORK="$(pwd)/.build/app-icon"
rm -rf "${WORK}" && mkdir -p "${WORK}/AppIcon.iconset"
if xcrun actool Assets/Brand/AppIcon.icon --compile "${APP}/Contents/Resources" --platform macosx \
     --minimum-deployment-target 15.0 --app-icon AppIcon --output-partial-info-plist "${WORK}/partial.plist" >"${WORK}/actool.log" 2>&1 \
   && [ -f "${APP}/Contents/Resources/Assets.car" ]; then
  echo "App icon: compiled with actool"
else
  echo "App icon: actool unavailable, using classic icns"
  MASTER="Assets/Brand/mitosis-mascot-1024.png"
  for size in 16 32 128 256 512; do
    sips -z "${size}" "${size}" "${MASTER}" --out "${WORK}/AppIcon.iconset/icon_${size}x${size}.png" >/dev/null
    double=$((size * 2))
    sips -z "${double}" "${double}" "${MASTER}" --out "${WORK}/AppIcon.iconset/icon_${size}x${size}@2x.png" >/dev/null
  done
  iconutil -c icns "${WORK}/AppIcon.iconset" -o "${APP}/Contents/Resources/AppIcon.icns"
fi

cat > "${APP}/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleIdentifier</key><string>com.mitosis-mac.Mitosis</string>
  <key>CFBundleName</key><string>Mitosis</string>
  <key>CFBundleDisplayName</key><string>Mitosis</string>
  <key>CFBundleExecutable</key><string>Mitosis</string>
  <key>CFBundleIconFile</key><string>AppIcon</string>
  <key>CFBundleIconName</key><string>AppIcon</string>
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>CFBundleInfoDictionaryVersion</key><string>6.0</string>
  <key>CFBundleShortVersionString</key><string>${VERSION}</string>
  <key>CFBundleVersion</key><string>${BUILD_NUMBER}</string>
  <key>LSMinimumSystemVersion</key><string>15.0</string>
  <key>LSApplicationCategoryType</key><string>public.app-category.utilities</string>
  <key>NSHighResolutionCapable</key><true/>
  <key>NSHumanReadableCopyright</key><string>Copyright 2026 The Avni Studio. Free and source-available.</string>
  <key>CFBundleDocumentTypes</key>
  <array>
    <dict>
      <key>CFBundleTypeName</key><string>Application</string>
      <key>CFBundleTypeRole</key><string>Viewer</string>
      <key>LSHandlerRank</key><string>None</string>
      <key>LSItemContentTypes</key><array><string>com.apple.application-bundle</string></array>
    </dict>
  </array>
</dict>
</plist>
PLIST
plutil -lint "${APP}/Contents/Info.plist" >/dev/null

# Ad-hoc signatures, inside out (no paid Developer ID; see README "Is it safe?").
codesign --force --sign - "${APP}/Contents/Helpers/mitosis"
codesign --force --sign - "${APP}/Contents/Helpers/LaunchStub"
codesign --force --sign - "${APP}/Contents/Helpers/MitosisRouter"
codesign --force --sign - "${APP}"
codesign --verify --strict "${APP}"

echo "Built ${APP} (${VERSION}, build ${BUILD_NUMBER}, $(du -sh "${APP}" | cut -f1))"
