# Mitosis Phase 0 — Identity-Mode Spike Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Answer spike questions S1–S7 from the spec (does an "identity mode" clone — APFS copy + new bundle ID + local ad-hoc re-sign — behave like a separate app?) and record decisions in the spec.

**Architecture:** Throwaway shell/Swift/C tooling in a scratch folder outside the repo. A tiny test app (NotifyProbe) proves the launch/notification/icon mechanics without logins; then real apps (Claude, VS Code, Signal, WhatsApp) prove data separation, Electron helpers, updater behavior, and sandboxed-app behavior. Nothing here becomes product code.

**Tech Stack:** bash, `codesign`, `PlistBuddy`, `cp -c` (APFS clone), `lsregister`, `lsof`, C (stub), Swift scripts (AppKit/ImageIO, UserNotifications), python3 `plistlib`.

**Spec:** `docs/specs/2026-10-09-mitosis-design.md` (section 12 lists S1–S8; S8 moves to the distribution plan).

## Global Constraints

- Original apps in `/Applications` are **never modified**. All copies live under `$SPIKE`.
- Before any real app's clone is launched for the first time, that app's data folder is backed up with an APFS clone (instant, no disk cost).
- A clone of a real app is never launched before its data separation is configured.
- Spike code is throwaway: it stays outside the repo and is deleted in Task 6.
- No credentials are typed by the agent. Login/QR/permission prompts are done by the human partner.
- macOS 27.2, Xcode 27, Apple silicon (this machine).

## Review Focus

- Clone silently shares the original's data folder → must be detected by `lsof` within seconds of launch, before the user does anything (Task 3 step 4).
- Clone's built-in updater replaces the clone with an official build (bundle ID reverts) → check bundle ID and cdhash after 10 minutes (Task 3 step 8).
- Notification click from Notification Center (app in background), not only the live banner → test both (Task 2 step 6).
- App refuses to start after re-sign with no visible error → capture exit status/crash report path (Task 4 step 3).
- Leftover clone registrations/containers after cleanup → verify with `lsregister -dump` and `ls ~/Library/Containers` (Task 6 step 2).

---

## Setup (once per shell)

```bash
export SPIKE="/private/tmp/claude-501/-Volumes-SSD-1TB-DEV-mac-apps/ec3dee8b-9bdc-41af-9796-e2796acc878c/scratchpad/spike"
export LSREG=/System/Library/Frameworks/CoreServices.framework/Frameworks/LaunchServices.framework/Support/lsregister
mkdir -p "$SPIKE"/{tools,clones,data,work,backup}
```
(If running in a different session, set `SPIKE` to `<that session's scratchpad>/spike`.)

---

### Task 1: Spike tooling (stub, clone script, entitlement sanitizer, badge renderer)

**Files (all throwaway, under `$SPIKE/tools`):**
- Create: `stub.c`, `make-clone.sh`, `sanitize-ents.py`, `badge.swift`

- [ ] **Step 1: Write the launch stub**

`$SPIKE/tools/stub.c`:
```c
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <unistd.h>
#include <libgen.h>
#include <limits.h>
#include <mach-o/dyld.h>

int main(int argc, char **argv) {
    char self[PATH_MAX]; uint32_t size = sizeof(self);
    if (_NSGetExecutablePath(self, &size) != 0) return 126;
    char real[PATH_MAX]; snprintf(real, sizeof real, "%s.mitosis-real", self);
    char buf[PATH_MAX]; strncpy(buf, self, sizeof buf - 1); buf[sizeof buf - 1] = 0;
    char conf[PATH_MAX]; snprintf(conf, sizeof conf, "%s/../Resources/mitosis-launch.conf", dirname(buf));
    char *nargv[512]; int n = 0; nargv[n++] = real;
    FILE *f = fopen(conf, "r");
    if (f) {
        char line[4096];
        while (fgets(line, sizeof line, f) && n < 400) {
            line[strcspn(line, "\n")] = 0;
            if (!strncmp(line, "arg=", 4)) nargv[n++] = strdup(line + 4);
            else if (!strncmp(line, "env=", 4)) {
                char *kv = line + 4, *eq = strchr(kv, '=');
                if (eq) { *eq = 0; setenv(kv, eq + 1, 1); }
            }
        }
        fclose(f);
    }
    for (int i = 1; i < argc && n < 510; i++)
        if (strncmp(argv[i], "-psn_", 5) != 0) nargv[n++] = argv[i];
    nargv[n] = NULL;
    execv(real, nargv);
    perror("mitosis-stub execv");
    return 127;
}
```

- [ ] **Step 2: Compile the stub**

Run: `xcrun clang -O2 -o "$SPIKE/tools/stub" "$SPIKE/tools/stub.c" && file "$SPIKE/tools/stub"`
Expected: `Mach-O 64-bit executable arm64`

- [ ] **Step 3: Write the entitlement sanitizer**

`$SPIKE/tools/sanitize-ents.py`:
```python
#!/usr/bin/env python3
"""Usage: sanitize-ents.py <in.plist> <out.plist>  — drops team-bound/restricted entitlements."""
import plistlib, sys
RESTRICTED_KEYS = {"keychain-access-groups", "com.apple.application-identifier", "application-identifier",
                   "aps-environment", "com.apple.security.application-groups", "com.apple.team-identifier"}
src, dst = sys.argv[1], sys.argv[2]
try:
    with open(src, "rb") as f:
        data = f.read()
    ents = plistlib.loads(data) if data.strip() else {}
except Exception:
    ents = {}
kept = {k: v for k, v in ents.items() if not (k.startswith("com.apple.developer.") or k in RESTRICTED_KEYS)}
dropped = sorted(set(ents) - set(kept))
with open(dst, "wb") as f:
    plistlib.dump(kept, f)
print("dropped:", ", ".join(dropped) if dropped else "(none)")
```

- [ ] **Step 4: Write the badge renderer**

`$SPIKE/tools/badge.swift`:
```swift
// Usage: swift badge.swift <app path> <out.icns> <letter> <hex RRGGBB>
import AppKit
import ImageIO
import UniformTypeIdentifiers

let args = CommandLine.arguments
let appPath = args[1], outPath = args[2], letter = args[3], hex = args[4]
let base = NSWorkspace.shared.icon(forFile: appPath)
func color(_ hex: String) -> CGColor {
    var v: UInt64 = 0; Scanner(string: hex).scanHexInt64(&v)
    return CGColor(red: CGFloat((v >> 16) & 0xff) / 255, green: CGFloat((v >> 8) & 0xff) / 255, blue: CGFloat(v & 0xff) / 255, alpha: 1)
}
func render(_ size: Int) -> CGImage {
    let s = CGFloat(size)
    let ctx = CGContext(data: nil, width: size, height: size, bitsPerComponent: 8, bytesPerRow: 0,
                        space: CGColorSpace(name: CGColorSpace.sRGB)!, bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)!
    var r = CGRect(x: 0, y: 0, width: s, height: s)
    ctx.draw(base.cgImage(forProposedRect: &r, context: nil, hints: nil)!, in: r)
    let d = s * 0.42, badge = CGRect(x: s - d - s * 0.04, y: s * 0.04, width: d, height: d)
    ctx.setFillColor(.white); ctx.fillEllipse(in: badge.insetBy(dx: -s * 0.02, dy: -s * 0.02))
    ctx.setFillColor(color(hex)); ctx.fillEllipse(in: badge)
    let font = CTFontCreateUIFontForLanguage(.emphasizedSystem, d * 0.6, nil)!
    let line = CTLineCreateWithAttributedString(NSAttributedString(string: letter,
        attributes: [NSAttributedString.Key(kCTFontAttributeName as String): font,
                     NSAttributedString.Key(kCTForegroundColorAttributeName as String): CGColor.white]))
    let b = CTLineGetBoundsWithOptions(line, .useGlyphPathBounds)
    ctx.textPosition = CGPoint(x: badge.midX - b.width / 2 - b.minX, y: badge.midY - b.height / 2 - b.minY)
    CTLineDraw(line, ctx)
    return ctx.makeImage()!
}
let sizes = [16, 32, 128, 256, 512, 1024]
let dest = CGImageDestinationCreateWithURL(URL(fileURLWithPath: outPath) as CFURL, UTType.icns.identifier as CFString, sizes.count, nil)!
for s in sizes { CGImageDestinationAddImage(dest, render(s), nil) }
print(CGImageDestinationFinalize(dest) ? "wrote \(outPath)" : "FAILED")
```

- [ ] **Step 5: Write the clone script**

`$SPIKE/tools/make-clone.sh`:
```bash
#!/bin/bash
# Usage: make-clone.sh <source.app> "<Clone Name>" <slug> <none|args|home> [letter] [hex]
set -euo pipefail
SRC="$1"; NAME="$2"; SLUG="$3"; DMODE="$4"; LETTER="${5:-W}"; HEX="${6:-0A84FF}"
: "${SPIKE:?set SPIKE}"; T="$SPIKE/tools"; PB=/usr/libexec/PlistBuddy
DEST="$SPIKE/clones/$NAME.app"; DATA="$SPIKE/data/$SLUG"; WORK="$SPIKE/work/$SLUG"
rm -rf "$DEST" "$WORK"; mkdir -p "$SPIKE/clones" "$DATA" "$WORK"

cp -c -R "$SRC" "$DEST"                      # APFS clone: instant, no extra space
chmod -R u+w "$DEST"
PL="$DEST/Contents/Info.plist"
SRCID=$($PB -c 'Print :CFBundleIdentifier' "$PL"); EXE=$($PB -c 'Print :CFBundleExecutable' "$PL")
NEWID="$SRCID.mitosis.$SLUG"
setkey() { $PB -c "Delete :$1" "$PL" 2>/dev/null || true; $PB -c "Add :$1 $2 $3" "$PL"; }
setkey CFBundleIdentifier string "$NEWID"
setkey CFBundleName string "$NAME"
setkey CFBundleDisplayName string "$NAME"
setkey SUEnableAutomaticChecks bool false
setkey SUAutomaticallyUpdate bool false
$PB -c 'Delete :SUFeedURL' "$PL" 2>/dev/null || true

# icon with badge
swift "$T/badge.swift" "$SRC" "$DEST/Contents/Resources/MitosisIcon.icns" "$LETTER" "$HEX"
setkey CFBundleIconFile string MitosisIcon
$PB -c 'Delete :CFBundleIconName' "$PL" 2>/dev/null || true

# launch stub for data separation
EXTRA=()
if [ "$DMODE" != "none" ]; then
  mv "$DEST/Contents/MacOS/$EXE" "$DEST/Contents/MacOS/$EXE.mitosis-real"
  cp "$T/stub" "$DEST/Contents/MacOS/$EXE"
  CONF="$DEST/Contents/Resources/mitosis-launch.conf"
  if [ "$DMODE" = "args" ]; then echo "arg=--user-data-dir=$DATA" > "$CONF"; fi
  if [ "$DMODE" = "home" ]; then mkdir -p "$DATA/home"; echo "env=HOME=$DATA/home" > "$CONF"; fi
  EXTRA=("$DEST/Contents/MacOS/$EXE.mitosis-real")
fi

# entitlements
codesign -d --entitlements - --xml "$SRC" > "$WORK/ents-orig.plist" 2>/dev/null || true
python3 "$T/sanitize-ents.py" "$WORK/ents-orig.plist" "$WORK/ents.plist"

# sign inside-out: every nested Mach-O file and bundle, deepest first
find "$DEST/Contents" \( -type d \( -name '*.framework' -o -name '*.app' -o -name '*.xpc' -o -name '*.appex' -o -name '*.bundle' -o -name '*.plugin' \) \) -o \( -type f \( -perm -u+x -o -name '*.dylib' -o -name '*.node' -o -name '*.so' \) \) \
  | grep -v "^$DEST/Contents/MacOS/$EXE\$" \
  | awk -F/ '{print NF"\t"$0}' | sort -rn | cut -f2- > "$WORK/nested.txt"
while IFS= read -r item; do
  if [ -f "$item" ] && ! file -b "$item" | grep -q 'Mach-O'; then continue; fi
  [ -L "$item" ] && continue
  codesign --force --sign - --timestamp=none "$item" 2>>"$WORK/sign.log" || echo "WARN sign failed: $item" | tee -a "$WORK/sign.log"
done < "$WORK/nested.txt"
for x in "${EXTRA[@]+"${EXTRA[@]}"}"; do
  codesign --force --sign - --timestamp=none --identifier "$NEWID" --entitlements "$WORK/ents.plist" "$x"
done
codesign --force --sign - --timestamp=none --entitlements "$WORK/ents.plist" "$DEST"
codesign --verify --deep --strict "$DEST" && echo "VERIFY OK"
"$LSREG" -f "$DEST"
echo "CLONE $DEST  id=$NEWID  data=$DATA"
```

- [ ] **Step 6: Make executable and dry-run on a harmless app**

Run:
```bash
chmod +x "$SPIKE/tools/make-clone.sh"
"$SPIKE/tools/make-clone.sh" "/System/Applications/Chess.app" "Chess Test" chess-test none C 30D158 || echo "EXPECTED: may fail on Apple system app"
```
Expected: script runs end-to-end through copy + plist edits; signing/launch of an Apple app may fail — that is fine (Apple apps are out of scope). Record whether it failed and at which step in `$SPIKE/results.md`.

No commit (throwaway).

---

### Task 2: NotifyProbe — S1 (any-order launch), S4 (notification routing), S6 (badge icon)

**Files (throwaway):** `$SPIKE/probe/NotifyProbe.swift`, `$SPIKE/probe/NotifyProbe.app`

- [ ] **Step 1: Write the probe app**

`$SPIKE/probe/NotifyProbe.swift`:
```swift
import AppKit
import UserNotifications

let logURL = URL(fileURLWithPath: "/tmp/mitosis-notifyprobe.log")
func append(_ s: String) {
    let line = "\(Date()) \(s)\n"
    if let h = try? FileHandle(forWritingTo: logURL) { h.seekToEndOfFile(); h.write(line.data(using: .utf8)!); try? h.close() }
    else { try? line.write(to: logURL, atomically: true, encoding: .utf8) }
}

final class AppDelegate: NSObject, NSApplicationDelegate, UNUserNotificationCenterDelegate {
    var window: NSWindow?
    let id = Bundle.main.bundleIdentifier ?? "?"
    func applicationDidFinishLaunching(_ n: Notification) {
        append("LAUNCH bundle=\(id) pid=\(getpid())")
        let w = NSWindow(contentRect: NSRect(x: 300, y: 300, width: 520, height: 80), styleMask: [.titled, .closable, .miniaturizable], backing: .buffered, defer: false)
        w.title = "\(id) — pid \(getpid())"; w.makeKeyAndOrderFront(nil); window = w
        NSApp.activate(ignoringOtherApps: true)
        let c = UNUserNotificationCenter.current(); c.delegate = self
        c.requestAuthorization(options: [.alert, .sound]) { granted, err in
            append("AUTH bundle=\(self.id) granted=\(granted) err=\(String(describing: err))")
            let content = UNMutableNotificationContent()
            content.title = "NotifyProbe"; content.body = "from \(self.id) pid \(getpid())"
            c.add(UNNotificationRequest(identifier: UUID().uuidString, content: content,
                                        trigger: UNTimeIntervalNotificationTrigger(timeInterval: 20, repeats: false)))
        }
    }
    func userNotificationCenter(_ c: UNUserNotificationCenter, willPresent n: UNNotification,
                                withCompletionHandler h: @escaping (UNNotificationPresentationOptions) -> Void) { h([.banner, .list]) }
    func userNotificationCenter(_ c: UNUserNotificationCenter, didReceive r: UNNotificationResponse,
                                withCompletionHandler h: @escaping () -> Void) {
        append("CLICK received_by bundle=\(id) pid=\(getpid()) notification_body='\(r.notification.request.content.body)'")
        h()
    }
}
let app = NSApplication.shared
let delegate = AppDelegate()
app.delegate = delegate
app.setActivationPolicy(.regular)
app.run()
```

- [ ] **Step 2: Build and sign the probe as the "original" app**

```bash
P="$SPIKE/probe"; A="$P/NotifyProbe.app"; mkdir -p "$A/Contents/MacOS"
xcrun swiftc -swift-version 5 -O -o "$A/Contents/MacOS/NotifyProbe" "$P/NotifyProbe.swift"
cat > "$A/Contents/Info.plist" <<'EOF'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
<key>CFBundleIdentifier</key><string>com.mitosis-mac.spike.notifyprobe</string>
<key>CFBundleName</key><string>NotifyProbe</string>
<key>CFBundleExecutable</key><string>NotifyProbe</string>
<key>CFBundlePackageType</key><string>APPL</string>
<key>CFBundleShortVersionString</key><string>1.0</string>
<key>CFBundleVersion</key><string>1</string>
<key>LSMinimumSystemVersion</key><string>15.0</string>
<key>NSPrincipalClass</key><string>NSApplication</string>
</dict></plist>
EOF
mkdir -p "$A/Contents/Resources"
codesign --force --sign - "$A" && "$LSREG" -f "$A" && echo BUILT
```
Expected: `BUILT`

- [ ] **Step 3: Clone it (identity mode, no stub)**

Run: `"$SPIKE/tools/make-clone.sh" "$SPIKE/probe/NotifyProbe.app" "NotifyProbe Work" work none W 0A84FF`
Expected: `VERIFY OK` and `CLONE … id=com.mitosis-mac.spike.notifyprobe.mitosis.work`

- [ ] **Step 4: S1 — launch clone FIRST, then original**

```bash
rm -f /tmp/mitosis-notifyprobe.log
open "$SPIKE/clones/NotifyProbe Work.app"; sleep 3
open "$SPIKE/probe/NotifyProbe.app"; sleep 3
grep LAUNCH /tmp/mitosis-notifyprobe.log
```
Expected: two LAUNCH lines with different bundle IDs and different pids. **Human partner:** confirm two windows and two Dock icons; allow notifications when macOS asks (twice).
Then quit both (`osascript -e 'quit app id "com.mitosis-mac.spike.notifyprobe"' -e 'quit app id "com.mitosis-mac.spike.notifyprobe.mitosis.work"'`), and repeat with the original first. Record pass/fail for both orders in `$SPIKE/results.md`.

- [ ] **Step 5: S6 — badge icon**

**Human partner:** look at the Dock and at `$SPIKE/clones` in Finder (`open "$SPIKE/clones"`). Expected: clone shows the probe's generic icon with a blue "W" badge. Record pass/fail. If Finder shows a stale icon, run `"$LSREG" -f "$SPIKE/clones/NotifyProbe Work.app"; killall Dock` and recheck; record whether the refresh was needed.

- [ ] **Step 6: S4 — notification routing (banner and Notification Center)**

With both apps running, notifications fire 20 s after launch.
1. **Live banner:** human partner clicks the banner that says "from …mitosis.work". Then clicks the other app's banner.
2. **From Notification Center:** relaunch both, switch to another app (so both are in background), wait for banners to disappear, open Notification Center, click each entry.
```bash
grep CLICK /tmp/mitosis-notifyprobe.log
```
Expected (pass): every CLICK line's `received_by bundle=` matches the bundle ID inside `notification_body`. Record results for banner and Notification Center separately.

---

### Task 3: Electron apps — S2 (data separation method), S3 (helpers), S7 (built-in updater)

Test order is chosen so precious data is last: **Claude → VS Code → Signal**.

- [ ] **Step 1: Back up original data folders (APFS clone, instant)**

```bash
for d in "Claude" "Code" "Signal"; do
  [ -d "$HOME/Library/Application Support/$d" ] && cp -c -R "$HOME/Library/Application Support/$d" "$SPIKE/backup/$d" && echo "backed up $d"
done
```
Expected: one line per existing folder.

- [ ] **Step 2: Record original apps' signatures (to prove they stay untouched)**

```bash
for a in Claude "Visual Studio Code" Signal; do codesign -dv --verbose=4 "/Applications/$a.app" 2>&1 | grep -E '^CDHash=' | sed "s/^/$a /"; done | tee "$SPIKE/work/orig-cdhash.txt"
```

- [ ] **Step 3: Clone Claude with `args` mode**

Run: `"$SPIKE/tools/make-clone.sh" /Applications/Claude.app "Claude Work" claude-work args W 0A84FF`
Expected: `VERIFY OK`. Note any `WARN sign failed` lines in `$SPIKE/results.md`.

- [ ] **Step 4: Launch and immediately check which data folder it opened (safety check)**

```bash
open "$SPIKE/clones/Claude Work.app"; sleep 6
PID=$(pgrep -f "clones/Claude Work.app/Contents/MacOS/Claude.mitosis-real" | head -1); echo "pid=$PID"
lsof -p "$PID" 2>/dev/null | grep -c "$SPIKE/data/claude-work" ; lsof -p "$PID" 2>/dev/null | grep -c "Application Support/Claude/"
```
Expected (pass): first count > 0, second count = 0.
**If the second count > 0:** quit the clone gracefully at once (`osascript -e 'quit app id "com.anthropic.claudefordesktop.mitosis.claude-work"'` — read the exact ID from the script output), record "args: FAIL (shares data)", then redo Step 3 with `home` instead of `args` and repeat this step checking `$SPIKE/data/claude-work/home`.

- [ ] **Step 5: S2/S3 — visible separation and helpers**

**Human partner:** the clone window should show Claude's **sign-in screen** (not your existing session), render normally, and not crash. Do not sign in unless you want to test further. Also open the original Claude at the same time — both should run.
Run: `pgrep -fl "Claude Work.app" | head -20`
Expected: main process plus "Claude Helper (Renderer)/(GPU)" processes from the clone path. Record S2 method that worked (args or home) and S3 pass/fail.

- [ ] **Step 6: Repeat Steps 3–5 for VS Code**

Run: `"$SPIKE/tools/make-clone.sh" "/Applications/Visual Studio Code.app" "Code Work" code-work args W 0A84FF`, then the Step 4 check with process name `Code Work.app/Contents/MacOS/Electron.mitosis-real` (VS Code's executable is `Electron`; confirm with `PlistBuddy -c 'Print :CFBundleExecutable'`) and data paths `$SPIKE/data/code-work` vs `Application Support/Code/`.
Expected: clone opens with default settings/no recent folders. Record.

- [ ] **Step 7: Repeat Steps 3–5 for Signal using the method proven above**

Run: `"$SPIKE/tools/make-clone.sh" /Applications/Signal.app "Signal Work" signal-work <args|home> W 0A84FF`
Expected: clone shows "Link this device" (fresh), original Signal unaffected. Quit clone after observing. Record.

- [ ] **Step 8: S7 — clone's built-in updater**

Leave "Claude Work" running ≥10 minutes, then:
```bash
/usr/libexec/PlistBuddy -c 'Print :CFBundleIdentifier' "$SPIKE/clones/Claude Work.app/Contents/Info.plist"
codesign --verify --deep --strict "$SPIKE/clones/Claude Work.app" && echo "clone intact"
for a in Claude "Visual Studio Code" Signal; do codesign -dv --verbose=4 "/Applications/$a.app" 2>&1 | grep -E '^CDHash=' | sed "s/^/$a /"; done | diff - "$SPIKE/work/orig-cdhash.txt" && echo "originals untouched"
log show --last 15m --predicate 'process CONTAINS "ShipIt" OR process CONTAINS "Squirrel"' --style compact | tail -20
```
Expected (pass): bundle ID still ends `.mitosis.claude-work`, `clone intact`, `originals untouched` (unless a genuine vendor update happened meanwhile — note it), updater errors (if any) are harmless. Record.

---

### Task 4: Sandboxed Mac App Store app — S5 (WhatsApp)

- [ ] **Step 1: Inspect**

```bash
codesign -d --entitlements - --xml /Applications/WhatsApp.app 2>/dev/null | plutil -p - | head -40
ls /Applications/WhatsApp.app/Contents/_MASReceipt 2>/dev/null && echo "MAS receipt present"
```
Record sandbox, app-group and keychain entitlements.

- [ ] **Step 2: Clone (no stub — sandbox containers are keyed by bundle ID)**

Run: `"$SPIKE/tools/make-clone.sh" /Applications/WhatsApp.app "WhatsApp Work" whatsapp-work none W 30D158`
Expected: `VERIFY OK` (record the `dropped:` entitlements line).

- [ ] **Step 3: Launch and observe**

```bash
open "$SPIKE/clones/WhatsApp Work.app"; sleep 8
pgrep -fl "WhatsApp Work.app" || echo "NOT RUNNING"
ls -d "$HOME/Library/Containers/"*mitosis.whatsapp-work* 2>/dev/null || echo "no new container"
ls -t "$HOME/Library/Logs/DiagnosticReports" | head -3
```
**Human partner:** does the clone show a fresh QR-code login screen? Any "damaged"/"purchased by another account" dialog?
Record: runs? new container? receipt dialog? crash report name (if any). Quit the clone.

---

### Task 5: Write the results into the spec

**Files:**
- Modify: `docs/specs/2026-10-09-mitosis-design.md` (append section 14)

- [ ] **Step 1: Append "14. Spike results" with this exact structure, filled from `$SPIKE/results.md`**

```markdown
## 14. Spike results (YYYY-MM-DD)

| # | Result | Evidence |
|---|---|---|
| S1 any-order launch | PASS/FAIL | … |
| S2 Electron data separation | args / home / FAIL | per app: Claude …, VS Code …, Signal … |
| S3 Electron helpers | PASS/FAIL | … |
| S4 notification routing | banner: PASS/FAIL; Notification Center: PASS/FAIL | … |
| S5 sandboxed MAS app | runs: Y/N; own container: Y/N; receipt issue: Y/N | … |
| S6 badge icon | PASS/FAIL (needed Dock refresh: Y/N) | … |
| S7 clone's updater | harmless / dangerous | … |

**Decisions**
- Default Electron launch settings: `<args: --user-data-dir={dataPath}>` or `<env: HOME={dataPath}/home>`
- Default Chromium launch settings: …
- Mac App Store apps default support level: limited / unsupported
- Icon handling: remove CFBundleIconName = sufficient / needs extra step: …
- Clone updater handling: leave as is / must disable via …
- Plan changes needed for the core-engine plan: … (or "none")
```

- [ ] **Step 2: Commit**

```bash
cd /Volumes/SSD_1TB/DEV/mac-apps/app-cloner
git add docs/specs/2026-10-09-mitosis-design.md
git commit -m "docs: record Phase 0 spike results

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: Clean up everything the spike created

- [ ] **Step 1: Quit clones, unregister, move spike data to Trash**

```bash
for id in $(for p in "$SPIKE"/clones/*.app "$SPIKE/probe/NotifyProbe.app"; do /usr/libexec/PlistBuddy -c 'Print :CFBundleIdentifier' "$p/Contents/Info.plist"; done); do
  osascript -e "quit app id \"$id\"" 2>/dev/null || true
done
sleep 2
for p in "$SPIKE"/clones/*.app "$SPIKE/probe/NotifyProbe.app"; do "$LSREG" -u "$p"; done
for d in "$HOME/Library/Containers/"*.mitosis.* "$HOME/Library/Preferences/"*.mitosis.*.plist "$HOME/Library/Preferences/com.mitosis-mac.spike."*; do
  [ -e "$d" ] && osascript -e "tell application \"Finder\" to delete POSIX file \"$d\"" >/dev/null && echo "trashed $d"
done
```
(Data goes to the Trash, not permanently deleted. The human partner may be asked to allow Terminal to control Finder.)

- [ ] **Step 2: Verify nothing is left registered**

```bash
"$LSREG" -dump | grep -c "\.mitosis\." ; ls "$HOME/Library/Containers" | grep -c mitosis
```
Expected: `0` and `0`.

- [ ] **Step 3: Keep the backups until the human partner confirms the original apps (Claude, VS Code, Signal) still work normally, then remove `$SPIKE`**

Ask: "Please open Claude, VS Code and Signal — all normal?" On yes: `rm -rf "$SPIKE"` (scratch folder only; nothing in it is user data except the backups, which are no longer needed).
