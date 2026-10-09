#!/bin/bash
# Mitosis installer - installs the `mitosis` command-line tool for the current user (no admin rights needed).
#
#   curl -fsSL https://raw.githubusercontent.com/deadhearth01/Mitosis/main/scripts/install.sh | bash
#
# Options (environment variables):
#   MITOSIS_VERSION=v0.1.0-alpha.1   install a specific release instead of the newest one
#   MITOSIS_PREFIX=~/.local          install location (the tool goes in $MITOSIS_PREFIX/bin)
set -euo pipefail

REPO="deadhearth01/Mitosis"
ASSET="mitosis-macos-arm64.tar.gz"
PREFIX="${MITOSIS_PREFIX:-$HOME/.local}"
BIN="${PREFIX}/bin"
HOME_DIR="${PREFIX}/share/mitosis"

say()  { printf '%s\n' "$*"; }
fail() { printf 'Error: %s\n' "$*" >&2; exit 1; }

[ "$(uname -s)" = "Darwin" ] || fail "Mitosis runs on macOS only."
[ "$(uname -m)" = "arm64" ] || fail "This release supports Apple silicon Macs only."
major="$(sw_vers -productVersion | cut -d. -f1)"
[ "${major}" -ge 15 ] || fail "Mitosis needs macOS 15 or later (this Mac has $(sw_vers -productVersion))."

TAG="${MITOSIS_VERSION:-}"
if [ -z "${TAG}" ]; then
  TAG="$(curl -fsSL "https://api.github.com/repos/${REPO}/releases?per_page=1" \
        | grep -m1 '"tag_name"' | sed -E 's/.*"tag_name": *"([^"]+)".*/\1/' || true)"
  [ -n "${TAG}" ] || fail "Couldn't find the newest Mitosis release. Check your internet connection and try again."
fi
URL="https://github.com/${REPO}/releases/download/${TAG}"

TMP="$(mktemp -d)"
trap 'rm -rf "${TMP}"' EXIT

say "Downloading Mitosis ${TAG}..."
curl -fsSL "${URL}/${ASSET}" -o "${TMP}/${ASSET}" || fail "Download failed (${URL}/${ASSET})."
curl -fsSL "${URL}/${ASSET}.sha256" -o "${TMP}/${ASSET}.sha256" || fail "Couldn't download the checksum file."

expected="$(awk '{print $1}' "${TMP}/${ASSET}.sha256")"
actual="$(shasum -a 256 "${TMP}/${ASSET}" | awk '{print $1}')"
[ -n "${expected}" ] && [ "${expected}" = "${actual}" ] || fail "Checksum mismatch, so nothing was installed. Please try again."

mkdir -p "${TMP}/unpacked"
tar -xzf "${TMP}/${ASSET}" -C "${TMP}/unpacked"
[ -x "${TMP}/unpacked/mitosis/mitosis" ] || fail "The download doesn't look like a Mitosis release."
"${TMP}/unpacked/mitosis/mitosis" --version >/dev/null 2>&1 || fail "The downloaded tool doesn't run on this Mac."

# Replace any previous install in one step, then add a tiny launcher to the bin folder.
mkdir -p "${BIN}" "$(dirname "${HOME_DIR}")"
rm -rf "${HOME_DIR}.new"
mv "${TMP}/unpacked/mitosis" "${HOME_DIR}.new"
rm -rf "${HOME_DIR}"
mv "${HOME_DIR}.new" "${HOME_DIR}"
printf '#!/bin/sh\nexec "%s/mitosis" "$@"\n' "${HOME_DIR}" > "${BIN}/mitosis"
chmod +x "${BIN}/mitosis"

say "Installed $("${BIN}/mitosis" --version 2>/dev/null || echo "Mitosis") to ${BIN}/mitosis"
case ":$PATH:" in
  *":${BIN}:"*) ;;
  *) say ""
     say "Add ${BIN} to your PATH so you can type 'mitosis':"
     say "  echo 'export PATH=\"${BIN}:\$PATH\"' >> ~/.zshrc && source ~/.zshrc" ;;
esac
say ""
say "Next: mitosis doctor Slack      (see how an app will be cloned)"
say "      mitosis clone Slack --label Work"
say "To uninstall: rm -rf \"${HOME_DIR}\" \"${BIN}/mitosis\""
