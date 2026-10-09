#!/bin/bash
# Mitosis installer: installs Mitosis.app and the `mitosis` command for the current user.
#
#   curl -fsSL https://raw.githubusercontent.com/deadhearth01/Mitosis/main/scripts/install.sh | bash
#
# Downloads the newest release from GitHub, checks its SHA-256 checksum and signature, installs the app to
# /Applications (or ~/Applications if /Applications isn't writable), links the `mitosis` command into
# ~/.local/bin, and opens the app. No admin password needed.
#
# Options (environment variables):
#   MITOSIS_VERSION=v0.1.0     install a specific release instead of the newest one
#   MITOSIS_APP_DIR=<folder>   where to put Mitosis.app
#   MITOSIS_BIN=<folder>       where to link the `mitosis` command (default ~/.local/bin)
#   MITOSIS_NO_OPEN=1          don't open Mitosis afterwards
#   MITOSIS_ZIP=<file>         install from a local Mitosis-<version>.zip (testing; needs MITOSIS_ZIP.sha256 next to it)
set -euo pipefail

REPO="deadhearth01/Mitosis"
BUNDLE_ID="com.mitosis-mac.Mitosis"
BIN="${MITOSIS_BIN:-$HOME/.local/bin}"

say()  { printf '%s\n' "$*"; }
fail() { printf 'Error: %s\n' "$*" >&2; exit 1; }

[ "$(uname -s)" = "Darwin" ] || fail "Mitosis runs on macOS only."
[ "$(uname -m)" = "arm64" ] || fail "This release supports Apple silicon Macs only."
major="$(sw_vers -productVersion | cut -d. -f1)"
[ "${major}" -ge 15 ] || fail "Mitosis needs macOS 15 or later (this Mac has $(sw_vers -productVersion))."

TMP="$(mktemp -d)"
trap 'rm -rf "${TMP}"' EXIT

if [ -n "${MITOSIS_ZIP:-}" ]; then
  ZIP_PATH="${MITOSIS_ZIP}"
  [ -f "${ZIP_PATH}" ] && [ -f "${ZIP_PATH}.sha256" ] || fail "MITOSIS_ZIP needs the zip and its .sha256 file."
  cp "${ZIP_PATH}.sha256" "${TMP}/checksum"
  ASSET="$(basename "${ZIP_PATH}")"
  say "Installing from ${ZIP_PATH}..."
else
  TAG="${MITOSIS_VERSION:-}"
  if [ -z "${TAG}" ]; then
    TAG="$(curl -fsSL "https://api.github.com/repos/${REPO}/releases/latest" \
          | grep -m1 '"tag_name"' | sed -E 's/.*"tag_name": *"([^"]+)".*/\1/' || true)"
    [ -n "${TAG}" ] || fail "Couldn't find the newest Mitosis release. Check your internet connection and try again."
  fi
  VERSION="${TAG#v}"
  ASSET="Mitosis-${VERSION}.zip"
  URL="https://github.com/${REPO}/releases/download/${TAG}"
  ZIP_PATH="${TMP}/${ASSET}"
  say "Downloading Mitosis ${VERSION}..."
  curl -fsSL "${URL}/${ASSET}" -o "${ZIP_PATH}" || fail "Download failed (${URL}/${ASSET})."
  curl -fsSL "${URL}/${ASSET}.sha256" -o "${TMP}/checksum" || fail "Couldn't download the checksum file."
fi

expected="$(awk '{print $1}' "${TMP}/checksum")"
actual="$(shasum -a 256 "${ZIP_PATH}" | awk '{print $1}')"
[ -n "${expected}" ] && [ "${expected}" = "${actual}" ] || fail "Checksum mismatch for ${ASSET}. Nothing was installed."
say "Checksum OK."

ditto -x -k "${ZIP_PATH}" "${TMP}/unpacked" || fail "Couldn't unpack ${ASSET}."
NEW_APP="${TMP}/unpacked/Mitosis.app"
[ -d "${NEW_APP}" ] || fail "The download doesn't contain Mitosis.app."
codesign --verify --deep --strict "${NEW_APP}" 2>/dev/null || fail "Mitosis.app's signature didn't verify. Nothing was installed."
"${NEW_APP}/Contents/Helpers/mitosis" --version >/dev/null 2>&1 || fail "The mitosis command in this download doesn't run."

if [ -n "${MITOSIS_APP_DIR:-}" ]; then
  APP_DIR="${MITOSIS_APP_DIR}"
elif [ -w "/Applications" ]; then
  APP_DIR="/Applications"
else
  APP_DIR="${HOME}/Applications"
fi
mkdir -p "${APP_DIR}"
TARGET="${APP_DIR}/Mitosis.app"

# Quit a running Mitosis first so the update replaces it cleanly.
if [ -z "${MITOSIS_APP_DIR:-}" ] && /usr/bin/pgrep -f "Mitosis.app/Contents/MacOS/Mitosis" >/dev/null 2>&1; then
  say "Quitting Mitosis to update it..."
  /usr/bin/osascript -e "tell application id \"${BUNDLE_ID}\" to quit" >/dev/null 2>&1 || true
  for _ in 1 2 3 4 5 6 7 8 9 10; do
    /usr/bin/pgrep -f "Mitosis.app/Contents/MacOS/Mitosis" >/dev/null 2>&1 || break
    sleep 1
  done
fi

# Swap in the new app; the old copy is only removed once the new one is in place.
if [ -d "${TARGET}" ]; then
  rm -rf "${TARGET}.previous"
  mv "${TARGET}" "${TARGET}.previous"
fi
if ! mv "${NEW_APP}" "${TARGET}"; then
  [ -d "${TARGET}.previous" ] && mv "${TARGET}.previous" "${TARGET}"
  fail "Couldn't put Mitosis.app in ${APP_DIR}."
fi
rm -rf "${TARGET}.previous"
xattr -dr com.apple.quarantine "${TARGET}" 2>/dev/null || true
/System/Library/Frameworks/CoreServices.framework/Frameworks/LaunchServices.framework/Support/lsregister -f "${TARGET}" >/dev/null 2>&1 || true
say "Installed Mitosis.app in ${APP_DIR}."

# The `mitosis` command: a link into the app. Replace only links and the 0.1.0-alpha installer's wrapper.
mkdir -p "${BIN}"
LINK="${BIN}/mitosis"
if [ -L "${LINK}" ] || [ ! -e "${LINK}" ] || grep -q "/share/mitosis/mitosis" "${LINK}" 2>/dev/null; then
  ln -sfn "${TARGET}/Contents/Helpers/mitosis" "${LINK}"
  say "Linked the mitosis command at ${LINK}."
  OLD_HOME="${HOME}/.local/share/mitosis"
  if [ "${BIN}" = "${HOME}/.local/bin" ] && [ -f "${OLD_HOME}/mitosis" ]; then
    rm -rf "${OLD_HOME}"   # files from the 0.1.0-alpha command-line installer
  fi
else
  say "Left ${LINK} alone (it isn't from Mitosis). The command is at ${TARGET}/Contents/Helpers/mitosis."
fi
case ":${PATH}:" in
  *":${BIN}:"*) ;;
  *) say ""
     say "To use the mitosis command, add ${BIN} to your PATH, for example:"
     say "  echo 'export PATH=\"${BIN}:\$PATH\"' >> ~/.zshrc && source ~/.zshrc" ;;
esac

say ""
say "Done. Find Mitosis in ${APP_DIR}, or run: mitosis --help"
say "To uninstall: quit Mitosis, then move ${TARGET} to the Trash and run: rm -f ${LINK}"
if [ -z "${MITOSIS_NO_OPEN:-}" ]; then
  open "${TARGET}" || true
fi
