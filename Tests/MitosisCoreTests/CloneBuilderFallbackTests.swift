import Foundation
import Testing
@testable import MitosisCore

@Suite struct CloneBuilderFallbackTests {
    @Test func restrictedEntitlementsProduceAShortcutApp() throws {
        var o = FixtureOptions()
        o.entitlements = ["keychain-access-groups": ["ABCDE12345.com.example"]]
        let (root, source, builder) = try CloneBuilderIdentityTests.setUp(o)
        let entry = try builder.create(CloneRequest(source: source, name: "Fixture (Safe)", badge: Badge(text: "S", color: "#30D158")))

        #expect(entry.manifest.mode == .fallback)
        let clone = root.appendingPathComponent("Clones/Fixture (Safe).app")
        let info = try InfoPlistEditor.read(clone.appendingPathComponent("Contents/Info.plist"))
        #expect(info["CFBundleIdentifier"] as? String == "com.example.fixture.mitosis.fixture-safe")
        #expect(info["CFBundleExecutable"] as? String == "MitosisShortcut")
        #expect(info["LSUIElement"] as? Bool == true)
        #expect(info["CFBundleIconFile"] as? String == "MitosisIcon")

        let launch = try JSONDecoder().decode(LaunchConfig.self, from: Data(contentsOf: clone.appendingPathComponent("Contents/Resources/mitosis-launch.json")))
        #expect(launch == LaunchConfig(kind: .spawn, target: source.appendingPathComponent("Contents/MacOS/Fixture").path, args: [],
                                     env: ["HOME": entry.manifest.dataPath], pidFile: entry.manifest.dataPath + "/.mitosis-instance.pid",
                                     dirs: [entry.manifest.dataPath]))
        try Signer.verify(clone)
        #expect(try Entitlements.read(from: clone).isEmpty)
    }

    @Test func reportsFallbackSteps() throws {
        var (_, source, builder) = try CloneBuilderIdentityTests.setUp()
        let recorder = StepRecorder()
        builder.progress = recorder.callback
        _ = try builder.create(CloneRequest(source: source, name: "Fixture (Steps)", badge: Badge(text: "S", color: "#0A84FF"), modeOverride: .fallback))
        #expect(recorder.steps == ["copy", "stub", "icon", "plist", "manifest", "sign", "verify", "install"])
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
