# MitosisCore Engine + `mitosis` CLI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the Mitosis clone engine (identity + fallback modes, registry, refresh/delete) as a tested Swift package, plus the tiny launch stub and the `mitosis` command-line tool.

**Architecture:** One Swift package at `Packages/MitosisCore` with three products: `MitosisCore` (library, no UI), `LaunchStub` (tiny executable copied into clones), `mitosis` (CLI over the library). Every engine unit is a small `Sendable` value type or enum with one job; side effects go through `Shell`, `FileManager`, and an injectable `MitosisEnvironment` so tests run in temp folders. Tests use Swift Testing and build real (tiny) fixture `.app` bundles.

**Tech Stack:** Swift 6.4 (Swift 6 language mode), Swift Testing, Foundation/AppKit/ImageIO/CoreText, `codesign`, `lsregister`, `lsappinfo`, `clonefile(2)`, swift-argument-parser 1.5+.

**Spec:** `docs/specs/2026-10-09-mitosis-design.md` — sections 4, 5, 7, 8, 10, and **14 (Spike results)**. Execute the Phase 0 spike plan first; Task 6 and Task 14 read decisions from section 14.

## Global Constraints

- Minimum OS: macOS 15 (`platforms: [.macOS(.v15)]`).
- License: MIT. Mitosis version string: `0.1.0`. Mitosis bundle ID: `com.mitosis-mac.Mitosis`.
- Clone bundle ID scheme: `<source bundle ID>.mitosis.<slug>`; slug = lowercase ASCII letters/digits/hyphens from the clone name, de-duplicated with `-2`, `-3`.
- Clone location `~/Applications/Mitosis/<Clone Name>.app`; registry `~/Library/Application Support/Mitosis/clones.json`; data `~/Library/Application Support/Mitosis/Data/<clone-uuid>/`; embedded manifest `Contents/Resources/mitosis.json`; launch config `Contents/Resources/mitosis-launch.json`.
- Original apps are never modified. Signing is ad-hoc (`codesign --sign -`), no hardened runtime.
- Deleting never removes data permanently: bundles and data go to the Trash.
- No telemetry; the engine performs no network access.
- MitosisCore contains no SwiftUI/UI code.
- Spec refinements adopted in this plan: (1) the CLI and launch stub live inside the package (`Sources/mitosis`, `Sources/LaunchStub`) rather than top-level `CLI/` and `LaunchStub/` folders, so one `swift build` builds everything; (2) `clones.json` stores `RegistryEntry` (manifest + `bundlePath`) instead of bare manifests; (3) the whole `com.apple.security.application-groups` key is removed during sanitizing (ad-hoc signatures can't validate any group).

## Review Focus

- Clone names containing `/`, `:`, a leading `.`, only spaces, or >60 characters → rejected with a clear message, never a crash or odd path (Task 3, Task 14).
- Choosing an existing Mitosis clone as the source → refused ("already a Mitosis clone") (Task 7, Task 14).
- Unsigned source apps or apps whose signature can't be read → still inspectable (no entitlements, no cdhash) and clonable (Task 4, Task 5).
- App and clone names with spaces and non-ASCII characters (`Ünïcode Äpp`) → work end-to-end (Task 14).
- Refresh or delete while the clone is running → refused with "quit it first" (Task 16).

## Amendments from Phase 0 (spec §14) — apply when executing the named task

These override the task text where they conflict.

- **A1 (Task 6):** `profiles.json`: remove the `telegram` entry; set `whatsapp` to `"support": "unsupported"` with notes `"Needs iCloud and shared app data that only work for the original app. Light mode (coming soon) will support it."`.
- **A2 (Task 7):** add a rule after the profile check and before the restricted-entitlements rule: `if app.isSandboxed && !app.restrictedEntitlements.isEmpty` → `ModeDecision(mode: nil, support: .unsupported, reasons: ["This app relies on iCloud or shared app data that only works for the original app, so it can't be cloned yet. Light mode (coming soon) will support it."])`. Add test `sandboxedAppsWithRestrictedEntitlementsAreUnsupported` (info with `isSandboxed: true`, `restricted: ["com.apple.security.application-groups"]` → support `.unsupported`, mode `nil`). The `ModeDeciderTests.info` helper gains a `sandboxed: Bool = false` parameter.
- **A3 (Task 12):** replace inside-out signing of *all* nested code with **signing only what Mitosis modified**. New API (replaces `signInsideOut`; `nestedCodeItems`/`isMachO` are dropped):
  ```swift
  public static func signModified(bundle: URL, entitlements: [String: Any], identifier: String,
                                  helperBundles: [URL] = [], extraExecutables: [URL] = []) throws
  ```
  Order: each `helperBundles` item (ad-hoc, no entitlements), then each `extraExecutables` item (`--identifier identifier` + entitlements), then `bundle` (`--identifier identifier` + entitlements). Tests: (a) modified Info.plist + `signModified` → `verify` passes and entitlements are sanitized; (b) **unmodified nested framework keeps its exact cdhash** (`AppInspector.cdhash(of:)` on the framework before/after); (c) extra executable gets `Identifier=`. Tasks 14/15 call `signModified`.
- **A4 (new Task 12b, before Task 13): `HelperRenamer`** in `Sources/MitosisCore/HelperRenamer.swift`:
  ```swift
  public enum HelperRenamer {
      /// Renames Contents/Frameworks/"<oldName> Helper*.app" → "<newName> Helper*.app", renaming each helper's
      /// executable and setting its CFBundleExecutable/CFBundleName. Returns the renamed bundle URLs.
      public static func rename(in bundle: URL, from oldName: String, to newName: String) throws -> [URL]
  }
  ```
  Test with fixture `helperApps: ["Fixture Helper", "Fixture Helper (Renderer)"]` → `["Work Helper.app", "Work Helper (Renderer).app"]`, executables `Work Helper`, `Work Helper (Renderer)`, Info.plist keys updated; non-matching names untouched; no helpers → `[]`.
- **A5 (Task 14):** `MitosisEnvironment.standard` without root: `dataRoot = ~/Library/Mitosis/Data` (registry stays in `~/Library/Application Support/Mitosis/clones.json`). Data folder name = first 8 hex chars of the UUID, lowercased (extend to the full UUID only if that folder already exists). Identity build steps become: copy → **helpers** (only if `.electron`: `HelperRenamer.rename(in: temp, from: <clone Info.plist CFBundleName before editing, else executableName>, to: name)`) → stub → icon → plist → manifest → sign (`signModified(helperBundles: renamed, extraExecutables: [real])`). Add assertions to `createsASignedIdentityCloneWithItsOwnID`: data folder name has 8 characters; helper `Fixture Work Helper.app` exists; the fixture framework's cdhash is unchanged.
- **A6 (new Task 16b, after Task 16): `StatsCollector`** in `Sources/MitosisCore/StatsCollector.swift`:
  ```swift
  public struct CloneUsage: Equatable, Sendable { public var processCount: Int; public var cpuPercent: Double; public var memoryBytes: Int64 }
  public struct CloneStats: Equatable, Sendable { public var extraDiskBytes: Int64; public var appBytes: Int64; public var dataBytes: Int64; public var usage: CloneUsage? }
  public enum StatsCollector {
      /// Bytes of files in the clone that are new or differ (size or mtime) from the source — the real extra disk, since the rest is APFS-shared.
      public static func extraDiskBytes(clone: URL, source: URL) -> Int64
      public static func dataBytes(_ paths: [URL]) -> Int64            // allocated size; missing paths count 0
      public static func usage(bundle: URL) -> CloneUsage?             // `ps -Ao pid=,pcpu=,rss=,comm=`, processes whose executable is inside the bundle; nil if none
      public static func stats(for entry: RegistryEntry) -> CloneStats // data paths: dataPath + ~/Library/Containers/<cloneBundleID>
      public static let cacheFolderNames: Set<String> = ["Cache", "Code Cache", "GPUCache", "DawnGraphiteCache", "DawnWebGPUCache", "update-cache", "CachedExtensionVSIXs"]
      /// Deletes only the known cache folders directly inside the data path (regenerable). Returns bytes freed.
      @discardableResult public static func cleanCaches(dataPath: URL) throws -> Int64
  }
  ```
  Tests: identity clone of a fixture → `extraDiskBytes` > 0 and < `appBytes`; `dataBytes` counts a written file; `usage` of a non-running clone is `nil`; `cleanCaches` removes `Cache/` and `update-cache/` but keeps `Local Storage/` and reports freed bytes. Add a launch test in `LaunchTests`: running clone → `usage` has `processCount >= 1` and `memoryBytes > 0`.
- **A7 (Task 18):** CLI additions: `mitosis stats <clone>` (prints `Extra disk: <n> (the rest is shared with <App>)`, `App size: <n>`, `Data: <n>`, and `Running: <k> processes · <cpu>% CPU · <mem>` or `Not running`; sizes via `ByteCountFormatter`), and `mitosis clean <clone>` (prints `Freed <n> of caches from "<name>".`). `clone` gains `--no-verify`; **by default** after creating, it runs `CloneLauncher.openAndCheck(window: .seconds(10))` and, if the clone isn't running, moves it (and its data) to the Trash and fails with `"<name>" didn't start correctly, so it was removed. Try again with --mode fallback.` CLI tests pass `--no-verify`. Add CLI test for `stats` (contains `Extra disk:` and `Not running`) and `clean`.

---

## File Structure

```
Packages/MitosisCore/
├─ Package.swift
├─ Sources/
│  ├─ MitosisCore/
│  │  ├─ Mitosis.swift              # version constant, Date helper
│  │  ├─ Shell.swift                # run external tools
│  │  ├─ Models.swift               # CloneMode, SupportLevel, ModeDecision, Badge, CloneManifest, RegistryEntry
│  │  ├─ Naming.swift               # Slug, CloneName validation
│  │  ├─ Entitlements.swift         # read / classify / sanitize entitlements
│  │  ├─ AppInspector.swift         # AppInfo, AppFramework, inspection
│  │  ├─ Profiles.swift             # LaunchSettings, AppProfile, ProfileStore, DefaultLaunchSettings
│  │  ├─ Resources/profiles.json    # bundled app profiles
│  │  ├─ ModeDecider.swift          # AppInfo + profile → ModeDecision
│  │  ├─ InfoPlistEditor.swift      # rewrite clone Info.plist
│  │  ├─ FileCloner.swift           # APFS clone / copy, sizes
│  │  ├─ LaunchConfig.swift         # resolved stub config written into clones
│  │  ├─ IconRenderer.swift         # badge icon → .icns
│  │  ├─ Signer.swift               # inside-out ad-hoc signing + verify
│  │  ├─ CloneRegistry.swift        # clones.json + rebuild from manifests; Trash
│  │  ├─ MitosisEnvironment.swift   # injectable paths/flags; LaunchServices
│  │  ├─ CloneBuilder.swift         # create clones (identity + fallback), rollback
│  │  ├─ CloneMaintenance.swift     # status, refresh, delete, relink
│  │  └─ Runtime.swift              # RunningMonitor, CloneLauncher, AppResolver
│  ├─ LaunchStub/main.swift
│  └─ mitosis/MitosisCLI.swift
└─ Tests/MitosisCoreTests/
   ├─ Support/TestSupport.swift
   ├─ Support/FixtureFactory.swift
   └─ <Unit>Tests.swift (one per task)
```

All commands below run from `Packages/MitosisCore` unless stated: `cd /Volumes/SSD_1TB/DEV/mac-apps/app-cloner/Packages/MitosisCore`.

---

### Task 1: Package scaffold, version constant, `Shell`

**Files:**
- Create: `Packages/MitosisCore/Package.swift`
- Create: `Sources/MitosisCore/Mitosis.swift`, `Sources/MitosisCore/Shell.swift`
- Create: `Sources/LaunchStub/main.swift` (placeholder body, replaced in Task 10)
- Create: `Sources/mitosis/MitosisCLI.swift` (placeholder body, replaced in Task 18)
- Create: `Sources/MitosisCore/Resources/profiles.json` (empty list, filled in Task 6)
- Create: `Tests/MitosisCoreTests/ShellTests.swift`
- Create: repo-root `.gitignore` additions

**Interfaces:**
- Produces: `Mitosis.version: String`; `Date.mitosisNow: Date`; `Shell.run(_ executable: String, _ arguments: [String], environment: [String: String]? = nil, check: Bool = true) throws -> ShellResult`; `ShellResult { status: Int32; stdout: String; stderr: String }`; `ShellError.failed(command:status:stderr:)`.

- [ ] **Step 1: Create the package manifest**

`Packages/MitosisCore/Package.swift`:
```swift
// swift-tools-version: 6.0
import PackageDescription

let package = Package(
    name: "MitosisCore",
    platforms: [.macOS(.v15)],
    products: [
        .library(name: "MitosisCore", targets: ["MitosisCore"]),
        .executable(name: "mitosis", targets: ["mitosis"]),
        .executable(name: "LaunchStub", targets: ["LaunchStub"]),
    ],
    dependencies: [
        .package(url: "https://github.com/apple/swift-argument-parser", from: "1.5.0"),
    ],
    targets: [
        .target(
            name: "MitosisCore",
            resources: [.copy("Resources/profiles.json")]
        ),
        .executableTarget(name: "LaunchStub"),
        .executableTarget(
            name: "mitosis",
            dependencies: [
                "MitosisCore",
                .product(name: "ArgumentParser", package: "swift-argument-parser"),
            ]
        ),
        .testTarget(
            name: "MitosisCoreTests",
            dependencies: ["MitosisCore", "LaunchStub", "mitosis"]
        ),
    ]
)
```

- [ ] **Step 2: Create placeholder sources so the package builds**

`Sources/LaunchStub/main.swift`:
```swift
// Replaced in Task 10.
print("LaunchStub placeholder")
```
`Sources/mitosis/MitosisCLI.swift`:
```swift
// Replaced in Task 18.
@main
struct MitosisCLIPlaceholder {
    static func main() { print("mitosis placeholder") }
}
```
`Sources/MitosisCore/Resources/profiles.json`:
```json
{ "schema": 1, "profiles": [] }
```
`Sources/MitosisCore/Mitosis.swift`:
```swift
import Foundation

public enum Mitosis {
    public static let version = "0.1.0"
    public static let bundleID = "com.mitosis-mac.Mitosis"
}

extension Date {
    /// Current time truncated to whole seconds, so ISO-8601 round-trips are exact.
    public static var mitosisNow: Date {
        Date(timeIntervalSince1970: Date().timeIntervalSince1970.rounded(.down))
    }
}
```
Append to repo-root `/Volumes/SSD_1TB/DEV/mac-apps/app-cloner/.gitignore`:
```
.build/
.swiftpm/
*.xcuserstate
```

- [ ] **Step 3: Write the failing test**

`Tests/MitosisCoreTests/ShellTests.swift`:
```swift
import Testing
@testable import MitosisCore

@Suite struct ShellTests {
    @Test func capturesStdoutAndStatus() throws {
        let r = try Shell.run("/bin/echo", ["hello", "world"])
        #expect(r.status == 0)
        #expect(r.stdout == "hello world\n")
    }

    @Test func capturesStderrWithoutDeadlockOnLargeOutput() throws {
        // 200 KB on both streams would deadlock a naive pipe implementation.
        let r = try Shell.run("/bin/sh", ["-c", "head -c 200000 /dev/zero | tr '\\0' a; head -c 200000 /dev/zero | tr '\\0' b 1>&2"])
        #expect(r.stdout.count == 200_000)
        #expect(r.stderr.count == 200_000)
    }

    @Test func throwsOnNonZeroExitWhenChecking() {
        let error = #expect(throws: ShellError.self) {
            try Shell.run("/bin/sh", ["-c", "echo boom 1>&2; exit 3"])
        }
        #expect(error?.description.contains("(3)") == true)
        #expect(error?.description.contains("boom") == true)
    }

    @Test func returnsStatusWhenNotChecking() throws {
        let r = try Shell.run("/bin/sh", ["-c", "exit 4"], check: false)
        #expect(r.status == 4)
    }
}
```

- [ ] **Step 4: Run test to verify it fails**

Run: `swift test --filter ShellTests`
Expected: FAIL to compile — `cannot find 'Shell' in scope`.

- [ ] **Step 5: Implement `Shell`**

`Sources/MitosisCore/Shell.swift`:
```swift
import Foundation

public struct ShellResult: Sendable, Equatable {
    public let status: Int32
    public let stdout: String
    public let stderr: String
}

public enum ShellError: Error, CustomStringConvertible, Sendable {
    case failed(command: String, status: Int32, stderr: String)

    public var description: String {
        switch self {
        case let .failed(command, status, stderr):
            let detail = stderr.trimmingCharacters(in: .whitespacesAndNewlines)
            return "\(command) failed (\(status))\(detail.isEmpty ? "" : ": \(detail)")"
        }
    }
}

public enum Shell {
    /// Runs a tool and waits for it. Output goes through temp files so large outputs can't deadlock.
    @discardableResult
    public static func run(
        _ executable: String,
        _ arguments: [String],
        environment: [String: String]? = nil,
        check: Bool = true
    ) throws -> ShellResult {
        let fm = FileManager.default
        let tmp = fm.temporaryDirectory
        let outURL = tmp.appendingPathComponent("mitosis-shell-\(UUID().uuidString).out")
        let errURL = tmp.appendingPathComponent("mitosis-shell-\(UUID().uuidString).err")
        fm.createFile(atPath: outURL.path, contents: nil)
        fm.createFile(atPath: errURL.path, contents: nil)
        defer {
            try? fm.removeItem(at: outURL)
            try? fm.removeItem(at: errURL)
        }
        let outHandle = try FileHandle(forWritingTo: outURL)
        let errHandle = try FileHandle(forWritingTo: errURL)

        let process = Process()
        process.executableURL = URL(fileURLWithPath: executable)
        process.arguments = arguments
        if let environment { process.environment = environment }
        process.standardOutput = outHandle
        process.standardError = errHandle
        try process.run()
        process.waitUntilExit()
        try outHandle.close()
        try errHandle.close()

        let result = ShellResult(
            status: process.terminationStatus,
            stdout: String(decoding: try Data(contentsOf: outURL), as: UTF8.self),
            stderr: String(decoding: try Data(contentsOf: errURL), as: UTF8.self)
        )
        if check && result.status != 0 {
            throw ShellError.failed(
                command: ([executable] + arguments).joined(separator: " "),
                status: result.status,
                stderr: result.stderr
            )
        }
        return result
    }
}
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `swift test --filter ShellTests`
Expected: PASS (4 tests). The first run resolves swift-argument-parser (network) and compiles all targets.

- [ ] **Step 7: Commit**

```bash
cd /Volumes/SSD_1TB/DEV/mac-apps/app-cloner
git add .gitignore Packages/MitosisCore
git commit -m "feat(core): package scaffold and Shell runner

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Models and manifest JSON

**Files:**
- Create: `Sources/MitosisCore/Models.swift`
- Test: `Tests/MitosisCoreTests/ModelsTests.swift`

**Interfaces:**
- Consumes: `Mitosis.version`.
- Produces: `CloneMode` (`identity`, `fallback`); `SupportLevel` (`full`, `limited`, `unsupported`); `ModeDecision(mode: CloneMode?, support: SupportLevel, reasons: [String])`; `Badge(text: String, color: String)` + `Badge.presets: [String: String]` + `Badge.defaultColor`; `CloneManifest` (fields per spec 5.2, `CloneManifest.Source`, `CloneManifest.ProfileRef`, `CloneManifest.encoder()`, `CloneManifest.decoder()`); `RegistryEntry(manifest:bundlePath:)` with `id`, `bundleURL`.

- [ ] **Step 1: Write the failing test**

`Tests/MitosisCoreTests/ModelsTests.swift`:
```swift
import Foundation
import Testing
@testable import MitosisCore

@Suite struct ModelsTests {
    static func sampleManifest() -> CloneManifest {
        CloneManifest(
            id: UUID(uuidString: "6F1C2D3E-4A5B-4C6D-8E9F-0A1B2C3D4E5F")!,
            name: "Slack Work",
            badge: Badge(text: "W", color: "#0A84FF"),
            mode: .identity,
            cloneBundleID: "com.tinyspeck.slackmacgap.mitosis.slack-work",
            source: .init(path: "/Applications/Slack.app", bundleID: "com.tinyspeck.slackmacgap", version: "4.47.0", cdhash: "abc123"),
            profile: .init(id: "slack", version: 3),
            dataPath: "/Users/me/Library/Application Support/Mitosis/Data/6F1C",
            createdAt: Date(timeIntervalSince1970: 1_791_500_000),
            refreshedAt: Date(timeIntervalSince1970: 1_791_500_000)
        )
    }

    @Test func manifestRoundTripsThroughJSON() throws {
        let m = Self.sampleManifest()
        let data = try CloneManifest.encoder().encode(m)
        let back = try CloneManifest.decoder().decode(CloneManifest.self, from: data)
        #expect(back == m)
        let text = String(decoding: data, as: UTF8.self)
        #expect(text.contains("\"schema\" : 1"))
        #expect(text.contains("\"mitosisVersion\" : \"0.1.0\""))
        #expect(text.contains("2026-10-08T"))   // ISO-8601 dates
    }

    @Test func registryEntryExposesIDAndURL() {
        let e = RegistryEntry(manifest: Self.sampleManifest(), bundlePath: "/tmp/Slack Work.app")
        #expect(e.id == Self.sampleManifest().id)
        #expect(e.bundleURL.lastPathComponent == "Slack Work.app")
    }

    @Test func badgePresetsCoverEightSystemColors() {
        #expect(Badge.presets.count == 8)
        #expect(Badge.presets["blue"] == "#0A84FF")
        #expect(Badge.defaultColor == "#0A84FF")
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `swift test --filter ModelsTests`
Expected: FAIL to compile — `cannot find 'CloneManifest' in scope`.

- [ ] **Step 3: Implement the models**

`Sources/MitosisCore/Models.swift`:
```swift
import Foundation

public enum CloneMode: String, Codable, Sendable, CaseIterable {
    case identity
    case fallback
}

public enum SupportLevel: String, Codable, Sendable {
    case full
    case limited
    case unsupported
}

public struct ModeDecision: Equatable, Sendable {
    public let mode: CloneMode?
    public let support: SupportLevel
    public let reasons: [String]

    public init(mode: CloneMode?, support: SupportLevel, reasons: [String]) {
        self.mode = mode
        self.support = support
        self.reasons = reasons
    }
}

public struct Badge: Codable, Equatable, Sendable {
    public var text: String
    /// "#RRGGBB"
    public var color: String

    public init(text: String, color: String) {
        self.text = text
        self.color = color
    }

    public static let defaultColor = "#0A84FF"
    public static let presets: [String: String] = [
        "blue": "#0A84FF", "green": "#30D158", "orange": "#FF9F0A", "red": "#FF453A",
        "purple": "#BF5AF2", "pink": "#FF375F", "yellow": "#FFD60A", "gray": "#8E8E93",
    ]
}

public struct CloneManifest: Codable, Equatable, Sendable, Identifiable {
    public struct Source: Codable, Equatable, Sendable {
        public var path: String
        public var bundleID: String
        public var version: String
        public var cdhash: String?

        public init(path: String, bundleID: String, version: String, cdhash: String?) {
            self.path = path
            self.bundleID = bundleID
            self.version = version
            self.cdhash = cdhash
        }
    }

    public struct ProfileRef: Codable, Equatable, Sendable {
        public var id: String
        public var version: Int

        public init(id: String, version: Int) {
            self.id = id
            self.version = version
        }
    }

    public var schema: Int
    public var id: UUID
    public var name: String
    public var badge: Badge
    public var mode: CloneMode
    public var cloneBundleID: String
    public var source: Source
    public var profile: ProfileRef?
    public var dataPath: String
    public var createdAt: Date
    public var refreshedAt: Date
    public var mitosisVersion: String

    public init(
        id: UUID, name: String, badge: Badge, mode: CloneMode, cloneBundleID: String,
        source: Source, profile: ProfileRef?, dataPath: String,
        createdAt: Date, refreshedAt: Date, mitosisVersion: String = Mitosis.version
    ) {
        self.schema = 1
        self.id = id
        self.name = name
        self.badge = badge
        self.mode = mode
        self.cloneBundleID = cloneBundleID
        self.source = source
        self.profile = profile
        self.dataPath = dataPath
        self.createdAt = createdAt
        self.refreshedAt = refreshedAt
        self.mitosisVersion = mitosisVersion
    }

    public static func encoder() -> JSONEncoder {
        let e = JSONEncoder()
        e.outputFormatting = [.prettyPrinted, .sortedKeys]
        e.dateEncodingStrategy = .iso8601
        return e
    }

    public static func decoder() -> JSONDecoder {
        let d = JSONDecoder()
        d.dateDecodingStrategy = .iso8601
        return d
    }
}

public struct RegistryEntry: Codable, Equatable, Sendable, Identifiable {
    public var manifest: CloneManifest
    public var bundlePath: String

    public init(manifest: CloneManifest, bundlePath: String) {
        self.manifest = manifest
        self.bundlePath = bundlePath
    }

    public var id: UUID { manifest.id }
    public var bundleURL: URL { URL(fileURLWithPath: bundlePath) }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `swift test --filter ModelsTests`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add Sources/MitosisCore/Models.swift Tests/MitosisCoreTests/ModelsTests.swift
git commit -m "feat(core): clone models and manifest JSON

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: Slugs and clone-name validation

**Files:**
- Create: `Sources/MitosisCore/Naming.swift`
- Test: `Tests/MitosisCoreTests/NamingTests.swift`

**Interfaces:**
- Produces: `Slug.make(from name: String, existing: Set<String> = []) -> String`; `CloneName.validate(_ raw: String) throws -> String` (returns trimmed name); `CloneName.maxLength = 60`; `CloneNameError` (`empty`, `tooLong`, `leadingDot`, `invalidCharacter(Character)`) with user-facing `description`.

- [ ] **Step 1: Write the failing test**

`Tests/MitosisCoreTests/NamingTests.swift`:
```swift
import Testing
@testable import MitosisCore

@Suite struct NamingTests {
    @Test(arguments: [
        ("Slack Work", "slack-work"),
        ("  Café   Ñoño!! ", "cafe-nono"),
        ("Client_A 2", "client-a-2"),
        ("日本", "clone"),
        ("---", "clone"),
    ])
    func slugFromName(input: String, expected: String) {
        #expect(Slug.make(from: input) == expected)
    }

    @Test func slugAvoidsExistingValues() {
        #expect(Slug.make(from: "Slack Work", existing: ["slack-work"]) == "slack-work-2")
        #expect(Slug.make(from: "Slack Work", existing: ["slack-work", "slack-work-2"]) == "slack-work-3")
    }

    @Test func slugIsCappedAt40Characters() {
        let s = Slug.make(from: String(repeating: "abc ", count: 30))
        #expect(s.count <= 40)
        #expect(!s.hasSuffix("-"))
    }

    @Test func validNamesAreTrimmed() throws {
        #expect(try CloneName.validate("  Slack Work ") == "Slack Work")
        #expect(try CloneName.validate("Ünïcode Wörk") == "Ünïcode Wörk")
    }

    // Review Focus: odd names must be rejected clearly.
    @Test func invalidNamesAreRejected() {
        #expect(throws: CloneNameError.empty) { try CloneName.validate("   ") }
        #expect(throws: CloneNameError.leadingDot) { try CloneName.validate(".hidden") }
        #expect(throws: CloneNameError.invalidCharacter("/")) { try CloneName.validate("a/b") }
        #expect(throws: CloneNameError.invalidCharacter(":")) { try CloneName.validate("a:b") }
        #expect(throws: CloneNameError.tooLong) { try CloneName.validate(String(repeating: "x", count: 61)) }
    }

    @Test func errorMessagesAreFriendly() {
        #expect(CloneNameError.invalidCharacter("/").description == "Clone names can't contain \"/\".")
        #expect(CloneNameError.empty.description == "Give the clone a name.")
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `swift test --filter NamingTests`
Expected: FAIL to compile — `cannot find 'Slug' in scope`.

- [ ] **Step 3: Implement**

`Sources/MitosisCore/Naming.swift`:
```swift
import Foundation

public enum Slug {
    public static func make(from name: String, existing: Set<String> = []) -> String {
        let folded = name.folding(options: [.diacriticInsensitive, .caseInsensitive], locale: Locale(identifier: "en_US_POSIX"))
        var out = ""
        var lastWasHyphen = false
        for scalar in folded.lowercased().unicodeScalars {
            let v = scalar.value
            if (97...122).contains(v) || (48...57).contains(v) {
                out.unicodeScalars.append(scalar)
                lastWasHyphen = false
            } else if !out.isEmpty && !lastWasHyphen {
                out.append("-")
                lastWasHyphen = true
            }
        }
        if out.count > 40 { out = String(out.prefix(40)) }
        while out.hasSuffix("-") { out.removeLast() }
        if out.isEmpty { out = "clone" }

        var candidate = out
        var n = 2
        while existing.contains(candidate) {
            candidate = "\(out)-\(n)"
            n += 1
        }
        return candidate
    }
}

public enum CloneNameError: Error, Equatable, CustomStringConvertible, Sendable {
    case empty
    case tooLong
    case leadingDot
    case invalidCharacter(Character)

    public var description: String {
        switch self {
        case .empty: return "Give the clone a name."
        case .tooLong: return "Clone names can be at most \(CloneName.maxLength) characters."
        case .leadingDot: return "Clone names can't start with a dot."
        case .invalidCharacter(let c): return "Clone names can't contain \"\(c)\"."
        }
    }
}

public enum CloneName {
    public static let maxLength = 60

    public static func validate(_ raw: String) throws -> String {
        let name = raw.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !name.isEmpty else { throw CloneNameError.empty }
        guard name.count <= maxLength else { throw CloneNameError.tooLong }
        guard !name.hasPrefix(".") else { throw CloneNameError.leadingDot }
        if let bad = name.first(where: { $0 == "/" || $0 == ":" || $0.isNewline || $0 == "\u{0}" }) {
            throw CloneNameError.invalidCharacter(bad)
        }
        return name
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `swift test --filter NamingTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Sources/MitosisCore/Naming.swift Tests/MitosisCoreTests/NamingTests.swift
git commit -m "feat(core): slugs and clone-name validation

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Test fixtures + entitlements

**Files:**
- Create: `Tests/MitosisCoreTests/Support/TestSupport.swift`
- Create: `Tests/MitosisCoreTests/Support/FixtureFactory.swift`
- Create: `Sources/MitosisCore/Entitlements.swift`
- Test: `Tests/MitosisCoreTests/EntitlementsTests.swift`

**Interfaces:**
- Consumes: `Shell.run`.
- Produces (tests): `TestSupport.packageRoot`, `TestSupport.productsDirectory`, `TestSupport.stubBinary`, `TestSupport.cliBinary`, `TestSupport.tempDir() throws -> URL`; `FixtureOptions` (fields below); `FixtureBehavior` (`.stayOpen(seconds: Int)`, `.exit(code: Int32)`); `FixtureFactory.makeApp(in: URL, _ options: FixtureOptions) throws -> URL`.
- Produces (core): `Entitlements.read(from: URL) throws -> [String: Any]`; `Entitlements.isRestricted(_ key: String) -> Bool`; `Entitlements.restricted(in: [String: Any]) -> [String]` (sorted); `Entitlements.sanitized(_: [String: Any]) -> [String: Any]`; `Entitlements.xmlData(_: [String: Any]) throws -> Data`.

- [ ] **Step 1: Write test support**

`Tests/MitosisCoreTests/Support/TestSupport.swift`:
```swift
import Foundation

enum TestSupport {
    /// Packages/MitosisCore
    static let packageRoot: URL = URL(fileURLWithPath: #filePath)
        .deletingLastPathComponent()   // Support
        .deletingLastPathComponent()   // MitosisCoreTests
        .deletingLastPathComponent()   // Tests
        .deletingLastPathComponent()   // MitosisCore

    /// `swift build`/`swift test` products (the `.build/debug` symlink).
    static let productsDirectory: URL = {
        let dir = packageRoot.appendingPathComponent(".build/debug")
        precondition(FileManager.default.fileExists(atPath: dir.path), "Run tests with `swift test` from Packages/MitosisCore")
        return dir
    }()

    static var stubBinary: URL { productsDirectory.appendingPathComponent("LaunchStub") }
    static var cliBinary: URL { productsDirectory.appendingPathComponent("mitosis") }

    static func tempDir() throws -> URL {
        let url = FileManager.default.temporaryDirectory
            .appendingPathComponent("MitosisTests", isDirectory: true)
            .appendingPathComponent(UUID().uuidString, isDirectory: true)
        try FileManager.default.createDirectory(at: url, withIntermediateDirectories: true)
        return url
    }
}
```

- [ ] **Step 2: Write the fixture factory**

`Tests/MitosisCoreTests/Support/FixtureFactory.swift`:
```swift
import Foundation
@testable import MitosisCore

enum FixtureBehavior {
    case stayOpen(seconds: Int)
    case exit(code: Int32)
}

struct FixtureOptions {
    var name = "Fixture"
    var bundleID = "com.example.fixture"
    var version = "1.0"
    var build = "1"
    var executable = "Fixture"
    var entitlements: [String: Any] = [:]
    var extraInfo: [String: Any] = [:]
    /// Framework names, e.g. ["Electron Framework", "Sparkle"] → Contents/Frameworks/<name>.framework
    var frameworks: [String] = []
    /// Adds Contents/Frameworks/<name> Framework.framework/Versions/A/Helpers/<name> Helper.app (Chromium layout).
    var chromiumFramework: String? = nil
    /// Helper apps in Contents/Frameworks, e.g. ["Fixture Helper"] (Electron layout).
    var helperApps: [String] = []
    /// Adds Contents/Resources/app.asar.unpacked/addon.node (a Mach-O dylib).
    var nativeModule = false
    var masReceipt = false
    var signed = true
    var behavior: FixtureBehavior = .stayOpen(seconds: 30)
    var logFile: URL? = nil
}

enum FixtureFactory {
    private static let buildDir: URL = {
        let dir = FileManager.default.temporaryDirectory.appendingPathComponent("MitosisFixtureBuild-\(getpid())")
        try? FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
        return dir
    }()

    /// A tiny AppKit app: reads Contents/Resources/fixture.conf, logs args/env, then stays open or exits.
    private static let mainSource = #"""
    import AppKit
    import Darwin
    let resources = Bundle.main.resourceURL!
    var logPath: String?
    var stay = 30
    var exitCode: Int32?
    if let conf = try? String(contentsOf: resources.appendingPathComponent("fixture.conf"), encoding: .utf8) {
        for line in conf.split(separator: "\n") {
            if line.hasPrefix("log=") { logPath = String(line.dropFirst(4)) }
            if line.hasPrefix("stay=") { stay = Int(line.dropFirst(5)) ?? 30 }
            if line.hasPrefix("exit=") { exitCode = Int32(line.dropFirst(5)) }
        }
    }
    if let logPath {
        let env = ProcessInfo.processInfo.environment
        var out = "pid=\(getpid())\n"
        for a in CommandLine.arguments { out += "arg=\(a)\n" }
        out += "env.HOME=\(env["HOME"] ?? "")\n"
        out += "env.MITOSIS_TEST_VAR=\(env["MITOSIS_TEST_VAR"] ?? "")\n"
        if let h = FileHandle(forWritingAtPath: logPath) { h.seekToEndOfFile(); h.write(Data(out.utf8)); try? h.close() }
        else { FileManager.default.createFile(atPath: logPath, contents: Data(out.utf8)) }
    }
    if let exitCode { exit(exitCode) }
    let app = NSApplication.shared
    app.setActivationPolicy(.accessory)
    DispatchQueue.main.asyncAfter(deadline: .now() + .seconds(stay)) { exit(0) }
    app.run()
    """#

    static let mainBinary: URL = {
        let src = buildDir.appendingPathComponent("fixture-main.swift")
        let out = buildDir.appendingPathComponent("fixture-main")
        try! mainSource.write(to: src, atomically: true, encoding: .utf8)
        try! Shell.run("/usr/bin/xcrun", ["swiftc", "-swift-version", "5", "-O", "-o", out.path, src.path])
        return out
    }()

    static let dylib: URL = {
        let src = buildDir.appendingPathComponent("fixture-lib.c")
        let out = buildDir.appendingPathComponent("libfixture.dylib")
        try! "int fixture_value(void) { return 42; }\n".write(to: src, atomically: true, encoding: .utf8)
        try! Shell.run("/usr/bin/xcrun", ["clang", "-dynamiclib", "-o", out.path, src.path])
        return out
    }()

    static func makeApp(in dir: URL, _ o: FixtureOptions = FixtureOptions()) throws -> URL {
        let fm = FileManager.default
        let app = dir.appendingPathComponent("\(o.name).app")
        let contents = app.appendingPathComponent("Contents")
        let macOS = contents.appendingPathComponent("MacOS")
        let resources = contents.appendingPathComponent("Resources")
        let frameworksDir = contents.appendingPathComponent("Frameworks")
        try fm.createDirectory(at: macOS, withIntermediateDirectories: true)
        try fm.createDirectory(at: resources, withIntermediateDirectories: true)
        try fm.copyItem(at: mainBinary, to: macOS.appendingPathComponent(o.executable))

        var info: [String: Any] = [
            "CFBundleIdentifier": o.bundleID, "CFBundleName": o.name, "CFBundleExecutable": o.executable,
            "CFBundleShortVersionString": o.version, "CFBundleVersion": o.build,
            "CFBundlePackageType": "APPL", "CFBundleInfoDictionaryVersion": "6.0", "LSMinimumSystemVersion": "13.0",
        ]
        for (k, v) in o.extraInfo { info[k] = v }
        try writePlist(info, to: contents.appendingPathComponent("Info.plist"))

        var conf = ""
        switch o.behavior {
        case .stayOpen(let s): conf += "stay=\(s)\n"
        case .exit(let c): conf += "exit=\(c)\n"
        }
        if let log = o.logFile { conf += "log=\(log.path)\n" }
        try conf.write(to: resources.appendingPathComponent("fixture.conf"), atomically: true, encoding: .utf8)

        var nested: [URL] = []
        for name in o.frameworks {
            nested.append(try makeFramework(named: name, in: frameworksDir))
        }
        if let chromium = o.chromiumFramework {
            let fw = try makeFramework(named: "\(chromium) Framework", in: frameworksDir)
            let helpers = fw.appendingPathComponent("Versions/A/Helpers")
            try fm.createDirectory(at: helpers, withIntermediateDirectories: true)
            let helper = try makeHelperApp(named: "\(chromium) Helper", bundleID: "\(o.bundleID).helper", in: helpers)
            nested.insert(helper, at: 0)   // inner helper first…
            nested.append(fw)              // …then the framework that contains it
        }
        for name in o.helperApps {
            nested.append(try makeHelperApp(named: name, bundleID: "\(o.bundleID).helper", in: frameworksDir))
        }
        if o.nativeModule {
            let unpacked = resources.appendingPathComponent("app.asar.unpacked")
            try fm.createDirectory(at: unpacked, withIntermediateDirectories: true)
            let node = unpacked.appendingPathComponent("addon.node")
            try fm.copyItem(at: dylib, to: node)
            nested.append(node)
        }

        if o.signed {
            for item in nested { try Shell.run("/usr/bin/codesign", ["--force", "--sign", "-", item.path]) }
            var args = ["--force", "--sign", "-"]
            if !o.entitlements.isEmpty {
                let ent = dir.appendingPathComponent("\(UUID().uuidString).entitlements")
                try writePlist(o.entitlements, to: ent)
                args += ["--entitlements", ent.path]
            }
            try Shell.run("/usr/bin/codesign", args + [app.path])
        }
        if o.masReceipt {
            let receiptDir = contents.appendingPathComponent("_MASReceipt")
            try fm.createDirectory(at: receiptDir, withIntermediateDirectories: true)
            try Data("fake-receipt".utf8).write(to: receiptDir.appendingPathComponent("receipt"))
        }
        return app
    }

    static func writePlist(_ dict: [String: Any], to url: URL) throws {
        let data = try PropertyListSerialization.data(fromPropertyList: dict, format: .xml, options: 0)
        try data.write(to: url)
    }

    private static func makeFramework(named name: String, in frameworksDir: URL) throws -> URL {
        let fm = FileManager.default
        let fw = frameworksDir.appendingPathComponent("\(name).framework")
        let versionA = fw.appendingPathComponent("Versions/A")
        try fm.createDirectory(at: versionA.appendingPathComponent("Resources"), withIntermediateDirectories: true)
        try fm.createDirectory(at: versionA.appendingPathComponent("Libraries"), withIntermediateDirectories: true)
        try fm.copyItem(at: dylib, to: versionA.appendingPathComponent(name))
        try fm.copyItem(at: dylib, to: versionA.appendingPathComponent("Libraries/libextra.dylib"))
        try writePlist([
            "CFBundleIdentifier": "com.example.framework.\(Slug.make(from: name))", "CFBundleExecutable": name,
            "CFBundlePackageType": "FMWK", "CFBundleInfoDictionaryVersion": "6.0",
        ], to: versionA.appendingPathComponent("Resources/Info.plist"))
        try fm.createSymbolicLink(atPath: fw.appendingPathComponent("Versions/Current").path, withDestinationPath: "A")
        try fm.createSymbolicLink(atPath: fw.appendingPathComponent(name).path, withDestinationPath: "Versions/Current/\(name)")
        try fm.createSymbolicLink(atPath: fw.appendingPathComponent("Resources").path, withDestinationPath: "Versions/Current/Resources")
        // Libraries inside the framework must be signed before the framework itself.
        try Shell.run("/usr/bin/codesign", ["--force", "--sign", "-", versionA.appendingPathComponent("Libraries/libextra.dylib").path])
        return fw
    }

    private static func makeHelperApp(named name: String, bundleID: String, in dir: URL) throws -> URL {
        let fm = FileManager.default
        let app = dir.appendingPathComponent("\(name).app")
        let macOS = app.appendingPathComponent("Contents/MacOS")
        try fm.createDirectory(at: macOS, withIntermediateDirectories: true)
        try fm.copyItem(at: mainBinary, to: macOS.appendingPathComponent(name))
        try writePlist([
            "CFBundleIdentifier": bundleID, "CFBundleExecutable": name, "CFBundlePackageType": "APPL",
            "CFBundleInfoDictionaryVersion": "6.0",
        ], to: app.appendingPathComponent("Contents/Info.plist"))
        return app
    }
}
```

- [ ] **Step 3: Write the failing entitlements test**

`Tests/MitosisCoreTests/EntitlementsTests.swift`:
```swift
import Foundation
import Testing
@testable import MitosisCore

@Suite struct EntitlementsTests {
    @Test func readsAndClassifiesEntitlements() throws {
        let dir = try TestSupport.tempDir()
        var o = FixtureOptions()
        o.entitlements = [
            "com.apple.security.app-sandbox": true,
            "com.apple.security.network.client": true,
            "com.apple.developer.icloud-container-identifiers": ["iCloud.com.example"],
            "keychain-access-groups": ["ABCDE12345.com.example"],
            "com.apple.security.application-groups": ["group.com.example"],
        ]
        let app = try FixtureFactory.makeApp(in: dir, o)

        let ents = try Entitlements.read(from: app)
        #expect(ents.count == 5)
        #expect(Entitlements.restricted(in: ents) == [
            "com.apple.developer.icloud-container-identifiers",
            "com.apple.security.application-groups",
            "keychain-access-groups",
        ])
        let kept = Entitlements.sanitized(ents)
        #expect(Set(kept.keys) == ["com.apple.security.app-sandbox", "com.apple.security.network.client"])
    }

    // Review Focus: unsigned sources must not break inspection.
    @Test func unsignedAppHasNoEntitlements() throws {
        let dir = try TestSupport.tempDir()
        var o = FixtureOptions()
        o.signed = false
        let app = try FixtureFactory.makeApp(in: dir, o)
        #expect(try Entitlements.read(from: app).isEmpty)
    }

    @Test func xmlDataIsAValidPlist() throws {
        let data = try Entitlements.xmlData(["com.apple.security.app-sandbox": true])
        let back = try PropertyListSerialization.propertyList(from: data, format: nil) as? [String: Any]
        #expect(back?["com.apple.security.app-sandbox"] as? Bool == true)
    }
}
```

- [ ] **Step 4: Run test to verify it fails**

Run: `swift test --filter EntitlementsTests`
Expected: FAIL to compile — `cannot find 'Entitlements' in scope`.

- [ ] **Step 5: Implement**

`Sources/MitosisCore/Entitlements.swift`:
```swift
import Foundation

public enum Entitlements {
    /// Keys that only work with the original developer's certificate/team.
    static let restrictedKeys: Set<String> = [
        "keychain-access-groups",
        "com.apple.application-identifier",
        "application-identifier",
        "aps-environment",
        "com.apple.security.application-groups",
        "com.apple.team-identifier",
    ]

    public static func isRestricted(_ key: String) -> Bool {
        key.hasPrefix("com.apple.developer.") || restrictedKeys.contains(key)
    }

    /// Entitlements of the bundle's main executable. Unsigned or unreadable → empty.
    public static func read(from bundle: URL) throws -> [String: Any] {
        let r = try Shell.run("/usr/bin/codesign", ["-d", "--entitlements", "-", "--xml", bundle.path], check: false)
        guard r.status == 0 else { return [:] }
        let data = Data(r.stdout.utf8)
        guard !data.isEmpty else { return [:] }
        let plist = try PropertyListSerialization.propertyList(from: data, format: nil)
        return plist as? [String: Any] ?? [:]
    }

    public static func restricted(in entitlements: [String: Any]) -> [String] {
        entitlements.keys.filter(isRestricted).sorted()
    }

    public static func sanitized(_ entitlements: [String: Any]) -> [String: Any] {
        entitlements.filter { !isRestricted($0.key) }
    }

    public static func xmlData(_ entitlements: [String: Any]) throws -> Data {
        try PropertyListSerialization.data(fromPropertyList: entitlements, format: .xml, options: 0)
    }
}
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `swift test --filter EntitlementsTests`
Expected: PASS (first run compiles the fixture app, ~10 s).

- [ ] **Step 7: Commit**

```bash
git add Sources/MitosisCore/Entitlements.swift Tests/MitosisCoreTests
git commit -m "feat(core): entitlement reading/sanitizing and test fixtures

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: `AppInspector`

**Files:**
- Create: `Sources/MitosisCore/AppInspector.swift`
- Test: `Tests/MitosisCoreTests/AppInspectorTests.swift`

**Interfaces:**
- Consumes: `Entitlements.read`, `Entitlements.restricted`, `Shell.run`.
- Produces: `AppFramework` (`electron`, `chromium`, `sparkle`, `squirrel`, `catalyst`); `AppInfo` (`url`, `bundleID`, `name`, `version`, `build`, `executableName`, `isSandboxed`, `hasMASReceipt`, `isAppleApp`, `isMitosisClone`, `frameworks: Set<AppFramework>`, `restrictedEntitlements: [String]`, `cdhash: String?`); `InspectionError` (`notAnApp(String)`, `missingInfo(key:app:)`); `AppInspector().inspect(_ url: URL) throws -> AppInfo`; `AppInspector.readInfoPlist(_ app: URL) throws -> [String: Any]` (internal static).

- [ ] **Step 1: Write the failing test**

`Tests/MitosisCoreTests/AppInspectorTests.swift`:
```swift
import Foundation
import Testing
@testable import MitosisCore

@Suite struct AppInspectorTests {
    @Test func readsBasicFields() throws {
        let dir = try TestSupport.tempDir()
        var o = FixtureOptions()
        o.version = "4.47.0"; o.build = "447"
        let info = try AppInspector().inspect(try FixtureFactory.makeApp(in: dir, o))
        #expect(info.bundleID == "com.example.fixture")
        #expect(info.name == "Fixture")
        #expect(info.version == "4.47.0")
        #expect(info.build == "447")
        #expect(info.executableName == "Fixture")
        #expect(!info.isSandboxed)
        #expect(!info.hasMASReceipt)
        #expect(!info.isAppleApp)
        #expect(!info.isMitosisClone)
        #expect(info.frameworks.isEmpty)
        #expect(info.restrictedEntitlements.isEmpty)
        #expect(info.cdhash?.count == 40)
    }

    @Test func detectsSandboxReceiptAndRestrictedEntitlements() throws {
        let dir = try TestSupport.tempDir()
        var o = FixtureOptions()
        o.entitlements = ["com.apple.security.app-sandbox": true, "aps-environment": "production"]
        o.masReceipt = true
        let info = try AppInspector().inspect(try FixtureFactory.makeApp(in: dir, o))
        #expect(info.isSandboxed)
        #expect(info.hasMASReceipt)
        #expect(info.restrictedEntitlements == ["aps-environment"])
    }

    @Test func detectsFrameworks() throws {
        let dir = try TestSupport.tempDir()
        var electron = FixtureOptions()
        electron.name = "ElectronApp"
        electron.frameworks = ["Electron Framework", "Squirrel"]
        electron.helperApps = ["ElectronApp Helper"]
        #expect(try AppInspector().inspect(try FixtureFactory.makeApp(in: dir, electron)).frameworks == [.electron, .squirrel])

        var chromium = FixtureOptions()
        chromium.name = "ChromeApp"
        chromium.chromiumFramework = "ChromeApp"
        chromium.frameworks = ["Sparkle"]
        #expect(try AppInspector().inspect(try FixtureFactory.makeApp(in: dir, chromium)).frameworks == [.chromium, .sparkle])

        var catalyst = FixtureOptions()
        catalyst.name = "CatalystApp"
        catalyst.extraInfo = ["UIDeviceFamily": [2, 6]]
        #expect(try AppInspector().inspect(try FixtureFactory.makeApp(in: dir, catalyst)).frameworks == [.catalyst])
    }

    @Test func flagsAppleAppsAndMitosisClones() throws {
        let dir = try TestSupport.tempDir()
        var apple = FixtureOptions()
        apple.name = "AppleLike"; apple.bundleID = "com.apple.fake"
        #expect(try AppInspector().inspect(try FixtureFactory.makeApp(in: dir, apple)).isAppleApp)

        var clone = FixtureOptions()
        clone.name = "AlreadyClone"; clone.signed = false
        let cloneURL = try FixtureFactory.makeApp(in: dir, clone)
        try Data("{}".utf8).write(to: cloneURL.appendingPathComponent("Contents/Resources/mitosis.json"))
        #expect(try AppInspector().inspect(cloneURL).isMitosisClone)
    }

    // Review Focus: unsigned source still inspectable.
    @Test func unsignedAppHasNoCDHash() throws {
        let dir = try TestSupport.tempDir()
        var o = FixtureOptions(); o.signed = false
        let info = try AppInspector().inspect(try FixtureFactory.makeApp(in: dir, o))
        #expect(info.cdhash == nil)
        #expect(info.restrictedEntitlements.isEmpty)
    }

    @Test func rejectsNonApps() throws {
        let dir = try TestSupport.tempDir()
        #expect(throws: InspectionError.notAnApp(dir.path)) { try AppInspector().inspect(dir) }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `swift test --filter AppInspectorTests`
Expected: FAIL to compile — `cannot find 'AppInspector' in scope`.

- [ ] **Step 3: Implement**

`Sources/MitosisCore/AppInspector.swift`:
```swift
import Foundation

public enum AppFramework: String, Codable, Sendable, CaseIterable {
    case electron, chromium, sparkle, squirrel, catalyst
}

public struct AppInfo: Equatable, Sendable {
    public var url: URL
    public var bundleID: String
    public var name: String
    public var version: String
    public var build: String
    public var executableName: String
    public var isSandboxed: Bool
    public var hasMASReceipt: Bool
    public var isAppleApp: Bool
    public var isMitosisClone: Bool
    public var frameworks: Set<AppFramework>
    public var restrictedEntitlements: [String]
    public var cdhash: String?
}

public enum InspectionError: Error, Equatable, CustomStringConvertible, Sendable {
    case notAnApp(String)
    case missingInfo(key: String, app: String)

    public var description: String {
        switch self {
        case .notAnApp(let path): return "\(path) is not a Mac app."
        case let .missingInfo(key, app): return "\(app) is missing \(key) in its Info.plist."
        }
    }
}

public struct AppInspector: Sendable {
    public init() {}

    public func inspect(_ url: URL) throws -> AppInfo {
        let info = try Self.readInfoPlist(url)
        guard let bundleID = info["CFBundleIdentifier"] as? String else {
            throw InspectionError.missingInfo(key: "CFBundleIdentifier", app: url.lastPathComponent)
        }
        guard let executable = info["CFBundleExecutable"] as? String else {
            throw InspectionError.missingInfo(key: "CFBundleExecutable", app: url.lastPathComponent)
        }
        let fm = FileManager.default
        let entitlements = try Entitlements.read(from: url)
        let version = info["CFBundleShortVersionString"] as? String ?? "0"
        let resolved = url.resolvingSymlinksInPath().path
        return AppInfo(
            url: url,
            bundleID: bundleID,
            name: (info["CFBundleDisplayName"] as? String) ?? (info["CFBundleName"] as? String) ?? url.deletingPathExtension().lastPathComponent,
            version: version,
            build: info["CFBundleVersion"] as? String ?? version,
            executableName: executable,
            isSandboxed: (entitlements["com.apple.security.app-sandbox"] as? Bool) == true,
            hasMASReceipt: fm.fileExists(atPath: url.appendingPathComponent("Contents/_MASReceipt/receipt").path),
            isAppleApp: resolved.hasPrefix("/System/") || bundleID.hasPrefix("com.apple."),
            isMitosisClone: fm.fileExists(atPath: url.appendingPathComponent("Contents/Resources/mitosis.json").path),
            frameworks: Self.detectFrameworks(app: url, info: info),
            restrictedEntitlements: Entitlements.restricted(in: entitlements),
            cdhash: Self.cdhash(of: url)
        )
    }

    static func readInfoPlist(_ app: URL) throws -> [String: Any] {
        guard app.pathExtension == "app",
              let data = try? Data(contentsOf: app.appendingPathComponent("Contents/Info.plist")),
              let plist = try? PropertyListSerialization.propertyList(from: data, format: nil) as? [String: Any]
        else { throw InspectionError.notAnApp(app.path) }
        return plist
    }

    static func cdhash(of app: URL) -> String? {
        guard let r = try? Shell.run("/usr/bin/codesign", ["-dv", "--verbose=4", app.path], check: false),
              r.status == 0 else { return nil }
        for line in r.stderr.split(separator: "\n") where line.hasPrefix("CDHash=") {
            return String(line.dropFirst("CDHash=".count))
        }
        return nil
    }

    static func detectFrameworks(app: URL, info: [String: Any]) -> Set<AppFramework> {
        let fm = FileManager.default
        let dir = app.appendingPathComponent("Contents/Frameworks")
        let names = (try? fm.contentsOfDirectory(atPath: dir.path)) ?? []
        var result = Set<AppFramework>()
        if names.contains("Electron Framework.framework") { result.insert(.electron) }
        if names.contains("Sparkle.framework") { result.insert(.sparkle) }
        if names.contains("Squirrel.framework") { result.insert(.squirrel) }
        if !result.contains(.electron) {
            for name in names where name.hasSuffix(".framework") {
                let helpers = dir.appendingPathComponent(name).appendingPathComponent("Versions/Current/Helpers")
                let items = (try? fm.contentsOfDirectory(atPath: helpers.path)) ?? []
                if items.contains(where: { $0.hasSuffix(".app") }) { result.insert(.chromium); break }
            }
        }
        if info["UIDeviceFamily"] != nil { result.insert(.catalyst) }
        return result
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `swift test --filter AppInspectorTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Sources/MitosisCore/AppInspector.swift Tests/MitosisCoreTests/AppInspectorTests.swift
git commit -m "feat(core): app inspector

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: App profiles and default launch settings

**Files:**
- Create: `Sources/MitosisCore/Profiles.swift`
- Modify: `Sources/MitosisCore/Resources/profiles.json`
- Test: `Tests/MitosisCoreTests/ProfilesTests.swift`

**Interfaces:**
- Consumes: `CloneMode`, `SupportLevel`, `AppFramework`.
- Produces: `LaunchSettings(args: [String], env: [String: String])`, `LaunchSettings.none`, `.isEmpty`; `AppProfile` (`id`, `bundleIDs`, `version`, `mode`, `support`, `launch`, `notes`); `ProfileValidationError` (`unknownSchema(Int)`, `argNotAllowed(profile:arg:)`, `envNotAllowed(profile:key:)`); `ProfileStore(profiles: [AppProfile]) throws`, `ProfileStore.decode(_ data: Data) throws`, `ProfileStore.bundled() throws`, `profile(forBundleID:) -> AppProfile?`, `ProfileStore.allowedArgs`, `ProfileStore.allowedEnv`; `DefaultLaunchSettings.forFrameworks(_ frameworks: Set<AppFramework>) -> LaunchSettings`, `DefaultLaunchSettings.fallback`.

**Spike dependency:** read spec section 14 "Decisions" before Step 3.
- If Electron's decision is `args`, keep the code below as written.
- If it is `home`, change the `.electron` line in `DefaultLaunchSettings.forFrameworks` to `return LaunchSettings(args: [], env: ["HOME": "{dataPath}"])`, change the Electron test expectation in Step 1 to the same value, and use `"env": {"HOME": "{dataPath}"}` instead of the `args` entry for Electron profiles in Step 4.
- Apply the same rule to Chromium.
- If section 14 adds any other required arg or env key, add it to `allowedArgs`/`allowedEnv` together with a test case in `validatesAllowlist`.

- [ ] **Step 1: Write the failing test**

`Tests/MitosisCoreTests/ProfilesTests.swift`:
```swift
import Foundation
import Testing
@testable import MitosisCore

@Suite struct ProfilesTests {
    static func profile(args: [String] = [], env: [String: String] = [:]) -> AppProfile {
        AppProfile(id: "test", bundleIDs: ["com.example.fixture"], version: 1, mode: .identity, support: .full,
                   launch: LaunchSettings(args: args, env: env), notes: "Test profile.")
    }

    @Test func lookupByBundleID() throws {
        let store = try ProfileStore(profiles: [Self.profile()])
        #expect(store.profile(forBundleID: "com.example.fixture")?.id == "test")
        #expect(store.profile(forBundleID: "com.example.other") == nil)
    }

    @Test func validatesAllowlist() {
        #expect(throws: ProfileValidationError.argNotAllowed(profile: "test", arg: "--remote-debugging-port=9222")) {
            try ProfileStore(profiles: [Self.profile(args: ["--remote-debugging-port=9222"])])
        }
        #expect(throws: ProfileValidationError.envNotAllowed(profile: "test", key: "DYLD_INSERT_LIBRARIES")) {
            try ProfileStore(profiles: [Self.profile(env: ["DYLD_INSERT_LIBRARIES": "/tmp/x.dylib"])])
        }
        #expect(throws: ProfileValidationError.envNotAllowed(profile: "test", key: "HOME")) {
            try ProfileStore(profiles: [Self.profile(env: ["HOME": "/etc"])])   // value must be {dataPath}-based
        }
    }

    @Test func rejectsUnknownSchema() {
        #expect(throws: ProfileValidationError.unknownSchema(2)) {
            try ProfileStore.decode(Data(#"{"schema":2,"profiles":[]}"#.utf8))
        }
    }

    @Test func bundledProfilesLoadAndAreValid() throws {
        let store = try ProfileStore.bundled()
        #expect(store.profile(forBundleID: "com.google.Chrome") != nil)
        #expect(store.profile(forBundleID: "com.anthropic.claudefordesktop") != nil)
    }

    @Test func defaultLaunchSettings() {
        #expect(DefaultLaunchSettings.forFrameworks([.electron]) == LaunchSettings(args: ["--user-data-dir={dataPath}"], env: [:]))
        #expect(DefaultLaunchSettings.forFrameworks([.chromium, .sparkle]) == LaunchSettings(args: ["--user-data-dir={dataPath}"], env: [:]))
        #expect(DefaultLaunchSettings.forFrameworks([.sparkle]).isEmpty)
        #expect(DefaultLaunchSettings.fallback == LaunchSettings(args: [], env: ["HOME": "{dataPath}"]))
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `swift test --filter ProfilesTests`
Expected: FAIL to compile — `cannot find 'AppProfile' in scope`.

- [ ] **Step 3: Implement**

`Sources/MitosisCore/Profiles.swift`:
```swift
import Foundation

public struct LaunchSettings: Codable, Equatable, Sendable {
    public var args: [String]
    public var env: [String: String]

    public init(args: [String], env: [String: String]) {
        self.args = args
        self.env = env
    }

    public static let none = LaunchSettings(args: [], env: [:])
    public var isEmpty: Bool { args.isEmpty && env.isEmpty }
}

public struct AppProfile: Codable, Equatable, Sendable {
    public var id: String
    public var bundleIDs: [String]
    public var version: Int
    public var mode: CloneMode
    public var support: SupportLevel
    public var launch: LaunchSettings
    public var notes: String

    public init(id: String, bundleIDs: [String], version: Int, mode: CloneMode, support: SupportLevel, launch: LaunchSettings, notes: String) {
        self.id = id
        self.bundleIDs = bundleIDs
        self.version = version
        self.mode = mode
        self.support = support
        self.launch = launch
        self.notes = notes
    }
}

public enum ProfileValidationError: Error, Equatable, CustomStringConvertible, Sendable {
    case unknownSchema(Int)
    case argNotAllowed(profile: String, arg: String)
    case envNotAllowed(profile: String, key: String)

    public var description: String {
        switch self {
        case .unknownSchema(let s): return "Unsupported profiles schema \(s)."
        case let .argNotAllowed(p, a): return "Profile \(p) uses a launch argument that isn't allowed: \(a)"
        case let .envNotAllowed(p, k): return "Profile \(p) sets an environment variable that isn't allowed: \(k)"
        }
    }
}

public struct ProfileStore: Sendable {
    /// Only these launch arguments may appear in profiles (protects against e.g. remote-debugging flags).
    public static let allowedArgs: Set<String> = ["--user-data-dir={dataPath}"]
    /// Allowed environment variables and their only allowed values.
    public static let allowedEnv: [String: Set<String>] = ["HOME": ["{dataPath}", "{dataPath}/home"]]

    private struct File: Codable {
        var schema: Int
        var profiles: [AppProfile]
    }

    private let byBundleID: [String: AppProfile]

    public init(profiles: [AppProfile]) throws {
        var map: [String: AppProfile] = [:]
        for p in profiles {
            for arg in p.launch.args where !Self.allowedArgs.contains(arg) {
                throw ProfileValidationError.argNotAllowed(profile: p.id, arg: arg)
            }
            for (key, value) in p.launch.env where Self.allowedEnv[key]?.contains(value) != true {
                throw ProfileValidationError.envNotAllowed(profile: p.id, key: key)
            }
            for id in p.bundleIDs { map[id] = p }
        }
        byBundleID = map
    }

    public static func decode(_ data: Data) throws -> ProfileStore {
        let file = try JSONDecoder().decode(File.self, from: data)
        guard file.schema == 1 else { throw ProfileValidationError.unknownSchema(file.schema) }
        return try ProfileStore(profiles: file.profiles)
    }

    public static func bundled() throws -> ProfileStore {
        guard let url = Bundle.module.url(forResource: "profiles", withExtension: "json") else {
            return try ProfileStore(profiles: [])
        }
        return try decode(Data(contentsOf: url))
    }

    public func profile(forBundleID bundleID: String) -> AppProfile? {
        byBundleID[bundleID]
    }
}

public enum DefaultLaunchSettings {
    /// How identity-mode clones keep their data separate when no profile exists. Values from spec §14.
    public static func forFrameworks(_ frameworks: Set<AppFramework>) -> LaunchSettings {
        if frameworks.contains(.electron) { return LaunchSettings(args: ["--user-data-dir={dataPath}"], env: [:]) }
        if frameworks.contains(.chromium) { return LaunchSettings(args: ["--user-data-dir={dataPath}"], env: [:]) }
        return .none
    }

    /// Fallback-mode shortcuts separate data by giving the original app its own HOME.
    public static let fallback = LaunchSettings(args: [], env: ["HOME": "{dataPath}"])
}
```

- [ ] **Step 4: Fill the bundled profiles**

Bundle IDs verified on the developer's Mac on 2026-10-09: Claude `com.anthropic.claudefordesktop`, Codex `com.openai.codex`, Cursor `com.todesktop.230313mzl4w4u92`, VS Code `com.microsoft.VSCode`, Chrome `com.google.Chrome`, Signal `org.whispersystems.signal-desktop`, Telegram (App Store) `ru.keepcoder.Telegram`, WhatsApp `net.whatsapp.WhatsApp`, Notion `notion.id`. Slack `com.tinyspeck.slackmacgap` and Discord `com.hnc.Discord` are their well-known IDs. Set `mode`/`support` for Signal, Claude, VS Code and WhatsApp from spec §14; the rest follow their framework default until tested.

`Sources/MitosisCore/Resources/profiles.json`:
```json
{
  "schema": 1,
  "profiles": [
    { "id": "slack", "bundleIDs": ["com.tinyspeck.slackmacgap"], "version": 1, "mode": "identity", "support": "full",
      "launch": { "args": ["--user-data-dir={dataPath}"], "env": {} }, "notes": "Electron app. Each clone keeps its own workspaces and login." },
    { "id": "discord", "bundleIDs": ["com.hnc.Discord"], "version": 1, "mode": "identity", "support": "full",
      "launch": { "args": ["--user-data-dir={dataPath}"], "env": {} }, "notes": "Electron app. Each clone keeps its own account." },
    { "id": "claude", "bundleIDs": ["com.anthropic.claudefordesktop"], "version": 1, "mode": "identity", "support": "full",
      "launch": { "args": ["--user-data-dir={dataPath}"], "env": {} }, "notes": "Electron app. Each clone signs in separately." },
    { "id": "codex", "bundleIDs": ["com.openai.codex"], "version": 1, "mode": "identity", "support": "full",
      "launch": { "args": ["--user-data-dir={dataPath}"], "env": {} }, "notes": "Chromium-based app. Each clone signs in separately." },
    { "id": "cursor", "bundleIDs": ["com.todesktop.230313mzl4w4u92"], "version": 1, "mode": "identity", "support": "full",
      "launch": { "args": ["--user-data-dir={dataPath}"], "env": {} }, "notes": "Electron app. Each clone has its own settings and login." },
    { "id": "vscode", "bundleIDs": ["com.microsoft.VSCode"], "version": 1, "mode": "identity", "support": "full",
      "launch": { "args": ["--user-data-dir={dataPath}"], "env": {} }, "notes": "Electron app. Each clone has its own settings and extensions state." },
    { "id": "chrome", "bundleIDs": ["com.google.Chrome"], "version": 1, "mode": "identity", "support": "full",
      "launch": { "args": ["--user-data-dir={dataPath}"], "env": {} }, "notes": "Chromium browser. Each clone is a separate browser profile." },
    { "id": "signal", "bundleIDs": ["org.whispersystems.signal-desktop"], "version": 1, "mode": "identity", "support": "full",
      "launch": { "args": ["--user-data-dir={dataPath}"], "env": {} }, "notes": "Electron app. Link each clone as a separate device." },
    { "id": "notion", "bundleIDs": ["notion.id"], "version": 1, "mode": "identity", "support": "full",
      "launch": { "args": ["--user-data-dir={dataPath}"], "env": {} }, "notes": "Electron app. Each clone signs in separately." },
    { "id": "telegram", "bundleIDs": ["ru.keepcoder.Telegram"], "version": 1, "mode": "identity", "support": "limited",
      "launch": { "args": [], "env": {} }, "notes": "Mac App Store app. Its data is separated automatically; it may ask you to sign in again." },
    { "id": "whatsapp", "bundleIDs": ["net.whatsapp.WhatsApp"], "version": 1, "mode": "identity", "support": "limited",
      "launch": { "args": [], "env": {} }, "notes": "Sandboxed app. Its data is separated automatically; some features may not work in clones." }
  ]
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `swift test --filter ProfilesTests`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add Sources/MitosisCore/Profiles.swift Sources/MitosisCore/Resources/profiles.json Tests/MitosisCoreTests/ProfilesTests.swift
git commit -m "feat(core): app profiles with launch-setting allowlist

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 7: `ModeDecider`

**Files:**
- Create: `Sources/MitosisCore/ModeDecider.swift`
- Test: `Tests/MitosisCoreTests/ModeDeciderTests.swift`

**Interfaces:**
- Consumes: `AppInfo`, `ProfileStore`, `ModeDecision`.
- Produces: `ModeDecider(profiles: ProfileStore)`, `decide(for: AppInfo) -> ModeDecision`.

- [ ] **Step 1: Write the failing test**

`Tests/MitosisCoreTests/ModeDeciderTests.swift`:
```swift
import Foundation
import Testing
@testable import MitosisCore

@Suite struct ModeDeciderTests {
    static func info(bundleID: String = "com.example.app", apple: Bool = false, clone: Bool = false,
                     receipt: Bool = false, restricted: [String] = []) -> AppInfo {
        AppInfo(url: URL(fileURLWithPath: "/Applications/App.app"), bundleID: bundleID, name: "App", version: "1", build: "1",
                executableName: "App", isSandboxed: false, hasMASReceipt: receipt, isAppleApp: apple, isMitosisClone: clone,
                frameworks: [], restrictedEntitlements: restricted, cdhash: nil)
    }
    static let decider = try! ModeDecider(profiles: ProfileStore(profiles: [
        AppProfile(id: "p", bundleIDs: ["com.example.profiled"], version: 1, mode: .fallback, support: .limited,
                   launch: .none, notes: "Profile says fallback."),
        AppProfile(id: "u", bundleIDs: ["com.example.blocked"], version: 1, mode: .identity, support: .unsupported,
                   launch: .none, notes: "Known not to work."),
    ]))

    @Test func appleAppsAreUnsupported() {
        let d = Self.decider.decide(for: Self.info(apple: true))
        #expect(d == ModeDecision(mode: nil, support: .unsupported, reasons: ["Apple's own apps are protected by macOS and can't be cloned."]))
    }

    // Review Focus: cloning a clone is refused.
    @Test func mitosisClonesAreUnsupported() {
        let d = Self.decider.decide(for: Self.info(clone: true))
        #expect(d.support == .unsupported)
        #expect(d.mode == nil)
        #expect(d.reasons == ["This is already a Mitosis clone. Clone the original app instead."])
    }

    @Test func profileWins() {
        #expect(Self.decider.decide(for: Self.info(bundleID: "com.example.profiled", restricted: ["aps-environment"]))
                == ModeDecision(mode: .fallback, support: .limited, reasons: ["Profile says fallback."]))
        #expect(Self.decider.decide(for: Self.info(bundleID: "com.example.blocked"))
                == ModeDecision(mode: nil, support: .unsupported, reasons: ["Known not to work."]))
    }

    @Test func restrictedEntitlementsUseFallback() {
        let d = Self.decider.decide(for: Self.info(restricted: ["aps-environment", "keychain-access-groups"]))
        #expect(d.mode == .fallback)
        #expect(d.support == .limited)
        #expect(d.reasons.first?.contains("aps-environment, keychain-access-groups") == true)
    }

    @Test func appStoreAppsAreLimitedIdentity() {
        let d = Self.decider.decide(for: Self.info(receipt: true))
        #expect(d.mode == .identity)
        #expect(d.support == .limited)
    }

    @Test func plainAppsAreFullIdentity() {
        #expect(Self.decider.decide(for: Self.info())
                == ModeDecision(mode: .identity, support: .full, reasons: ["Fully supported: separate data, notifications and Dock icon."]))
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `swift test --filter ModeDeciderTests`
Expected: FAIL to compile — `cannot find 'ModeDecider' in scope`.

- [ ] **Step 3: Implement**

`Sources/MitosisCore/ModeDecider.swift`:
```swift
import Foundation

public struct ModeDecider: Sendable {
    public let profiles: ProfileStore

    public init(profiles: ProfileStore) {
        self.profiles = profiles
    }

    public func decide(for app: AppInfo) -> ModeDecision {
        if app.isAppleApp {
            return ModeDecision(mode: nil, support: .unsupported,
                                reasons: ["Apple's own apps are protected by macOS and can't be cloned."])
        }
        if app.isMitosisClone {
            return ModeDecision(mode: nil, support: .unsupported,
                                reasons: ["This is already a Mitosis clone. Clone the original app instead."])
        }
        if let profile = profiles.profile(forBundleID: app.bundleID) {
            return ModeDecision(mode: profile.support == .unsupported ? nil : profile.mode,
                                support: profile.support, reasons: [profile.notes])
        }
        if !app.restrictedEntitlements.isEmpty {
            let list = app.restrictedEntitlements.joined(separator: ", ")
            return ModeDecision(mode: .fallback, support: .limited, reasons: [
                "Uses features tied to its developer (\(list)), so the clone runs in compatibility mode: it shares the original's notifications and must be opened after the original.",
            ])
        }
        if app.hasMASReceipt {
            return ModeDecision(mode: .identity, support: .limited, reasons: [
                "Mac App Store app: the clone may ask you to sign in again or refuse to start.",
            ])
        }
        return ModeDecision(mode: .identity, support: .full,
                            reasons: ["Fully supported: separate data, notifications and Dock icon."])
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `swift test --filter ModeDeciderTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Sources/MitosisCore/ModeDecider.swift Tests/MitosisCoreTests/ModeDeciderTests.swift
git commit -m "feat(core): mode decider

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 8: `InfoPlistEditor`

**Files:**
- Create: `Sources/MitosisCore/InfoPlistEditor.swift`
- Test: `Tests/MitosisCoreTests/InfoPlistEditorTests.swift`

**Interfaces:**
- Produces: `InfoPlistEdit(bundleID: String, name: String, iconFile: String?, disableSparkle: Bool)`; `InfoPlistEditor.apply(_ edit: InfoPlistEdit, to infoPlist: URL) throws`; `InfoPlistEditor.read(_ url: URL) throws -> [String: Any]`; `InfoPlistEditor.write(_ dict: [String: Any], to url: URL) throws`.

- [ ] **Step 1: Write the failing test**

`Tests/MitosisCoreTests/InfoPlistEditorTests.swift`:
```swift
import Foundation
import Testing
@testable import MitosisCore

@Suite struct InfoPlistEditorTests {
    @Test func appliesIdentityIconAndSparkleEdits() throws {
        let dir = try TestSupport.tempDir()
        let url = dir.appendingPathComponent("Info.plist")
        try InfoPlistEditor.write([
            "CFBundleIdentifier": "com.example.app", "CFBundleName": "App", "CFBundleIconName": "AppIcon",
            "CFBundleIcons": ["x": 1], "SUFeedURL": "https://example.com/appcast.xml",
            "ElectronAsarIntegrity": ["Resources/app.asar": ["hash": "abc"]],
        ], to: url)

        try InfoPlistEditor.apply(InfoPlistEdit(bundleID: "com.example.app.mitosis.work", name: "App Work",
                                                iconFile: "MitosisIcon", disableSparkle: true), to: url)

        let d = try InfoPlistEditor.read(url)
        #expect(d["CFBundleIdentifier"] as? String == "com.example.app.mitosis.work")
        #expect(d["CFBundleName"] as? String == "App Work")
        #expect(d["CFBundleDisplayName"] as? String == "App Work")
        #expect(d["CFBundleIconFile"] as? String == "MitosisIcon")
        #expect(d["CFBundleIconName"] == nil)
        #expect(d["CFBundleIcons"] == nil)
        #expect(d["SUFeedURL"] == nil)
        #expect(d["SUEnableAutomaticChecks"] as? Bool == false)
        #expect(d["SUAutomaticallyUpdate"] as? Bool == false)
        #expect(d["ElectronAsarIntegrity"] != nil)   // must be preserved
    }

    @Test func leavesSparkleKeysAloneWhenNotRequested() throws {
        let dir = try TestSupport.tempDir()
        let url = dir.appendingPathComponent("Info.plist")
        try InfoPlistEditor.write(["CFBundleIdentifier": "a", "SUFeedURL": "https://x"], to: url)
        try InfoPlistEditor.apply(InfoPlistEdit(bundleID: "b", name: "B", iconFile: nil, disableSparkle: false), to: url)
        let d = try InfoPlistEditor.read(url)
        #expect(d["SUFeedURL"] as? String == "https://x")
        #expect(d["CFBundleIconFile"] == nil)
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `swift test --filter InfoPlistEditorTests`
Expected: FAIL to compile — `cannot find 'InfoPlistEditor' in scope`.

- [ ] **Step 3: Implement**

`Sources/MitosisCore/InfoPlistEditor.swift`:
```swift
import Foundation

public struct InfoPlistEdit: Sendable, Equatable {
    public var bundleID: String
    public var name: String
    public var iconFile: String?
    public var disableSparkle: Bool

    public init(bundleID: String, name: String, iconFile: String?, disableSparkle: Bool) {
        self.bundleID = bundleID
        self.name = name
        self.iconFile = iconFile
        self.disableSparkle = disableSparkle
    }
}

public enum InfoPlistEditor {
    public static func read(_ url: URL) throws -> [String: Any] {
        let data = try Data(contentsOf: url)
        return try PropertyListSerialization.propertyList(from: data, format: nil) as? [String: Any] ?? [:]
    }

    public static func write(_ dict: [String: Any], to url: URL) throws {
        let data = try PropertyListSerialization.data(fromPropertyList: dict, format: .xml, options: 0)
        try data.write(to: url, options: .atomic)
    }

    public static func apply(_ edit: InfoPlistEdit, to url: URL) throws {
        var d = try read(url)
        d["CFBundleIdentifier"] = edit.bundleID
        d["CFBundleName"] = edit.name
        d["CFBundleDisplayName"] = edit.name
        if let icon = edit.iconFile {
            d["CFBundleIconFile"] = icon
            d.removeValue(forKey: "CFBundleIconName")   // asset-catalog icon would win over the .icns
            d.removeValue(forKey: "CFBundleIcons")
        }
        if edit.disableSparkle {
            d["SUEnableAutomaticChecks"] = false
            d["SUAutomaticallyUpdate"] = false
            d.removeValue(forKey: "SUFeedURL")
        }
        try write(d, to: url)
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `swift test --filter InfoPlistEditorTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Sources/MitosisCore/InfoPlistEditor.swift Tests/MitosisCoreTests/InfoPlistEditorTests.swift
git commit -m "feat(core): Info.plist editor

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 9: `FileCloner`

**Files:**
- Create: `Sources/MitosisCore/FileCloner.swift`
- Test: `Tests/MitosisCoreTests/FileClonerTests.swift`

**Interfaces:**
- Produces: `FileCloner.cloneOrCopy(from: URL, to: URL) throws -> Bool` (true = APFS clone, false = regular copy); `FileCloner.isSameVolume(_ a: URL, _ b: URL) -> Bool` (b may not exist yet; its nearest existing ancestor is used); `FileCloner.allocatedSize(of: URL) -> Int64`.

- [ ] **Step 1: Write the failing test**

`Tests/MitosisCoreTests/FileClonerTests.swift`:
```swift
import Foundation
import Testing
@testable import MitosisCore

@Suite struct FileClonerTests {
    @Test func clonesADirectoryTreeOnTheSameVolume() throws {
        let dir = try TestSupport.tempDir()
        let src = try FixtureFactory.makeApp(in: dir)
        let dst = dir.appendingPathComponent("Copy.app")
        let cloned = try FileCloner.cloneOrCopy(from: src, to: dst)
        #expect(cloned)   // temp dir is on the APFS data volume
        #expect(FileManager.default.fileExists(atPath: dst.appendingPathComponent("Contents/MacOS/Fixture").path))
        #expect(FileManager.default.isWritableFile(atPath: dst.appendingPathComponent("Contents/Info.plist").path))
    }

    @Test func failsWhenDestinationExists() throws {
        let dir = try TestSupport.tempDir()
        let src = try FixtureFactory.makeApp(in: dir)
        #expect(throws: (any Error).self) { try FileCloner.cloneOrCopy(from: src, to: src) }
    }

    @Test func sameVolumeDetection() throws {
        let dir = try TestSupport.tempDir()
        #expect(FileCloner.isSameVolume(dir, dir.appendingPathComponent("not/yet/created")))
        #expect(!FileCloner.isSameVolume(dir, URL(fileURLWithPath: "/System/Library")))   // sealed system volume
    }

    @Test func sizeIsPositive() throws {
        let dir = try TestSupport.tempDir()
        #expect(FileCloner.allocatedSize(of: try FixtureFactory.makeApp(in: dir)) > 0)
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `swift test --filter FileClonerTests`
Expected: FAIL to compile — `cannot find 'FileCloner' in scope`.

- [ ] **Step 3: Implement**

`Sources/MitosisCore/FileCloner.swift`:
```swift
import Darwin
import Foundation

public enum FileCloner {
    /// APFS-clones `from` to `to` (instant, no extra disk). Falls back to a full copy across volumes.
    /// Ownership is not copied (CLONE_NOOWNERCOPY), so the current user owns the result.
    @discardableResult
    public static func cloneOrCopy(from: URL, to: URL) throws -> Bool {
        if clonefile(from.path, to.path, UInt32(CLONE_NOOWNERCOPY)) == 0 {
            return true
        }
        let err = errno
        if err == EXDEV || err == ENOTSUP {
            try FileManager.default.copyItem(at: from, to: to)
            return false
        }
        throw POSIXError(POSIXErrorCode(rawValue: err) ?? .EIO)
    }

    public static func isSameVolume(_ a: URL, _ b: URL) -> Bool {
        guard let va = volumeID(of: a), let vb = volumeID(of: nearestExisting(b)) else { return false }
        return va.isEqual(vb)
    }

    public static func allocatedSize(of url: URL) -> Int64 {
        let keys: [URLResourceKey] = [.totalFileAllocatedSizeKey, .isRegularFileKey]
        guard let e = FileManager.default.enumerator(at: url, includingPropertiesForKeys: keys) else { return 0 }
        var total: Int64 = 0
        for case let item as URL in e {
            let v = try? item.resourceValues(forKeys: Set(keys))
            if v?.isRegularFile == true { total += Int64(v?.totalFileAllocatedSize ?? 0) }
        }
        return total
    }

    private static func volumeID(of url: URL) -> NSObject? {
        (try? url.resourceValues(forKeys: [.volumeIdentifierKey]))?.volumeIdentifier as? NSObject
    }

    private static func nearestExisting(_ url: URL) -> URL {
        var u = url
        while !FileManager.default.fileExists(atPath: u.path) && u.path != "/" {
            u = u.deletingLastPathComponent()
        }
        return u
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `swift test --filter FileClonerTests`
Expected: PASS. If the compiler can't see `CLONE_NOOWNERCOPY`, use its value directly: `UInt32(0x0002)` (from `<sys/clonefile.h>`). If `clonesADirectoryTreeOnTheSameVolume` reports a non-writable Info.plist, add `try Shell.run("/bin/chmod", ["-R", "u+w", to.path])` at the end of the clone branch and re-run.

- [ ] **Step 5: Commit**

```bash
git add Sources/MitosisCore/FileCloner.swift Tests/MitosisCoreTests/FileClonerTests.swift
git commit -m "feat(core): APFS clone with copy fallback

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 10: `LaunchConfig` and the launch stub

**Files:**
- Create: `Sources/MitosisCore/LaunchConfig.swift`
- Modify: `Sources/LaunchStub/main.swift` (replace placeholder)
- Test: `Tests/MitosisCoreTests/LaunchStubTests.swift`

**Interfaces:**
- Consumes: `LaunchSettings`.
- Produces: `LaunchConfig(kind: Kind, target: String, args: [String], env: [String: String])`, `LaunchConfig.Kind` (`exec`, `open`), `LaunchConfig.resolve(_ settings: LaunchSettings, kind:target:dataPath:) -> LaunchConfig`, `LaunchConfig.fileName = "mitosis-launch.json"`, `write(toResources: URL) throws`. Stub contract: in `exec` mode runs `Contents/MacOS/<target>` with `config.args + passthrough args` and `config.env` added, preserving the process (execv); in `open` mode runs `/usr/bin/open -n -a <target> [--env K=V]... [--args ...]` and exits with its status; drops macOS `-psn_*` args.

- [ ] **Step 1: Write the failing test**

`Tests/MitosisCoreTests/LaunchStubTests.swift`:
```swift
import Foundation
import Testing
@testable import MitosisCore

@Suite struct LaunchStubTests {
    @Test func resolvesDataPathPlaceholders() {
        let c = LaunchConfig.resolve(LaunchSettings(args: ["--user-data-dir={dataPath}"], env: ["HOME": "{dataPath}/home"]),
                                     kind: .exec, target: "App.mitosis-real", dataPath: "/d/1")
        #expect(c == LaunchConfig(kind: .exec, target: "App.mitosis-real", args: ["--user-data-dir=/d/1"], env: ["HOME": "/d/1/home"]))
    }

    @Test func execModeRunsRealExecutableWithArgsAndEnv() throws {
        let dir = try TestSupport.tempDir()
        let log = dir.appendingPathComponent("run.log")
        var o = FixtureOptions(); o.behavior = .exit(code: 0); o.logFile = log; o.signed = false
        let app = try FixtureFactory.makeApp(in: dir, o)
        let macOS = app.appendingPathComponent("Contents/MacOS")
        try FileManager.default.moveItem(at: macOS.appendingPathComponent("Fixture"), to: macOS.appendingPathComponent("Fixture.mitosis-real"))
        try FileManager.default.copyItem(at: TestSupport.stubBinary, to: macOS.appendingPathComponent("Fixture"))
        try LaunchConfig(kind: .exec, target: "Fixture.mitosis-real", args: ["--user-data-dir=/tmp/x y"], env: ["MITOSIS_TEST_VAR": "hello"])
            .write(toResources: app.appendingPathComponent("Contents/Resources"))

        let r = try Shell.run(macOS.appendingPathComponent("Fixture").path, ["extra", "-psn_0_12345"])
        #expect(r.status == 0)
        let text = try String(contentsOf: log, encoding: .utf8)
        #expect(text.contains("Contents/MacOS/Fixture.mitosis-real\n"))   // argv[0] is the real executable (path may be /private-prefixed)
        #expect(text.contains("arg=--user-data-dir=/tmp/x y"))
        #expect(text.contains("arg=extra"))
        #expect(!text.contains("-psn_"))
        #expect(text.contains("env.MITOSIS_TEST_VAR=hello"))
    }

    @Test func missingConfigFailsWith127() throws {
        let dir = try TestSupport.tempDir()
        var o = FixtureOptions(); o.signed = false
        let app = try FixtureFactory.makeApp(in: dir, o)
        let stub = app.appendingPathComponent("Contents/MacOS/Stub")
        try FileManager.default.copyItem(at: TestSupport.stubBinary, to: stub)
        let r = try Shell.run(stub.path, [], check: false)
        #expect(r.status == 127)
        #expect(r.stderr.contains("mitosis-launch.json"))
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `swift test --filter LaunchStubTests`
Expected: FAIL to compile — `cannot find 'LaunchConfig' in scope`.

- [ ] **Step 3: Implement `LaunchConfig`**

`Sources/MitosisCore/LaunchConfig.swift`:
```swift
import Foundation

/// Resolved launch instructions stored inside a clone and read by LaunchStub.
public struct LaunchConfig: Codable, Equatable, Sendable {
    public enum Kind: String, Codable, Sendable {
        /// Replace the stub process with Contents/MacOS/<target> (identity mode).
        case exec
        /// Ask macOS to open the app at absolute path <target> as a new instance (fallback mode).
        case open
    }

    public var kind: Kind
    public var target: String
    public var args: [String]
    public var env: [String: String]

    public static let fileName = "mitosis-launch.json"

    public init(kind: Kind, target: String, args: [String], env: [String: String]) {
        self.kind = kind
        self.target = target
        self.args = args
        self.env = env
    }

    public static func resolve(_ settings: LaunchSettings, kind: Kind, target: String, dataPath: String) -> LaunchConfig {
        func fill(_ s: String) -> String { s.replacingOccurrences(of: "{dataPath}", with: dataPath) }
        return LaunchConfig(kind: kind, target: target, args: settings.args.map(fill), env: settings.env.mapValues(fill))
    }

    public func write(toResources resources: URL) throws {
        try FileManager.default.createDirectory(at: resources, withIntermediateDirectories: true)
        let e = JSONEncoder()
        e.outputFormatting = [.prettyPrinted, .sortedKeys]
        try e.encode(self).write(to: resources.appendingPathComponent(Self.fileName), options: .atomic)
    }
}
```

- [ ] **Step 4: Implement the stub**

`Sources/LaunchStub/main.swift` (replace the whole file; no dependency on MitosisCore to stay tiny):
```swift
import Darwin
import Foundation

struct StubConfig: Decodable {
    let kind: String
    let target: String
    let args: [String]
    let env: [String: String]
}

func fail(_ message: String) -> Never {
    FileHandle.standardError.write(Data("mitosis-launch: \(message)\n".utf8))
    exit(127)
}

guard let configURL = Bundle.main.url(forResource: "mitosis-launch", withExtension: "json"),
      let data = try? Data(contentsOf: configURL),
      let config = try? JSONDecoder().decode(StubConfig.self, from: data)
else { fail("missing or invalid mitosis-launch.json") }

let passthrough = CommandLine.arguments.dropFirst().filter { !$0.hasPrefix("-psn_") }

switch config.kind {
case "exec":
    guard let macOSDir = Bundle.main.executableURL?.deletingLastPathComponent() else { fail("cannot locate executable") }
    let target = macOSDir.appendingPathComponent(config.target).path
    for (key, value) in config.env { setenv(key, value, 1) }
    let argv = [target] + config.args + passthrough
    var cArgs: [UnsafeMutablePointer<CChar>?] = argv.map { strdup($0) } + [nil]
    execv(target, &cArgs)
    fail("execv \(target) failed: \(String(cString: strerror(errno)))")

case "open":
    var openArgs = ["-n", "-a", config.target]
    for (key, value) in config.env.sorted(by: { $0.key < $1.key }) { openArgs += ["--env", "\(key)=\(value)"] }
    let launchArgs = config.args + passthrough
    if !launchArgs.isEmpty { openArgs += ["--args"] + launchArgs }
    let p = Process()
    p.executableURL = URL(fileURLWithPath: "/usr/bin/open")
    p.arguments = openArgs
    do {
        try p.run()
        p.waitUntilExit()
        exit(p.terminationStatus)
    } catch {
        fail("open failed: \(error)")
    }

default:
    fail("unknown launch kind \(config.kind)")
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `swift test --filter LaunchStubTests`
Expected: PASS (3 tests).

- [ ] **Step 6: Commit**

```bash
git add Sources/MitosisCore/LaunchConfig.swift Sources/LaunchStub/main.swift Tests/MitosisCoreTests/LaunchStubTests.swift
git commit -m "feat: launch config and LaunchStub

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 11: `IconRenderer`

**Files:**
- Create: `Sources/MitosisCore/IconRenderer.swift`
- Test: `Tests/MitosisCoreTests/IconRendererTests.swift`

**Interfaces:**
- Consumes: `Badge`.
- Produces: `IconRenderer.iconFileBaseName = "MitosisIcon"`; `IconRenderer.baseIcon(forApp: URL) -> CGImage?`; `IconRenderer.render(base: CGImage, badge: Badge, size: Int) -> CGImage?`; `IconRenderer.icnsData(base: CGImage, badge: Badge) throws -> Data`; `IconRenderer.color(fromHex: String) -> CGColor` (invalid → `Badge.defaultColor`); `IconError` (`noBaseIcon(String)`, `renderFailed(Int)`, `encodeFailed`).

- [ ] **Step 1: Write the failing test**

`Tests/MitosisCoreTests/IconRendererTests.swift`:
```swift
import CoreGraphics
import Foundation
import ImageIO
import Testing
@testable import MitosisCore

@Suite struct IconRendererTests {
    @Test func baseIconExistsForAnyApp() throws {
        let dir = try TestSupport.tempDir()
        let icon = IconRenderer.baseIcon(forApp: try FixtureFactory.makeApp(in: dir))
        #expect((icon?.width ?? 0) >= 512)
    }

    @Test func renderProducesRequestedSize() throws {
        let dir = try TestSupport.tempDir()
        let base = try #require(IconRenderer.baseIcon(forApp: try FixtureFactory.makeApp(in: dir)))
        let img = try #require(IconRenderer.render(base: base, badge: Badge(text: "W", color: "#0A84FF"), size: 256))
        #expect(img.width == 256 && img.height == 256)
    }

    @Test func icnsContainsSmallAndLargeImages() throws {
        let dir = try TestSupport.tempDir()
        let base = try #require(IconRenderer.baseIcon(forApp: try FixtureFactory.makeApp(in: dir)))
        let data = try IconRenderer.icnsData(base: base, badge: Badge(text: "Wk", color: "#30D158"))
        let src = try #require(CGImageSourceCreateWithData(data as CFData, nil))
        let widths = (0..<CGImageSourceGetCount(src)).compactMap { CGImageSourceCreateImageAtIndex(src, $0, nil)?.width }
        #expect(widths.contains(1024))
        #expect(widths.contains(16))
    }

    @Test func hexColorParsing() {
        let c = IconRenderer.color(fromHex: "#30D158").components!
        #expect(abs(c[0] - 0x30 / 255.0) < 0.01 && abs(c[1] - 0xD1 / 255.0) < 0.01 && abs(c[2] - 0x58 / 255.0) < 0.01)
        let fallback = IconRenderer.color(fromHex: "nope").components!
        #expect(abs(fallback[2] - 1.0) < 0.01)   // #0A84FF blue
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `swift test --filter IconRendererTests`
Expected: FAIL to compile — `cannot find 'IconRenderer' in scope`.

- [ ] **Step 3: Implement**

`Sources/MitosisCore/IconRenderer.swift`:
```swift
import AppKit
import CoreText
import ImageIO
import UniformTypeIdentifiers

public enum IconError: Error, Equatable, CustomStringConvertible, Sendable {
    case noBaseIcon(String)
    case renderFailed(Int)
    case encodeFailed

    public var description: String {
        switch self {
        case .noBaseIcon(let app): return "Couldn't read the icon of \(app)."
        case .renderFailed(let size): return "Couldn't draw the \(size)px icon."
        case .encodeFailed: return "Couldn't save the clone icon."
        }
    }
}

public enum IconRenderer {
    public static let iconFileBaseName = "MitosisIcon"
    static let icnsSizes = [16, 32, 128, 256, 512, 1024]

    public static func baseIcon(forApp url: URL) -> CGImage? {
        let image = NSWorkspace.shared.icon(forFile: url.path)
        image.size = NSSize(width: 1024, height: 1024)
        var rect = CGRect(x: 0, y: 0, width: 1024, height: 1024)
        return image.cgImage(forProposedRect: &rect, context: nil, hints: nil)
    }

    public static func render(base: CGImage, badge: Badge, size: Int) -> CGImage? {
        guard let space = CGColorSpace(name: CGColorSpace.sRGB),
              let ctx = CGContext(data: nil, width: size, height: size, bitsPerComponent: 8, bytesPerRow: 0,
                                  space: space, bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)
        else { return nil }
        let s = CGFloat(size)
        ctx.interpolationQuality = .high
        ctx.draw(base, in: CGRect(x: 0, y: 0, width: s, height: s))

        // Badge circle in the bottom-right corner (Core Graphics origin is bottom-left).
        let d = s * 0.42
        let circle = CGRect(x: s - d - s * 0.04, y: s * 0.04, width: d, height: d)
        ctx.setFillColor(CGColor(gray: 1, alpha: 1))
        ctx.fillEllipse(in: circle.insetBy(dx: -s * 0.02, dy: -s * 0.02))
        ctx.setFillColor(color(fromHex: badge.color))
        ctx.fillEllipse(in: circle)

        let text = String(badge.text.prefix(2)).uppercased()
        if !text.isEmpty, size >= 32,
           let font = CTFontCreateUIFontForLanguage(.emphasizedSystem, d * (text.count == 1 ? 0.6 : 0.45), nil) {
            let attributed = NSAttributedString(string: text, attributes: [
                NSAttributedString.Key(kCTFontAttributeName as String): font,
                NSAttributedString.Key(kCTForegroundColorAttributeName as String): CGColor(gray: 1, alpha: 1),
            ])
            let line = CTLineCreateWithAttributedString(attributed)
            let b = CTLineGetBoundsWithOptions(line, .useGlyphPathBounds)
            ctx.textPosition = CGPoint(x: circle.midX - b.width / 2 - b.minX, y: circle.midY - b.height / 2 - b.minY)
            CTLineDraw(line, ctx)
        }
        return ctx.makeImage()
    }

    public static func icnsData(base: CGImage, badge: Badge) throws -> Data {
        let data = NSMutableData()
        guard let dest = CGImageDestinationCreateWithData(data, UTType.icns.identifier as CFString, icnsSizes.count, nil) else {
            throw IconError.encodeFailed
        }
        for size in icnsSizes {
            guard let image = render(base: base, badge: badge, size: size) else { throw IconError.renderFailed(size) }
            CGImageDestinationAddImage(dest, image, nil)
        }
        guard CGImageDestinationFinalize(dest) else { throw IconError.encodeFailed }
        return data as Data
    }

    public static func color(fromHex hex: String) -> CGColor {
        let digits = hex.hasPrefix("#") ? String(hex.dropFirst()) : hex
        guard digits.count == 6, let v = UInt32(digits, radix: 16) else {
            return color(fromHex: Badge.defaultColor)
        }
        return CGColor(srgbRed: CGFloat((v >> 16) & 0xFF) / 255, green: CGFloat((v >> 8) & 0xFF) / 255,
                       blue: CGFloat(v & 0xFF) / 255, alpha: 1)
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `swift test --filter IconRendererTests`
Expected: PASS. If the compiler reports that `NSWorkspace.shared` is main-actor isolated, mark `baseIcon(forApp:)` as `@MainActor`, mark the two tests that call it `@MainActor`, and in Task 14 call it via `MainActor.assumeIsolated` only from main-thread contexts — record the change in the commit message.

- [ ] **Step 5: Commit**

```bash
git add Sources/MitosisCore/IconRenderer.swift Tests/MitosisCoreTests/IconRendererTests.swift
git commit -m "feat(core): badge icon renderer

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 12: `Signer`

**Files:**
- Create: `Sources/MitosisCore/Signer.swift`
- Test: `Tests/MitosisCoreTests/SignerTests.swift`

**Interfaces:**
- Consumes: `Shell.run`, `Entitlements.xmlData`, `InfoPlistEditor.read`.
- Produces: `Signer.signInsideOut(bundle: URL, entitlements: [String: Any], identifier: String, extraExecutables: [URL] = []) throws`; `Signer.verify(_ bundle: URL) throws`; `Signer.nestedCodeItems(in bundle: URL, excluding: [URL]) throws -> [URL]` (internal, deepest first).

- [ ] **Step 1: Write the failing test**

`Tests/MitosisCoreTests/SignerTests.swift`:
```swift
import Foundation
import Testing
@testable import MitosisCore

@Suite struct SignerTests {
    static func electronLikeApp(in dir: URL) throws -> URL {
        var o = FixtureOptions()
        o.name = "Electronish"
        o.frameworks = ["Electron Framework"]
        o.helperApps = ["Electronish Helper"]
        o.nativeModule = true
        o.entitlements = ["com.apple.security.app-sandbox": true, "aps-environment": "production"]
        return try FixtureFactory.makeApp(in: dir, o)
    }

    @Test func nestedItemsAreDeepestFirstAndExcludeMainExecutable() throws {
        let dir = try TestSupport.tempDir()
        let app = try Self.electronLikeApp(in: dir)
        let items = try Signer.nestedCodeItems(in: app, excluding: [app.appendingPathComponent("Contents/MacOS/Fixture")])
        let names = items.map(\.lastPathComponent)
        #expect(!names.contains("Fixture"))
        #expect(names.contains("addon.node"))
        let lib = try #require(names.firstIndex(of: "libextra.dylib"))
        let framework = try #require(names.firstIndex(of: "Electron Framework.framework"))
        #expect(lib < framework)   // inner code before its container
        let helperExe = try #require(names.firstIndex(of: "Electronish Helper"))
        let helperApp = try #require(names.firstIndex(of: "Electronish Helper.app"))
        #expect(helperExe < helperApp)
    }

    @Test func resignsModifiedBundleWithSanitizedEntitlements() throws {
        let dir = try TestSupport.tempDir()
        let app = try Self.electronLikeApp(in: dir)
        let plist = app.appendingPathComponent("Contents/Info.plist")
        var info = try InfoPlistEditor.read(plist)
        info["CFBundleIdentifier"] = "com.example.fixture.mitosis.work"
        try InfoPlistEditor.write(info, to: plist)
        #expect(throws: ShellError.self) { try Signer.verify(app) }   // modification broke the seal

        try Signer.signInsideOut(bundle: app, entitlements: ["com.apple.security.app-sandbox": true],
                                 identifier: "com.example.fixture.mitosis.work")
        try Signer.verify(app)
        let ents = try Entitlements.read(from: app)
        #expect(Set(ents.keys) == ["com.apple.security.app-sandbox"])
    }

    @Test func signsExtraExecutablesWithIdentifier() throws {
        let dir = try TestSupport.tempDir()
        var o = FixtureOptions(); o.signed = false
        let app = try FixtureFactory.makeApp(in: dir, o)
        let macOS = app.appendingPathComponent("Contents/MacOS")
        let real = macOS.appendingPathComponent("Fixture.mitosis-real")
        try FileManager.default.moveItem(at: macOS.appendingPathComponent("Fixture"), to: real)
        try FileManager.default.copyItem(at: TestSupport.stubBinary, to: macOS.appendingPathComponent("Fixture"))
        try Signer.signInsideOut(bundle: app, entitlements: [:], identifier: "com.example.x", extraExecutables: [real])
        try Signer.verify(app)
        let r = try Shell.run("/usr/bin/codesign", ["-dv", real.path], check: false)
        #expect(r.stderr.contains("Identifier=com.example.x"))
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `swift test --filter SignerTests`
Expected: FAIL to compile — `cannot find 'Signer' in scope`.

- [ ] **Step 3: Implement**

`Sources/MitosisCore/Signer.swift`:
```swift
import Foundation

public enum Signer {
    static let codesign = "/usr/bin/codesign"
    static let bundleExtensions: Set<String> = ["framework", "app", "xpc", "appex", "bundle", "plugin"]
    static let codeFileExtensions: Set<String> = ["dylib", "node", "so"]

    /// Ad-hoc signs all nested code deepest-first, then `extraExecutables` (with entitlements + identifier),
    /// then the bundle itself (with entitlements). No hardened runtime.
    public static func signInsideOut(bundle: URL, entitlements: [String: Any], identifier: String,
                                     extraExecutables: [URL] = []) throws {
        let info = try InfoPlistEditor.read(bundle.appendingPathComponent("Contents/Info.plist"))
        var excluded = extraExecutables
        if let exe = info["CFBundleExecutable"] as? String {
            excluded.append(bundle.appendingPathComponent("Contents/MacOS").appendingPathComponent(exe))
        }
        var entArgs: [String] = []
        var entFile: URL?
        if !entitlements.isEmpty {
            let f = FileManager.default.temporaryDirectory.appendingPathComponent("mitosis-\(UUID().uuidString).entitlements")
            try Entitlements.xmlData(entitlements).write(to: f)
            entArgs = ["--entitlements", f.path]
            entFile = f
        }
        defer { if let entFile { try? FileManager.default.removeItem(at: entFile) } }

        let base = ["--force", "--sign", "-", "--timestamp=none"]
        for item in try nestedCodeItems(in: bundle, excluding: excluded) {
            try Shell.run(codesign, base + [item.path])
        }
        for exe in extraExecutables {
            try Shell.run(codesign, base + ["--identifier", identifier] + entArgs + [exe.path])
        }
        try Shell.run(codesign, base + ["--identifier", identifier] + entArgs + [bundle.path])
    }

    public static func verify(_ bundle: URL) throws {
        try Shell.run(codesign, ["--verify", "--deep", "--strict", bundle.path])
    }

    static func nestedCodeItems(in bundle: URL, excluding: [URL]) throws -> [URL] {
        let excludedPaths = Set(excluding.map { $0.resolvingSymlinksInPath().path })
        let contents = bundle.appendingPathComponent("Contents")
        let keys: [URLResourceKey] = [.isDirectoryKey, .isSymbolicLinkKey, .isRegularFileKey, .isExecutableKey]
        guard let e = FileManager.default.enumerator(at: contents, includingPropertiesForKeys: keys) else { return [] }
        var items: [URL] = []
        for case let url as URL in e {
            let v = try url.resourceValues(forKeys: Set(keys))
            if v.isSymbolicLink == true { continue }
            if excludedPaths.contains(url.resolvingSymlinksInPath().path) { continue }
            if v.isDirectory == true {
                if bundleExtensions.contains(url.pathExtension) { items.append(url) }
            } else if v.isRegularFile == true,
                      v.isExecutable == true || codeFileExtensions.contains(url.pathExtension),
                      isMachO(url) {
                items.append(url)
            }
        }
        return items.sorted { $0.pathComponents.count > $1.pathComponents.count }
    }

    static func isMachO(_ url: URL) -> Bool {
        guard let h = try? FileHandle(forReadingFrom: url) else { return false }
        defer { try? h.close() }
        guard let d = try? h.read(upToCount: 4), d.count == 4 else { return false }
        let magic = d.withUnsafeBytes { $0.load(as: UInt32.self) }
        return [0xFEEDFACE, 0xFEEDFACF, 0xCEFAEDFE, 0xCFFAEDFE, 0xCAFEBABE, 0xBEBAFECA].contains(magic)
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `swift test --filter SignerTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Sources/MitosisCore/Signer.swift Tests/MitosisCoreTests/SignerTests.swift
git commit -m "feat(core): inside-out ad-hoc signer

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 13: `CloneRegistry` and `Trash`

**Files:**
- Create: `Sources/MitosisCore/CloneRegistry.swift`
- Test: `Tests/MitosisCoreTests/CloneRegistryTests.swift`

**Interfaces:**
- Consumes: `RegistryEntry`, `CloneManifest.encoder()/decoder()`.
- Produces: `CloneRegistry(fileURL: URL)`, `load() throws -> [RegistryEntry]` (missing file → `[]`), `save(_:) throws`, `upsert(_:) throws`, `remove(id: UUID) throws`, `rebuild(scanning clonesDir: URL) throws -> [RegistryEntry]`, `loadOrRebuild(clonesDir: URL) throws -> [RegistryEntry]`, `CloneRegistry.manifestFileName = "mitosis.json"`, `CloneRegistry.embeddedManifest(in bundle: URL) -> CloneManifest?`; `Trash.move(_ url: URL, overrideDir: URL?) throws -> URL?`.

- [ ] **Step 1: Write the failing test**

`Tests/MitosisCoreTests/CloneRegistryTests.swift`:
```swift
import Foundation
import Testing
@testable import MitosisCore

@Suite struct CloneRegistryTests {
    static func entry(name: String, in dir: URL) -> RegistryEntry {
        var m = ModelsTests.sampleManifest()
        m.id = UUID()
        m.name = name
        return RegistryEntry(manifest: m, bundlePath: dir.appendingPathComponent("\(name).app").path)
    }

    @Test func missingFileLoadsEmpty() throws {
        let dir = try TestSupport.tempDir()
        #expect(try CloneRegistry(fileURL: dir.appendingPathComponent("clones.json")).load().isEmpty)
    }

    @Test func upsertRemoveAndPersist() throws {
        let dir = try TestSupport.tempDir()
        let reg = CloneRegistry(fileURL: dir.appendingPathComponent("sub/clones.json"))
        var a = Self.entry(name: "A", in: dir)
        let b = Self.entry(name: "B", in: dir)
        try reg.upsert(a)
        try reg.upsert(b)
        a.manifest.name = "A renamed"
        try reg.upsert(a)
        #expect(try reg.load().map(\.manifest.name).sorted() == ["A renamed", "B"])
        try reg.remove(id: b.id)
        #expect(try reg.load().map(\.id) == [a.id])
    }

    @Test func corruptFileIsRebuiltFromEmbeddedManifests() throws {
        let dir = try TestSupport.tempDir()
        let clones = dir.appendingPathComponent("Clones")
        let e = Self.entry(name: "Rebuilt", in: clones)
        let resources = e.bundleURL.appendingPathComponent("Contents/Resources")
        try FileManager.default.createDirectory(at: resources, withIntermediateDirectories: true)
        try CloneManifest.encoder().encode(e.manifest).write(to: resources.appendingPathComponent(CloneRegistry.manifestFileName))
        try FileManager.default.createDirectory(at: clones.appendingPathComponent(".mitosis-tmp-x.app/Contents"), withIntermediateDirectories: true)

        let file = dir.appendingPathComponent("clones.json")
        try Data("not json".utf8).write(to: file)
        let reg = CloneRegistry(fileURL: file)
        let loaded = try reg.loadOrRebuild(clonesDir: clones)
        #expect(loaded.map(\.id) == [e.id])
        #expect(loaded.first?.bundlePath == e.bundleURL.path)
        #expect(try reg.load().map(\.id) == [e.id])   // rewritten
    }

    @Test func trashOverrideMovesItem() throws {
        let dir = try TestSupport.tempDir()
        let f = dir.appendingPathComponent("thing.txt")
        try Data("x".utf8).write(to: f)
        let moved = try #require(try Trash.move(f, overrideDir: dir.appendingPathComponent("Trash")))
        #expect(!FileManager.default.fileExists(atPath: f.path))
        #expect(FileManager.default.fileExists(atPath: moved.path))
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `swift test --filter CloneRegistryTests`
Expected: FAIL to compile — `cannot find 'CloneRegistry' in scope`.

- [ ] **Step 3: Implement**

`Sources/MitosisCore/CloneRegistry.swift`:
```swift
import Foundation

public struct CloneRegistry: Sendable {
    public static let manifestFileName = "mitosis.json"
    public let fileURL: URL

    public init(fileURL: URL) {
        self.fileURL = fileURL
    }

    public func load() throws -> [RegistryEntry] {
        guard FileManager.default.fileExists(atPath: fileURL.path) else { return [] }
        return try CloneManifest.decoder().decode([RegistryEntry].self, from: Data(contentsOf: fileURL))
    }

    public func save(_ entries: [RegistryEntry]) throws {
        try FileManager.default.createDirectory(at: fileURL.deletingLastPathComponent(), withIntermediateDirectories: true)
        try CloneManifest.encoder().encode(entries).write(to: fileURL, options: .atomic)
    }

    public func upsert(_ entry: RegistryEntry) throws {
        var entries = try load()
        if let i = entries.firstIndex(where: { $0.id == entry.id }) { entries[i] = entry } else { entries.append(entry) }
        try save(entries)
    }

    public func remove(id: UUID) throws {
        try save(try load().filter { $0.id != id })
    }

    public func rebuild(scanning clonesDir: URL) throws -> [RegistryEntry] {
        let names = (try? FileManager.default.contentsOfDirectory(atPath: clonesDir.path)) ?? []
        var entries: [RegistryEntry] = []
        for name in names.sorted() where name.hasSuffix(".app") && !name.hasPrefix(".") {
            let bundle = clonesDir.appendingPathComponent(name)
            if let m = Self.embeddedManifest(in: bundle) {
                entries.append(RegistryEntry(manifest: m, bundlePath: bundle.path))
            }
        }
        try save(entries)
        return entries
    }

    public func loadOrRebuild(clonesDir: URL) throws -> [RegistryEntry] {
        do { return try load() } catch { return try rebuild(scanning: clonesDir) }
    }

    public static func embeddedManifest(in bundle: URL) -> CloneManifest? {
        let url = bundle.appendingPathComponent("Contents/Resources").appendingPathComponent(manifestFileName)
        guard let data = try? Data(contentsOf: url) else { return nil }
        return try? CloneManifest.decoder().decode(CloneManifest.self, from: data)
    }
}

public enum Trash {
    /// Moves to the user's Trash, or into `overrideDir` (tests / MITOSIS_TRASH_DIR). Never deletes permanently.
    @discardableResult
    public static func move(_ url: URL, overrideDir: URL?) throws -> URL? {
        let fm = FileManager.default
        if let overrideDir {
            try fm.createDirectory(at: overrideDir, withIntermediateDirectories: true)
            let dest = overrideDir.appendingPathComponent("\(UUID().uuidString)-\(url.lastPathComponent)")
            try fm.moveItem(at: url, to: dest)
            return dest
        }
        var result: NSURL?
        try fm.trashItem(at: url, resultingItemURL: &result)
        return result as URL?
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `swift test --filter CloneRegistryTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Sources/MitosisCore/CloneRegistry.swift Tests/MitosisCoreTests/CloneRegistryTests.swift
git commit -m "feat(core): clone registry with rebuild, and trash helper

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 14: `CloneBuilder` — identity mode, environment, rollback

**Files:**
- Create: `Sources/MitosisCore/MitosisEnvironment.swift`
- Create: `Sources/MitosisCore/CloneBuilder.swift`
- Test: `Tests/MitosisCoreTests/CloneBuilderIdentityTests.swift`
- Modify: `Tests/MitosisCoreTests/Support/TestSupport.swift` (add `environment(root:)`)

**Interfaces:**
- Consumes: everything from Tasks 2–13.
- Produces: `MitosisEnvironment` (`clonesDir`, `dataRoot`, `registryFile`, `stubBinary`, `trashOverride: URL?`, `registerWithLaunchServices: Bool`, `standard(stubBinary:root:)`); `LaunchServices.register(_:)`, `.unregister(_:)`; `CloneRequest(source:name:badge:modeOverride:)`; `CloneError` (`invalidName(CloneNameError)`, `unsupported([String])`, `nameTaken(String)`, `sourceMissing(String)`, `cloneRunning(String)`, `notFound(String)`, `relinkMismatch(expected:found:)`, `failed(step:message:)`) with friendly `description`; `CloneBuilder(environment:profiles:)`, `create(_:) throws -> RegistryEntry`, internal `BuildPlan`, `install(_:replacing:) throws -> URL`, `launchSettings(for:mode:profile:) -> LaunchSettings`, test hook `failAfterStep: String?`.

- [ ] **Step 1: Add the test environment helper**

Append to `Tests/MitosisCoreTests/Support/TestSupport.swift` inside `enum TestSupport`:
```swift
    static func environment(root: URL) -> MitosisEnvironment {
        var env = MitosisEnvironment.standard(stubBinary: stubBinary, root: root)
        env.trashOverride = root.appendingPathComponent("Trash")
        env.registerWithLaunchServices = false
        return env
    }
```
and add `@testable import MitosisCore` at the top of that file.

- [ ] **Step 2: Write the failing test**

`Tests/MitosisCoreTests/CloneBuilderIdentityTests.swift`:
```swift
import Foundation
import Testing
@testable import MitosisCore

@Suite struct CloneBuilderIdentityTests {
    static func setUp(_ options: FixtureOptions = {
        var o = FixtureOptions()
        o.frameworks = ["Electron Framework"]
        o.helperApps = ["Fixture Helper"]
        o.entitlements = ["com.apple.security.app-sandbox": true]
        return o
    }()) throws -> (root: URL, source: URL, builder: CloneBuilder) {
        let root = try TestSupport.tempDir()
        let apps = root.appendingPathComponent("Applications")
        try FileManager.default.createDirectory(at: apps, withIntermediateDirectories: true)
        let source = try FixtureFactory.makeApp(in: apps, options)
        let builder = CloneBuilder(environment: TestSupport.environment(root: root), profiles: try ProfileStore(profiles: []))
        return (root, source, builder)
    }

    @Test func createsASignedIdentityCloneWithItsOwnID() throws {
        let (root, source, builder) = try Self.setUp()
        let sourceHash = AppInspector.cdhash(of: source)
        let entry = try builder.create(CloneRequest(source: source, name: "Fixture Work", badge: Badge(text: "W", color: "#0A84FF")))

        let clone = root.appendingPathComponent("Clones/Fixture Work.app")
        #expect(entry.bundleURL.path == clone.path)
        let info = try InfoPlistEditor.read(clone.appendingPathComponent("Contents/Info.plist"))
        #expect(info["CFBundleIdentifier"] as? String == "com.example.fixture.mitosis.fixture-work")
        #expect(info["CFBundleName"] as? String == "Fixture Work")
        #expect(info["CFBundleIconFile"] as? String == "MitosisIcon")
        #expect(FileManager.default.fileExists(atPath: clone.appendingPathComponent("Contents/Resources/MitosisIcon.icns").path))
        #expect(FileManager.default.fileExists(atPath: clone.appendingPathComponent("Contents/MacOS/Fixture.mitosis-real").path))

        let launchData = try Data(contentsOf: clone.appendingPathComponent("Contents/Resources/mitosis-launch.json"))
        let launch = try JSONDecoder().decode(LaunchConfig.self, from: launchData)
        #expect(launch.kind == .exec)
        #expect(launch.target == "Fixture.mitosis-real")
        #expect(launch.args == ["--user-data-dir=\(entry.manifest.dataPath)"])
        #expect(FileManager.default.fileExists(atPath: entry.manifest.dataPath))

        #expect(CloneRegistry.embeddedManifest(in: clone)?.id == entry.id)
        #expect(entry.manifest.mode == .identity)
        try Signer.verify(clone)
        #expect(Set(try Entitlements.read(from: clone).keys) == ["com.apple.security.app-sandbox"])
        #expect(try CloneRegistry(fileURL: root.appendingPathComponent("clones.json")).load().map(\.id) == [entry.id])
        #expect(AppInspector.cdhash(of: source) == sourceHash)   // original untouched
    }

    @Test func plainNativeAppNeedsNoStub() throws {
        var o = FixtureOptions()
        o.name = "Native"
        let (root, source, builder) = try Self.setUp(o)
        _ = try builder.create(CloneRequest(source: source, name: "Native 2", badge: Badge(text: "2", color: "#FF9F0A")))
        let clone = root.appendingPathComponent("Clones/Native 2.app")
        #expect(!FileManager.default.fileExists(atPath: clone.appendingPathComponent("Contents/MacOS/Fixture.mitosis-real").path))
        #expect(!FileManager.default.fileExists(atPath: clone.appendingPathComponent("Contents/Resources/mitosis-launch.json").path))
        try Signer.verify(clone)
    }

    @Test func duplicateNameIsRejectedAndSlugIsDeduplicated() throws {
        let (_, source, builder) = try Self.setUp()
        _ = try builder.create(CloneRequest(source: source, name: "Fixture Work", badge: Badge(text: "W", color: "#0A84FF")))
        let err = #expect(throws: CloneError.self) {
            try builder.create(CloneRequest(source: source, name: "Fixture Work", badge: Badge(text: "W", color: "#0A84FF")))
        }
        #expect(err?.description == "A clone named \"Fixture Work\" already exists.")
        let second = try builder.create(CloneRequest(source: source, name: "Fixture-Work", badge: Badge(text: "W", color: "#0A84FF")))
        #expect(second.manifest.cloneBundleID == "com.example.fixture.mitosis.fixture-work-2")
    }

    @Test func failureRollsBackEverything() throws {
        let (root, source, base) = try Self.setUp()
        var builder = base
        builder.failAfterStep = "sign"
        let err = #expect(throws: CloneError.self) {
            try builder.create(CloneRequest(source: source, name: "Broken", badge: Badge(text: "B", color: "#FF453A")))
        }
        #expect(err == .failed(step: "sign", message: "injected test failure"))
        let leftovers = (try? FileManager.default.contentsOfDirectory(atPath: root.appendingPathComponent("Clones").path)) ?? []
        #expect(leftovers.isEmpty)
        #expect(((try? FileManager.default.contentsOfDirectory(atPath: root.appendingPathComponent("Data").path)) ?? []).isEmpty)
        #expect(try CloneRegistry(fileURL: root.appendingPathComponent("clones.json")).load().isEmpty)
    }

    // Review Focus: invalid names, cloning a clone, unicode paths.
    @Test func rejectsInvalidNameAndCloneOfClone() throws {
        let (_, source, builder) = try Self.setUp()
        #expect(throws: CloneError.invalidName(.invalidCharacter("/"))) {
            try builder.create(CloneRequest(source: source, name: "a/b", badge: Badge(text: "A", color: "#0A84FF")))
        }
        let clone = try builder.create(CloneRequest(source: source, name: "First", badge: Badge(text: "F", color: "#0A84FF")))
        let err = #expect(throws: CloneError.self) {
            try builder.create(CloneRequest(source: clone.bundleURL, name: "Second", badge: Badge(text: "S", color: "#0A84FF")))
        }
        #expect(err == .unsupported(["This is already a Mitosis clone. Clone the original app instead."]))
    }

    @Test func unicodeNamesAndPathsWork() throws {
        var o = FixtureOptions()
        o.name = "Ünïcode Äpp"
        let (root, source, builder) = try Self.setUp(o)
        let e = try builder.create(CloneRequest(source: source, name: "Ünïcode Wörk", badge: Badge(text: "Ü", color: "#BF5AF2")))
        #expect(e.manifest.cloneBundleID == "com.example.fixture.mitosis.unicode-work")
        try Signer.verify(root.appendingPathComponent("Clones/Ünïcode Wörk.app"))
    }

    @Test func missingSourceIsReported() throws {
        let (root, _, builder) = try Self.setUp()
        let missing = root.appendingPathComponent("Nope.app")
        #expect(throws: CloneError.sourceMissing(missing.path)) {
            try builder.create(CloneRequest(source: missing, name: "X", badge: Badge(text: "X", color: "#0A84FF")))
        }
    }
}
```
(`CloneError` must be `Equatable` for these comparisons.)

- [ ] **Step 3: Run test to verify it fails**

Run: `swift test --filter CloneBuilderIdentityTests`
Expected: FAIL to compile — `cannot find 'CloneBuilder' in scope`.

- [ ] **Step 4: Implement the environment and LaunchServices helper**

`Sources/MitosisCore/MitosisEnvironment.swift`:
```swift
import Foundation

public struct MitosisEnvironment: Sendable {
    public var clonesDir: URL
    public var dataRoot: URL
    public var registryFile: URL
    public var stubBinary: URL
    /// When set, "Trash" moves go here instead of the user's Trash (tests, MITOSIS_TRASH_DIR).
    public var trashOverride: URL?
    public var registerWithLaunchServices: Bool

    public init(clonesDir: URL, dataRoot: URL, registryFile: URL, stubBinary: URL,
                trashOverride: URL? = nil, registerWithLaunchServices: Bool = true) {
        self.clonesDir = clonesDir
        self.dataRoot = dataRoot
        self.registryFile = registryFile
        self.stubBinary = stubBinary
        self.trashOverride = trashOverride
        self.registerWithLaunchServices = registerWithLaunchServices
    }

    /// Spec locations; with `root`, everything lives under that folder (tests, MITOSIS_HOME).
    public static func standard(stubBinary: URL, root: URL? = nil) -> MitosisEnvironment {
        if let root {
            return MitosisEnvironment(clonesDir: root.appendingPathComponent("Clones"),
                                      dataRoot: root.appendingPathComponent("Data"),
                                      registryFile: root.appendingPathComponent("clones.json"),
                                      stubBinary: stubBinary)
        }
        let home = FileManager.default.homeDirectoryForCurrentUser
        let support = home.appendingPathComponent("Library/Application Support/Mitosis")
        return MitosisEnvironment(clonesDir: home.appendingPathComponent("Applications/Mitosis"),
                                  dataRoot: support.appendingPathComponent("Data"),
                                  registryFile: support.appendingPathComponent("clones.json"),
                                  stubBinary: stubBinary)
    }
}

public enum LaunchServices {
    static let lsregister = "/System/Library/Frameworks/CoreServices.framework/Frameworks/LaunchServices.framework/Support/lsregister"

    public static func register(_ app: URL) throws {
        try Shell.run(lsregister, ["-f", app.path])
    }

    public static func unregister(_ app: URL) {
        _ = try? Shell.run(lsregister, ["-u", app.path], check: false)
    }
}
```

- [ ] **Step 5: Implement `CloneBuilder` (identity + fallback dispatch; fallback body in Task 15)**

`Sources/MitosisCore/CloneBuilder.swift`:
```swift
import Foundation

public struct CloneRequest: Sendable {
    public var source: URL
    public var name: String
    public var badge: Badge
    public var modeOverride: CloneMode?

    public init(source: URL, name: String, badge: Badge, modeOverride: CloneMode? = nil) {
        self.source = source
        self.name = name
        self.badge = badge
        self.modeOverride = modeOverride
    }
}

public enum CloneError: Error, Equatable, CustomStringConvertible, Sendable {
    case invalidName(CloneNameError)
    case unsupported([String])
    case nameTaken(String)
    case sourceMissing(String)
    case cloneRunning(String)
    case notFound(String)
    case relinkMismatch(expected: String, found: String)
    case failed(step: String, message: String)

    public var description: String {
        switch self {
        case .invalidName(let e): return e.description
        case .unsupported(let reasons): return "This app can't be cloned. " + reasons.joined(separator: " ")
        case .nameTaken(let n): return "A clone named \"\(n)\" already exists."
        case .sourceMissing(let p): return "The original app isn't at \(p) anymore."
        case .cloneRunning(let n): return "\"\(n)\" is running. Quit it first."
        case .notFound(let q): return "No clone matches \"\(q)\"."
        case let .relinkMismatch(expected, found): return "That app is \(found), but this clone was made from \(expected)."
        case let .failed(step, message): return "Cloning failed while \(Self.stepLabel(step)): \(message)"
        }
    }

    static func stepLabel(_ step: String) -> String {
        switch step {
        case "copy": return "copying the app"
        case "stub": return "setting up separate data"
        case "icon": return "drawing the icon"
        case "plist": return "giving the clone its own identity"
        case "manifest": return "saving clone details"
        case "sign": return "signing the clone"
        case "verify": return "checking the clone"
        default: return step
        }
    }
}

struct BuildPlan: Sendable {
    var info: AppInfo
    var mode: CloneMode
    var id: UUID
    var bundleID: String
    var name: String
    var badge: Badge
    var dataPath: String
    var launch: LaunchSettings
    var profile: CloneManifest.ProfileRef?
    var createdAt: Date
    var refreshedAt: Date

    var manifest: CloneManifest {
        CloneManifest(id: id, name: name, badge: badge, mode: mode, cloneBundleID: bundleID,
                      source: .init(path: info.url.path, bundleID: info.bundleID, version: info.version, cdhash: info.cdhash),
                      profile: profile, dataPath: dataPath, createdAt: createdAt, refreshedAt: refreshedAt)
    }
}

public struct CloneBuilder: Sendable {
    public let environment: MitosisEnvironment
    public let profiles: ProfileStore
    /// Test hook: throw right after the named step succeeds.
    var failAfterStep: String?

    public init(environment: MitosisEnvironment, profiles: ProfileStore) {
        self.environment = environment
        self.profiles = profiles
    }

    public func create(_ request: CloneRequest) throws -> RegistryEntry {
        let name: String
        do { name = try CloneName.validate(request.name) } catch let e as CloneNameError { throw CloneError.invalidName(e) }
        let fm = FileManager.default
        guard fm.fileExists(atPath: request.source.path) else { throw CloneError.sourceMissing(request.source.path) }

        let info = try AppInspector().inspect(request.source)
        let decision = ModeDecider(profiles: profiles).decide(for: info)
        guard decision.support != .unsupported, let decided = decision.mode else { throw CloneError.unsupported(decision.reasons) }
        let mode = request.modeOverride ?? decided

        let registry = CloneRegistry(fileURL: environment.registryFile)
        let entries = try registry.loadOrRebuild(clonesDir: environment.clonesDir)
        guard !fm.fileExists(atPath: environment.clonesDir.appendingPathComponent("\(name).app").path) else {
            throw CloneError.nameTaken(name)
        }
        let prefix = info.bundleID + ".mitosis."
        let usedSlugs = Set(entries.map(\.manifest.cloneBundleID).filter { $0.hasPrefix(prefix) }.map { String($0.dropFirst(prefix.count)) })
        let slug = Slug.make(from: name, existing: usedSlugs)

        let id = UUID()
        let dataURL = environment.dataRoot.appendingPathComponent(id.uuidString)
        let profile = profiles.profile(forBundleID: info.bundleID)
        let now = Date.mitosisNow
        let plan = BuildPlan(info: info, mode: mode, id: id, bundleID: prefix + slug, name: name, badge: request.badge,
                             dataPath: dataURL.path, launch: launchSettings(for: info, mode: mode, profile: profile),
                             profile: profile.map { .init(id: $0.id, version: $0.version) }, createdAt: now, refreshedAt: now)

        try fm.createDirectory(at: dataURL, withIntermediateDirectories: true)
        do {
            let url = try install(plan, replacing: nil)
            let entry = RegistryEntry(manifest: plan.manifest, bundlePath: url.path)
            try registry.upsert(entry)
            return entry
        } catch {
            try? fm.removeItem(at: dataURL)
            throw error
        }
    }

    func launchSettings(for info: AppInfo, mode: CloneMode, profile: AppProfile?) -> LaunchSettings {
        let settings = profile?.launch ?? DefaultLaunchSettings.forFrameworks(info.frameworks)
        if mode == .fallback && settings.isEmpty { return DefaultLaunchSettings.fallback }
        return settings
    }

    /// Builds into a hidden temp bundle next to the destination, verifies it, then moves (or replaces) atomically.
    func install(_ plan: BuildPlan, replacing existing: URL?) throws -> URL {
        let fm = FileManager.default
        try fm.createDirectory(at: environment.clonesDir, withIntermediateDirectories: true)
        let temp = environment.clonesDir.appendingPathComponent(".mitosis-tmp-\(UUID().uuidString).app")
        do {
            switch plan.mode {
            case .identity: try buildIdentity(plan, at: temp)
            case .fallback: try buildFallback(plan, at: temp)
            }
            try step("verify") { try Signer.verify(temp) }
            let final: URL
            if let existing {
                _ = try fm.replaceItemAt(existing, withItemAt: temp)
                final = existing
            } else {
                final = environment.clonesDir.appendingPathComponent("\(plan.name).app")
                try fm.moveItem(at: temp, to: final)
            }
            if environment.registerWithLaunchServices { try LaunchServices.register(final) }
            return final
        } catch {
            try? fm.removeItem(at: temp)
            throw error
        }
    }

    func step(_ name: String, _ body: () throws -> Void) throws {
        do {
            try body()
        } catch let e as CloneError {
            throw e
        } catch {
            throw CloneError.failed(step: name, message: String(describing: error))
        }
        if failAfterStep == name { throw CloneError.failed(step: name, message: "injected test failure") }
    }

    func buildIdentity(_ p: BuildPlan, at temp: URL) throws {
        let fm = FileManager.default
        let contents = temp.appendingPathComponent("Contents")
        let macOS = contents.appendingPathComponent("MacOS")
        let resources = contents.appendingPathComponent("Resources")
        var extraExecutables: [URL] = []

        try step("copy") {
            try FileCloner.cloneOrCopy(from: p.info.url, to: temp)
            try Shell.run("/bin/chmod", ["-R", "u+w", temp.path])
        }
        try step("stub") {
            guard !p.launch.isEmpty else { return }
            let exe = macOS.appendingPathComponent(p.info.executableName)
            let real = macOS.appendingPathComponent(p.info.executableName + ".mitosis-real")
            try fm.moveItem(at: exe, to: real)
            try fm.copyItem(at: environment.stubBinary, to: exe)
            try LaunchConfig.resolve(p.launch, kind: .exec, target: real.lastPathComponent, dataPath: p.dataPath)
                .write(toResources: resources)
            extraExecutables = [real]
        }
        try step("icon") { try writeIcon(p, resources: resources) }
        try step("plist") {
            try InfoPlistEditor.apply(InfoPlistEdit(bundleID: p.bundleID, name: p.name, iconFile: IconRenderer.iconFileBaseName,
                                                    disableSparkle: p.info.frameworks.contains(.sparkle)),
                                      to: contents.appendingPathComponent("Info.plist"))
        }
        try step("manifest") { try writeManifest(p, resources: resources) }
        try step("sign") {
            let entitlements = Entitlements.sanitized(try Entitlements.read(from: p.info.url))
            try Signer.signInsideOut(bundle: temp, entitlements: entitlements, identifier: p.bundleID,
                                     extraExecutables: extraExecutables)
        }
    }

    func buildFallback(_ p: BuildPlan, at temp: URL) throws {
        throw CloneError.failed(step: "copy", message: "fallback mode is implemented in Task 15")
    }

    func writeIcon(_ p: BuildPlan, resources: URL) throws {
        guard let base = IconRenderer.baseIcon(forApp: p.info.url) else { throw IconError.noBaseIcon(p.info.name) }
        try FileManager.default.createDirectory(at: resources, withIntermediateDirectories: true)
        try IconRenderer.icnsData(base: base, badge: p.badge)
            .write(to: resources.appendingPathComponent("\(IconRenderer.iconFileBaseName).icns"))
    }

    func writeManifest(_ p: BuildPlan, resources: URL) throws {
        try FileManager.default.createDirectory(at: resources, withIntermediateDirectories: true)
        try CloneManifest.encoder().encode(p.manifest)
            .write(to: resources.appendingPathComponent(CloneRegistry.manifestFileName))
    }
}
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `swift test --filter CloneBuilderIdentityTests`
Expected: PASS (7 tests).

- [ ] **Step 7: Commit**

```bash
git add Sources/MitosisCore/MitosisEnvironment.swift Sources/MitosisCore/CloneBuilder.swift Tests/MitosisCoreTests
git commit -m "feat(core): identity-mode clone builder with rollback

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 15: Fallback mode

**Files:**
- Modify: `Sources/MitosisCore/CloneBuilder.swift` (replace `buildFallback`)
- Test: `Tests/MitosisCoreTests/CloneBuilderFallbackTests.swift`

**Interfaces:**
- Consumes: `BuildPlan`, `LaunchConfig`, `Signer`, `InfoPlistEditor.write`, `writeIcon`, `writeManifest`, `step`.
- Produces: fallback bundles: executable `Contents/MacOS/MitosisShortcut` (LaunchStub copy), `LSUIElement = true`, `mitosis-launch.json` with `kind: open`, `target: <absolute source path>`.

- [ ] **Step 1: Write the failing test**

`Tests/MitosisCoreTests/CloneBuilderFallbackTests.swift`:
```swift
import Foundation
import Testing
@testable import MitosisCore

@Suite struct CloneBuilderFallbackTests {
    @Test func restrictedEntitlementsProduceAShortcutApp() throws {
        var o = FixtureOptions()
        o.entitlements = ["keychain-access-groups": ["ABCDE12345.com.example"]]
        let (root, source, builder) = try CloneBuilderIdentityTests.setUp(o)
        let entry = try builder.create(CloneRequest(source: source, name: "Fixture Safe", badge: Badge(text: "S", color: "#30D158")))

        #expect(entry.manifest.mode == .fallback)
        let clone = root.appendingPathComponent("Clones/Fixture Safe.app")
        let info = try InfoPlistEditor.read(clone.appendingPathComponent("Contents/Info.plist"))
        #expect(info["CFBundleIdentifier"] as? String == "com.example.fixture.mitosis.fixture-safe")
        #expect(info["CFBundleExecutable"] as? String == "MitosisShortcut")
        #expect(info["LSUIElement"] as? Bool == true)
        #expect(info["CFBundleIconFile"] as? String == "MitosisIcon")

        let launch = try JSONDecoder().decode(LaunchConfig.self, from: Data(contentsOf: clone.appendingPathComponent("Contents/Resources/mitosis-launch.json")))
        #expect(launch == LaunchConfig(kind: .open, target: source.path, args: [], env: ["HOME": entry.manifest.dataPath]))
        try Signer.verify(clone)
        #expect(try Entitlements.read(from: clone).isEmpty)
    }

    @Test func modeOverrideForcesFallback() throws {
        let (_, source, builder) = try CloneBuilderIdentityTests.setUp()
        let e = try builder.create(CloneRequest(source: source, name: "Forced", badge: Badge(text: "F", color: "#8E8E93"), modeOverride: .fallback))
        #expect(e.manifest.mode == .fallback)
        // Electron default args are kept for fallback when present (data separation via --user-data-dir).
        let launch = try JSONDecoder().decode(LaunchConfig.self, from: Data(contentsOf: e.bundleURL.appendingPathComponent("Contents/Resources/mitosis-launch.json")))
        #expect(launch.args == ["--user-data-dir=\(e.manifest.dataPath)"])
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `swift test --filter CloneBuilderFallbackTests`
Expected: FAIL — `Cloning failed while copying the app: fallback mode is implemented in Task 15`.

- [ ] **Step 3: Implement `buildFallback`**

In `Sources/MitosisCore/CloneBuilder.swift`, replace the placeholder `buildFallback` with:
```swift
    func buildFallback(_ p: BuildPlan, at temp: URL) throws {
        let fm = FileManager.default
        let contents = temp.appendingPathComponent("Contents")
        let macOS = contents.appendingPathComponent("MacOS")
        let resources = contents.appendingPathComponent("Resources")
        let executable = "MitosisShortcut"

        try step("copy") {
            try fm.createDirectory(at: macOS, withIntermediateDirectories: true)
            try fm.createDirectory(at: resources, withIntermediateDirectories: true)
            try fm.copyItem(at: environment.stubBinary, to: macOS.appendingPathComponent(executable))
        }
        try step("stub") {
            try LaunchConfig.resolve(p.launch, kind: .open, target: p.info.url.path, dataPath: p.dataPath)
                .write(toResources: resources)
        }
        try step("icon") { try writeIcon(p, resources: resources) }
        try step("plist") {
            try InfoPlistEditor.write([
                "CFBundleIdentifier": p.bundleID,
                "CFBundleName": p.name,
                "CFBundleDisplayName": p.name,
                "CFBundleExecutable": executable,
                "CFBundlePackageType": "APPL",
                "CFBundleInfoDictionaryVersion": "6.0",
                "CFBundleIconFile": IconRenderer.iconFileBaseName,
                "CFBundleShortVersionString": p.info.version,
                "CFBundleVersion": p.info.build,
                "LSMinimumSystemVersion": "15.0",
                "LSUIElement": true,
            ], to: contents.appendingPathComponent("Info.plist"))
        }
        try step("manifest") { try writeManifest(p, resources: resources) }
        try step("sign") { try Signer.signInsideOut(bundle: temp, entitlements: [:], identifier: p.bundleID) }
    }
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `swift test --filter CloneBuilder`
Expected: PASS (identity + fallback suites).

- [ ] **Step 5: Commit**

```bash
git add Sources/MitosisCore/CloneBuilder.swift Tests/MitosisCoreTests/CloneBuilderFallbackTests.swift
git commit -m "feat(core): fallback-mode shortcut clones

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 16: `CloneMaintenance` — status, refresh, delete, relink

**Files:**
- Create: `Sources/MitosisCore/CloneMaintenance.swift`
- Test: `Tests/MitosisCoreTests/CloneMaintenanceTests.swift`

**Interfaces:**
- Consumes: `CloneBuilder` (`environment`, `profiles`, `install`, `launchSettings`), `BuildPlan`, `CloneRegistry`, `Trash`, `LaunchServices`, `RunningMonitor.isRunning(bundleID:)` (Task 17 — define the default closure there; until then this task declares the closure type and tests always inject it).
- Produces: `CloneStatus` (`upToDate`, `updateAvailable(currentVersion: String)`, `originalMissing`); `CloneMaintenance(builder:isRunning:)`, `status(of:) -> CloneStatus`, `refresh(_:) throws -> RegistryEntry`, `delete(_:deleteData:) throws`, `relink(_:to:) throws -> RegistryEntry`.

- [ ] **Step 1: Write the failing test**

`Tests/MitosisCoreTests/CloneMaintenanceTests.swift`:
```swift
import Foundation
import Testing
@testable import MitosisCore

@Suite struct CloneMaintenanceTests {
    static func setUp() throws -> (root: URL, source: URL, builder: CloneBuilder, entry: RegistryEntry) {
        let (root, source, builder) = try CloneBuilderIdentityTests.setUp()
        let entry = try builder.create(CloneRequest(source: source, name: "Fixture Work", badge: Badge(text: "W", color: "#0A84FF")))
        return (root, source, builder, entry)
    }

    static func bumpVersion(of app: URL, to version: String) throws {
        let plist = app.appendingPathComponent("Contents/Info.plist")
        var info = try InfoPlistEditor.read(plist)
        info["CFBundleShortVersionString"] = version
        info["CFBundleVersion"] = version
        try InfoPlistEditor.write(info, to: plist)
        try Shell.run("/usr/bin/codesign", ["--force", "--sign", "-", app.path])
    }

    @Test func statusTracksSourceChanges() throws {
        let (_, source, builder, entry) = try Self.setUp()
        let m = CloneMaintenance(builder: builder, isRunning: { _ in false })
        #expect(m.status(of: entry) == .upToDate)
        try Self.bumpVersion(of: source, to: "2.0")
        #expect(m.status(of: entry) == .updateAvailable(currentVersion: "2.0"))
        try FileManager.default.removeItem(at: source)
        #expect(m.status(of: entry) == .originalMissing)
    }

    @Test func refreshKeepsIdentityAndData() throws {
        let (_, source, builder, entry) = try Self.setUp()
        let marker = URL(fileURLWithPath: entry.manifest.dataPath).appendingPathComponent("login.db")
        try Data("session".utf8).write(to: marker)
        try Self.bumpVersion(of: source, to: "2.0")

        let m = CloneMaintenance(builder: builder, isRunning: { _ in false })
        let refreshed = try m.refresh(entry)
        #expect(refreshed.id == entry.id)
        #expect(refreshed.manifest.cloneBundleID == entry.manifest.cloneBundleID)
        #expect(refreshed.manifest.dataPath == entry.manifest.dataPath)
        #expect(refreshed.manifest.source.version == "2.0")
        #expect(refreshed.manifest.createdAt == entry.manifest.createdAt)
        #expect(FileManager.default.fileExists(atPath: marker.path))
        #expect(m.status(of: refreshed) == .upToDate)
        try Signer.verify(refreshed.bundleURL)
        let names = try FileManager.default.contentsOfDirectory(atPath: builder.environment.clonesDir.path)
        #expect(names == ["Fixture Work.app"])   // no temp leftovers
    }

    // Review Focus: running clones are protected.
    @Test func refreshAndDeleteRefuseWhileRunning() throws {
        let (_, _, builder, entry) = try Self.setUp()
        let m = CloneMaintenance(builder: builder, isRunning: { _ in true })
        #expect(throws: CloneError.cloneRunning("Fixture Work")) { try m.refresh(entry) }
        #expect(throws: CloneError.cloneRunning("Fixture Work")) { try m.delete(entry, deleteData: true) }
        #expect(FileManager.default.fileExists(atPath: entry.bundlePath))
    }

    @Test func deleteMovesToTrashAndOptionallyData() throws {
        let (root, _, builder, entry) = try Self.setUp()
        let m = CloneMaintenance(builder: builder, isRunning: { _ in false })
        try m.delete(entry, deleteData: false)
        #expect(!FileManager.default.fileExists(atPath: entry.bundlePath))
        #expect(FileManager.default.fileExists(atPath: entry.manifest.dataPath))   // kept by default
        #expect(try CloneRegistry(fileURL: builder.environment.registryFile).load().isEmpty)
        let trashed = try FileManager.default.contentsOfDirectory(atPath: root.appendingPathComponent("Trash").path)
        #expect(trashed.count == 1)

        let second = try builder.create(CloneRequest(source: URL(fileURLWithPath: entry.manifest.source.path), name: "Again", badge: Badge(text: "A", color: "#0A84FF")))
        try m.delete(second, deleteData: true)
        #expect(!FileManager.default.fileExists(atPath: second.manifest.dataPath))
        #expect(try FileManager.default.contentsOfDirectory(atPath: root.appendingPathComponent("Trash").path).count == 3)
    }

    @Test func relinkRequiresSameBundleID() throws {
        let (root, source, builder, entry) = try Self.setUp()
        let m = CloneMaintenance(builder: builder, isRunning: { _ in false })
        let moved = root.appendingPathComponent("Moved.app")
        try FileManager.default.moveItem(at: source, to: moved)
        let relinked = try m.relink(entry, to: moved)
        #expect(relinked.manifest.source.path == moved.path)
        #expect(m.status(of: relinked) == .upToDate)

        var other = FixtureOptions(); other.name = "Other"; other.bundleID = "com.example.other"
        let otherApp = try FixtureFactory.makeApp(in: root, other)
        #expect(throws: CloneError.relinkMismatch(expected: "com.example.fixture", found: "com.example.other")) {
            try m.relink(entry, to: otherApp)
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `swift test --filter CloneMaintenanceTests`
Expected: FAIL to compile — `cannot find 'CloneMaintenance' in scope`.

- [ ] **Step 3: Implement**

`Sources/MitosisCore/CloneMaintenance.swift`:
```swift
import Foundation

public enum CloneStatus: Equatable, Sendable {
    case upToDate
    case updateAvailable(currentVersion: String)
    case originalMissing
}

public struct CloneMaintenance: Sendable {
    public let builder: CloneBuilder
    public let isRunning: @Sendable (String) -> Bool

    public init(builder: CloneBuilder, isRunning: @escaping @Sendable (String) -> Bool = RunningMonitor.isRunning(bundleID:)) {
        self.builder = builder
        self.isRunning = isRunning
    }

    private var registry: CloneRegistry { CloneRegistry(fileURL: builder.environment.registryFile) }

    public func status(of entry: RegistryEntry) -> CloneStatus {
        let source = URL(fileURLWithPath: entry.manifest.source.path)
        guard let info = try? AppInspector().inspect(source), info.bundleID == entry.manifest.source.bundleID else {
            return .originalMissing
        }
        if let old = entry.manifest.source.cdhash, let new = info.cdhash {
            return old == new ? .upToDate : .updateAvailable(currentVersion: info.version)
        }
        return info.version == entry.manifest.source.version ? .upToDate : .updateAvailable(currentVersion: info.version)
    }

    public func refresh(_ entry: RegistryEntry) throws -> RegistryEntry {
        let m = entry.manifest
        guard !isRunning(m.cloneBundleID) else { throw CloneError.cloneRunning(m.name) }
        let source = URL(fileURLWithPath: m.source.path)
        guard FileManager.default.fileExists(atPath: source.path) else { throw CloneError.sourceMissing(source.path) }
        let info = try AppInspector().inspect(source)
        let profile = builder.profiles.profile(forBundleID: info.bundleID)
        let plan = BuildPlan(info: info, mode: m.mode, id: m.id, bundleID: m.cloneBundleID, name: m.name, badge: m.badge,
                             dataPath: m.dataPath, launch: builder.launchSettings(for: info, mode: m.mode, profile: profile),
                             profile: profile.map { .init(id: $0.id, version: $0.version) },
                             createdAt: m.createdAt, refreshedAt: .mitosisNow)
        let url = try builder.install(plan, replacing: entry.bundleURL)
        let updated = RegistryEntry(manifest: plan.manifest, bundlePath: url.path)
        try registry.upsert(updated)
        return updated
    }

    public func delete(_ entry: RegistryEntry, deleteData: Bool) throws {
        let m = entry.manifest
        guard !isRunning(m.cloneBundleID) else { throw CloneError.cloneRunning(m.name) }
        let fm = FileManager.default
        let trashDir = builder.environment.trashOverride
        if builder.environment.registerWithLaunchServices { LaunchServices.unregister(entry.bundleURL) }
        if fm.fileExists(atPath: entry.bundlePath) { try Trash.move(entry.bundleURL, overrideDir: trashDir) }
        if deleteData {
            let home = fm.homeDirectoryForCurrentUser
            let candidates = [
                URL(fileURLWithPath: m.dataPath),
                home.appendingPathComponent("Library/Containers/\(m.cloneBundleID)"),
                home.appendingPathComponent("Library/Preferences/\(m.cloneBundleID).plist"),
            ]
            for url in candidates where fm.fileExists(atPath: url.path) {
                try Trash.move(url, overrideDir: trashDir)
            }
        }
        try registry.remove(id: entry.id)
    }

    public func relink(_ entry: RegistryEntry, to newSource: URL) throws -> RegistryEntry {
        let info = try AppInspector().inspect(newSource)
        guard info.bundleID == entry.manifest.source.bundleID else {
            throw CloneError.relinkMismatch(expected: entry.manifest.source.bundleID, found: info.bundleID)
        }
        var updated = entry
        updated.manifest.source.path = newSource.path
        try registry.upsert(updated)
        return updated
    }
}
```
Also create `Sources/MitosisCore/Runtime.swift` with the minimal `RunningMonitor` so this compiles (completed in Task 17):
```swift
import Foundation

public enum RunningMonitor {
    /// True if any process for this bundle ID has checked in with macOS (GUI apps).
    public static func isRunning(bundleID: String) -> Bool {
        guard let r = try? Shell.run("/usr/bin/lsappinfo", ["find", "bundleid=\(bundleID)"], check: false) else { return false }
        return !r.stdout.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `swift test --filter CloneMaintenanceTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Sources/MitosisCore/CloneMaintenance.swift Sources/MitosisCore/Runtime.swift Tests/MitosisCoreTests/CloneMaintenanceTests.swift
git commit -m "feat(core): clone status, refresh, delete and relink

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 17: Runtime — launching, running state, app resolution

**Files:**
- Modify: `Sources/MitosisCore/Runtime.swift`
- Test: `Tests/MitosisCoreTests/RuntimeTests.swift`

**Interfaces:**
- Consumes: `Shell`, `RunningMonitor.isRunning`, `AppInspector.readInfoPlist`.
- Produces: `CloneLauncher.open(_ app: URL) throws`; `CloneLauncher.openAndCheck(_ app: URL, bundleID: String, window: Duration = .seconds(10)) async throws -> Bool` (true = still running after window); `AppResolver.defaultSearchDirs: [URL]`; `AppResolver.resolve(_ query: String, searchDirs: [URL] = defaultSearchDirs) -> URL?` (path, then case-insensitive "<name>.app", then bundle ID).

- [ ] **Step 1: Write the failing test**

`Tests/MitosisCoreTests/RuntimeTests.swift`:
```swift
import Foundation
import Testing
@testable import MitosisCore

@Suite struct AppResolverTests {
    @Test func resolvesByPathNameAndBundleID() throws {
        let dir = try TestSupport.tempDir()
        var o = FixtureOptions(); o.name = "Slacky"; o.bundleID = "com.example.slacky"; o.signed = false
        let app = try FixtureFactory.makeApp(in: dir, o)
        #expect(AppResolver.resolve(app.path, searchDirs: [dir])?.path == app.path)
        #expect(AppResolver.resolve("slacky", searchDirs: [dir])?.path == app.path)
        #expect(AppResolver.resolve("Slacky.app", searchDirs: [dir])?.path == app.path)
        #expect(AppResolver.resolve("COM.example.slacky", searchDirs: [dir])?.path == app.path)
        #expect(AppResolver.resolve("nothing", searchDirs: [dir]) == nil)
        #expect(AppResolver.resolve(dir.appendingPathComponent("Missing.app").path, searchDirs: [dir]) == nil)
    }
}

/// Launches real (tiny) GUI apps through macOS. Skip with MITOSIS_SKIP_LAUNCH_TESTS=1 (e.g. in CI).
@Suite(.serialized, .enabled(if: ProcessInfo.processInfo.environment["MITOSIS_SKIP_LAUNCH_TESTS"] == nil))
struct LaunchTests {
    static func makeClone(behavior: FixtureBehavior, name: String) throws -> (RegistryEntry, URL) {
        let root = try TestSupport.tempDir()
        let apps = root.appendingPathComponent("Applications")
        try FileManager.default.createDirectory(at: apps, withIntermediateDirectories: true)
        var o = FixtureOptions(); o.behavior = behavior; o.bundleID = "com.example.launchfixture.\(UUID().uuidString.prefix(8).lowercased())"
        let source = try FixtureFactory.makeApp(in: apps, o)
        var env = TestSupport.environment(root: root)
        env.registerWithLaunchServices = true
        let builder = CloneBuilder(environment: env, profiles: try ProfileStore(profiles: []))
        return (try builder.create(CloneRequest(source: source, name: name, badge: Badge(text: "T", color: "#0A84FF"))), root)
    }

    @Test func runningCloneIsDetected() async throws {
        let (entry, _) = try Self.makeClone(behavior: .stayOpen(seconds: 20), name: "Stays Open")
        defer {
            _ = try? Shell.run("/usr/bin/pkill", ["-f", entry.bundlePath], check: false)
            LaunchServices.unregister(entry.bundleURL)
        }
        let alive = try await CloneLauncher.openAndCheck(entry.bundleURL, bundleID: entry.manifest.cloneBundleID, window: .seconds(3))
        #expect(alive)
        #expect(RunningMonitor.isRunning(bundleID: entry.manifest.cloneBundleID))
    }

    @Test func cloneThatExitsImmediatelyIsReported() async throws {
        let (entry, _) = try Self.makeClone(behavior: .exit(code: 1), name: "Exits")
        defer { LaunchServices.unregister(entry.bundleURL) }
        let alive = try await CloneLauncher.openAndCheck(entry.bundleURL, bundleID: entry.manifest.cloneBundleID, window: .seconds(3))
        #expect(!alive)
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `swift test --filter "AppResolverTests|LaunchTests"`
Expected: FAIL to compile — `cannot find 'AppResolver' in scope`.

- [ ] **Step 3: Implement (replace `Runtime.swift`)**

`Sources/MitosisCore/Runtime.swift`:
```swift
import Foundation

public enum RunningMonitor {
    /// True if any process for this bundle ID has checked in with macOS (GUI apps).
    /// Fallback-mode clones run as the original app, so they are never reported as running.
    public static func isRunning(bundleID: String) -> Bool {
        guard let r = try? Shell.run("/usr/bin/lsappinfo", ["find", "bundleid=\(bundleID)"], check: false) else { return false }
        return !r.stdout.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
    }
}

public enum CloneLauncher {
    public static func open(_ app: URL) throws {
        try Shell.run("/usr/bin/open", [app.path])
    }

    /// Opens the app, waits `window`, and reports whether it is still running (false = it quit or crashed).
    public static func openAndCheck(_ app: URL, bundleID: String, window: Duration = .seconds(10)) async throws -> Bool {
        try open(app)
        try await Task.sleep(for: window)
        return RunningMonitor.isRunning(bundleID: bundleID)
    }
}

public enum AppResolver {
    public static let defaultSearchDirs: [URL] = [
        URL(fileURLWithPath: "/Applications"),
        FileManager.default.homeDirectoryForCurrentUser.appendingPathComponent("Applications"),
        URL(fileURLWithPath: "/Applications/Setapp"),
    ]

    /// Accepts a path, an app name ("Slack" / "Slack.app"), or a bundle ID.
    public static func resolve(_ query: String, searchDirs: [URL] = defaultSearchDirs) -> URL? {
        let fm = FileManager.default
        if query.contains("/") {
            let url = URL(fileURLWithPath: (query as NSString).expandingTildeInPath)
            return fm.fileExists(atPath: url.path) ? url : nil
        }
        let lower = query.lowercased()
        let wanted = lower.hasSuffix(".app") ? lower : lower + ".app"
        for dir in searchDirs {
            let items = (try? fm.contentsOfDirectory(atPath: dir.path)) ?? []
            if let hit = items.first(where: { $0.lowercased() == wanted }) { return dir.appendingPathComponent(hit) }
        }
        for dir in searchDirs {
            let items = (try? fm.contentsOfDirectory(atPath: dir.path)) ?? []
            for item in items where item.hasSuffix(".app") {
                let url = dir.appendingPathComponent(item)
                if let info = try? AppInspector.readInfoPlist(url),
                   (info["CFBundleIdentifier"] as? String)?.lowercased() == lower {
                    return url
                }
            }
        }
        return nil
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `swift test --filter "AppResolverTests|LaunchTests"`
Expected: PASS. (Launch tests open two tiny invisible apps for a few seconds.)

- [ ] **Step 5: Commit**

```bash
git add Sources/MitosisCore/Runtime.swift Tests/MitosisCoreTests/RuntimeTests.swift
git commit -m "feat(core): launcher, running monitor and app resolver

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 18: `mitosis` CLI

**Files:**
- Modify: `Sources/mitosis/MitosisCLI.swift` (replace placeholder)
- Test: `Tests/MitosisCoreTests/CLITests.swift`

**Interfaces:**
- Consumes: `MitosisEnvironment.standard`, `ProfileStore.bundled`, `CloneBuilder`, `CloneMaintenance`, `CloneRegistry`, `AppResolver`, `AppInspector`, `ModeDecider`, `CloneLauncher`, `Badge.presets`.
- Produces: commands `list`, `doctor <app>`, `clone <app> --name <n> [--badge <t>] [--color <name|#hex>] [--mode auto|identity|fallback]`, `refresh <clone>|--all`, `delete <clone> [--delete-data]`, `open <clone>`, `--version`. Environment variables: `MITOSIS_HOME` (root folder override), `MITOSIS_STUB` (stub path; default: `LaunchStub` next to the `mitosis` binary), `MITOSIS_TRASH_DIR`, `MITOSIS_NO_LSREGISTER=1`.

- [ ] **Step 1: Write the failing test**

`Tests/MitosisCoreTests/CLITests.swift`:
```swift
import Foundation
import Testing
@testable import MitosisCore

@Suite struct CLITests {
    static func run(_ args: [String], root: URL) throws -> ShellResult {
        var env = ProcessInfo.processInfo.environment
        env["MITOSIS_HOME"] = root.path
        env["MITOSIS_TRASH_DIR"] = root.appendingPathComponent("Trash").path
        env["MITOSIS_NO_LSREGISTER"] = "1"
        env["MITOSIS_STUB"] = TestSupport.stubBinary.path
        return try Shell.run(TestSupport.cliBinary.path, args, environment: env, check: false)
    }

    @Test func versionFlag() throws {
        let r = try Self.run(["--version"], root: try TestSupport.tempDir())
        #expect(r.stdout.trimmingCharacters(in: .whitespacesAndNewlines) == "0.1.0")
    }

    @Test func doctorCloneListDeleteFlow() throws {
        let root = try TestSupport.tempDir()
        var o = FixtureOptions(); o.frameworks = ["Electron Framework"]
        let app = try FixtureFactory.makeApp(in: root, o)

        let doctor = try Self.run(["doctor", app.path], root: root)
        #expect(doctor.status == 0)
        #expect(doctor.stdout.contains("Fixture 1.0 (com.example.fixture)"))
        #expect(doctor.stdout.contains("Mode: identity · Support: full"))
        #expect(doctor.stdout.contains("Frameworks: electron"))

        let clone = try Self.run(["clone", app.path, "--name", "Fixture Work", "--color", "green"], root: root)
        #expect(clone.status == 0, "\(clone.stderr)")
        #expect(clone.stdout.contains("Created \"Fixture Work\""))
        let entries = try CloneRegistry(fileURL: root.appendingPathComponent("clones.json")).load()
        #expect(entries.first?.manifest.badge == Badge(text: "F", color: "#30D158"))

        let list = try Self.run(["list"], root: root)
        #expect(list.stdout.contains("Fixture Work\tcom.example.fixture\tidentity\tok"))

        let delete = try Self.run(["delete", "fixture work", "--delete-data"], root: root)
        #expect(delete.status == 0, "\(delete.stderr)")
        #expect(try Self.run(["list"], root: root).stdout.contains("No clones yet"))
    }

    @Test func friendlyErrors() throws {
        let root = try TestSupport.tempDir()
        let app = try FixtureFactory.makeApp(in: root)
        let bad = try Self.run(["clone", app.path, "--name", "a/b"], root: root)
        #expect(bad.status != 0)
        #expect(bad.stderr.contains("Clone names can't contain \"/\"."))
        let missing = try Self.run(["clone", "DefinitelyNotAnApp", "--name", "X"], root: root)
        #expect(missing.status != 0)
        #expect(missing.stderr.contains("Couldn't find an app called \"DefinitelyNotAnApp\"."))
        let noClone = try Self.run(["delete", "ghost"], root: root)
        #expect(noClone.stderr.contains("No clone matches \"ghost\"."))
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `swift test --filter CLITests`
Expected: FAIL — `versionFlag` prints `mitosis placeholder`, other assertions fail.

- [ ] **Step 3: Implement the CLI**

`Sources/mitosis/MitosisCLI.swift` (replace the whole file):
```swift
import ArgumentParser
import Foundation
import MitosisCore

@main
struct MitosisCLI: ParsableCommand {
    static let configuration = CommandConfiguration(
        commandName: "mitosis",
        abstract: "Run separate copies of your Mac apps.",
        version: Mitosis.version,
        subcommands: [List.self, Doctor.self, Clone.self, Refresh.self, Delete.self, Open.self]
    )
}

struct CLIError: Error, CustomStringConvertible {
    let description: String
}

enum Context {
    static func environment() -> MitosisEnvironment {
        let vars = ProcessInfo.processInfo.environment
        let exeDir = (Bundle.main.executableURL ?? URL(fileURLWithPath: CommandLine.arguments[0]))
            .resolvingSymlinksInPath().deletingLastPathComponent()
        let stub = vars["MITOSIS_STUB"].map { URL(fileURLWithPath: $0) } ?? exeDir.appendingPathComponent("LaunchStub")
        var env = MitosisEnvironment.standard(stubBinary: stub, root: vars["MITOSIS_HOME"].map { URL(fileURLWithPath: $0) })
        if let trash = vars["MITOSIS_TRASH_DIR"] { env.trashOverride = URL(fileURLWithPath: trash) }
        if vars["MITOSIS_NO_LSREGISTER"] == "1" { env.registerWithLaunchServices = false }
        return env
    }

    static func builder() throws -> CloneBuilder {
        CloneBuilder(environment: environment(), profiles: try ProfileStore.bundled())
    }

    static func entries() throws -> [RegistryEntry] {
        let env = environment()
        return try CloneRegistry(fileURL: env.registryFile).loadOrRebuild(clonesDir: env.clonesDir)
    }

    static func findClone(_ query: String) throws -> RegistryEntry {
        let all = try entries()
        if let hit = all.first(where: { $0.manifest.name.lowercased() == query.lowercased() }) { return hit }
        if query.count >= 4, let hit = all.first(where: { $0.id.uuidString.lowercased().hasPrefix(query.lowercased()) }) { return hit }
        throw CLIError(description: CloneError.notFound(query).description)
    }

    static func resolveApp(_ query: String) throws -> URL {
        guard let url = AppResolver.resolve(query) else {
            throw CLIError(description: "Couldn't find an app called \"\(query)\".")
        }
        return url
    }

    static func describe(_ status: CloneStatus) -> String {
        switch status {
        case .upToDate: return "ok"
        case .updateAvailable(let v): return "update available (\(v))"
        case .originalMissing: return "original missing"
        }
    }
}

/// Runs a throwing body and turns any error into a clean one-line message.
func friendly<T>(_ body: () throws -> T) throws -> T {
    do { return try body() } catch let e as CLIError { throw e } catch { throw CLIError(description: String(describing: error)) }
}

struct List: ParsableCommand {
    static let configuration = CommandConfiguration(abstract: "List your clones.")

    func run() throws {
        try friendly {
            let entries = try Context.entries()
            guard !entries.isEmpty else {
                print("No clones yet. Try: mitosis clone Slack --name Work")
                return
            }
            let maintenance = CloneMaintenance(builder: try Context.builder())
            for e in entries.sorted(by: { $0.manifest.name.localizedStandardCompare($1.manifest.name) == .orderedAscending }) {
                print("\(e.manifest.name)\t\(e.manifest.source.bundleID)\t\(e.manifest.mode.rawValue)\t\(Context.describe(maintenance.status(of: e)))")
            }
        }
    }
}

struct Doctor: ParsableCommand {
    static let configuration = CommandConfiguration(abstract: "Check whether an app can be cloned and how.")
    @Argument(help: "App name, bundle ID, or path.") var app: String

    func run() throws {
        try friendly {
            let url = try Context.resolveApp(app)
            let info = try AppInspector().inspect(url)
            let decision = ModeDecider(profiles: try ProfileStore.bundled()).decide(for: info)
            print("\(info.name) \(info.version) (\(info.bundleID))")
            print("Path: \(url.path)")
            print("Mode: \(decision.mode?.rawValue ?? "none") · Support: \(decision.support.rawValue)")
            for reason in decision.reasons { print("Why: \(reason)") }
            print("Frameworks: \(info.frameworks.isEmpty ? "none" : info.frameworks.map(\.rawValue).sorted().joined(separator: ", "))")
            print("Sandboxed: \(info.isSandboxed ? "yes" : "no") · App Store receipt: \(info.hasMASReceipt ? "yes" : "no")")
            print("Restricted entitlements: \(info.restrictedEntitlements.isEmpty ? "none" : info.restrictedEntitlements.joined(separator: ", "))")
        }
    }
}

struct Clone: ParsableCommand {
    static let configuration = CommandConfiguration(abstract: "Create a clone of an app.")
    @Argument(help: "App name, bundle ID, or path.") var app: String
    @Option(help: "Name of the clone, e.g. \"Slack Work\".") var name: String
    @Option(help: "Badge text (1–2 characters). Defaults to the first letter of the name.") var badge: String?
    @Option(help: "Badge color: blue, green, orange, red, purple, pink, yellow, gray, or #RRGGBB.") var color: String = "blue"
    @Option(help: "auto, identity, or fallback.") var mode: String = "auto"

    func run() throws {
        try friendly {
            let url = try Context.resolveApp(app)
            let hex = Badge.presets[color.lowercased()] ?? (color.hasPrefix("#") && color.count == 7 ? color.uppercased() : nil)
            guard let hex else { throw CLIError(description: "Unknown color \"\(color)\".") }
            let modeOverride: CloneMode?
            switch mode.lowercased() {
            case "auto": modeOverride = nil
            case "identity": modeOverride = .identity
            case "fallback": modeOverride = .fallback
            default: throw CLIError(description: "Unknown mode \"\(mode)\". Use auto, identity, or fallback.")
            }
            let trimmed = name.trimmingCharacters(in: .whitespaces)
            let text = String((badge ?? trimmed.first.map(String.init) ?? "?").prefix(2)).uppercased()
            let entry = try Context.builder().create(CloneRequest(source: url, name: name, badge: Badge(text: text, color: hex),
                                                                  modeOverride: modeOverride))
            print("Created \"\(entry.manifest.name)\" (\(entry.manifest.mode.rawValue) mode) at \(entry.bundlePath)")
        }
    }
}

struct Refresh: ParsableCommand {
    static let configuration = CommandConfiguration(abstract: "Rebuild clones from the current version of their original app.")
    @Argument(help: "Clone name.") var clone: String?
    @Flag(help: "Refresh every clone that has an update.") var all = false

    func run() throws {
        try friendly {
            let maintenance = CloneMaintenance(builder: try Context.builder())
            let targets: [RegistryEntry]
            if all {
                targets = try Context.entries().filter {
                    if case .updateAvailable = maintenance.status(of: $0) { return true } else { return false }
                }
            } else {
                guard let clone else { throw CLIError(description: "Name a clone or use --all.") }
                targets = [try Context.findClone(clone)]
            }
            if targets.isEmpty { print("Everything is up to date.") }
            for t in targets {
                let updated = try maintenance.refresh(t)
                print("Refreshed \"\(updated.manifest.name)\" to \(updated.manifest.source.version)")
            }
        }
    }
}

struct Delete: ParsableCommand {
    static let configuration = CommandConfiguration(abstract: "Move a clone to the Trash.")
    @Argument(help: "Clone name.") var clone: String
    @Flag(help: "Also move the clone's data (logins, settings) to the Trash.") var deleteData = false

    func run() throws {
        try friendly {
            let entry = try Context.findClone(clone)
            try CloneMaintenance(builder: try Context.builder()).delete(entry, deleteData: deleteData)
            print("Moved \"\(entry.manifest.name)\" to the Trash\(deleteData ? " with its data" : " (data kept)").")
        }
    }
}

struct Open: ParsableCommand {
    static let configuration = CommandConfiguration(abstract: "Open a clone.")
    @Argument(help: "Clone name.") var clone: String

    func run() throws {
        try friendly {
            try CloneLauncher.open(try Context.findClone(clone).bundleURL)
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `swift test --filter CLITests`
Expected: PASS. ArgumentParser prints errors as `Error: <message>` on stderr with a non-zero exit, which the `contains` checks accept.

- [ ] **Step 5: Run the whole suite**

Run: `swift test`
Expected: all suites PASS (launch tests open two invisible fixture apps briefly).

- [ ] **Step 6: Manual smoke test on a real app (human partner watches)**

```bash
swift build -c release
.build/release/mitosis doctor Signal
.build/release/mitosis clone Signal --name "Signal Test" --color purple
open ~/Applications/Mitosis
```
Expected: `doctor` prints mode/support from the bundled profile; clone appears in `~/Applications/Mitosis` with a purple "S" badge and opens to Signal's "Link this device" screen. Then clean up with `.build/release/mitosis delete "Signal Test" --delete-data` (moves to Trash).

- [ ] **Step 7: Commit**

```bash
git add Sources/mitosis/MitosisCLI.swift Tests/MitosisCoreTests/CLITests.swift
git commit -m "feat(cli): mitosis command-line tool

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```
