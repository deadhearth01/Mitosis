import Foundation
import Synchronization
import Testing
@testable import MitosisCore

/// Collects progress steps reported from any thread.
final class StepRecorder: Sendable {
    private let box = Mutex<[String]>([])
    var steps: [String] { box.withLock { $0 } }
    var callback: @Sendable (String) -> Void { { [self] step in box.withLock { $0.append(step) } } }
}

@Suite struct CloneBuilderIdentityTests {
    static func setUp(_ options: FixtureOptions = {
        var o = FixtureOptions()
        o.frameworks = ["Electron Framework"]
        o.helperApps = ["Fixture Helper", "Fixture Helper (Renderer)"]
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

    @Test func reportsEachStepInOrder() throws {
        var (_, source, builder) = try Self.setUp()
        let recorder = StepRecorder()
        builder.progress = recorder.callback
        _ = try builder.create(CloneRequest(source: source, name: "Fixture (Steps)", badge: Badge(text: "S", color: "#0A84FF")))
        #expect(recorder.steps == ["copy", "helpers", "stub", "icon", "plist", "manifest", "sign", "verify", "install"])
    }

    @Test func createsASignedIdentityCloneWithItsOwnID() throws {
        let (root, source, builder) = try Self.setUp()
        let sourceHash = AppInspector.cdhash(of: source)
        let framework = source.appendingPathComponent("Contents/Frameworks/Electron Framework.framework")
        let frameworkHash = AppInspector.cdhash(of: framework)
        let entry = try builder.create(CloneRequest(source: source, name: "Fixture (Work)", badge: Badge(text: "W", color: "#0A84FF")))

        let clone = root.appendingPathComponent("Clones/Fixture (Work).app")
        #expect(entry.bundleURL.path == clone.path)
        let info = try InfoPlistEditor.read(clone.appendingPathComponent("Contents/Info.plist"))
        #expect(info["CFBundleIdentifier"] as? String == "com.example.fixture.mitosis.fixture-work")
        #expect(info["CFBundleName"] as? String == "Fixture (Work)")
        #expect(info["CFBundleIconFile"] as? String == "MitosisIcon")
        #expect(FileManager.default.fileExists(atPath: clone.appendingPathComponent("Contents/Resources/MitosisIcon.icns").path))
        #expect(FileManager.default.fileExists(atPath: clone.appendingPathComponent("Contents/MacOS/Fixture.mitosis-real").path))

        // A4/A5: Electron helpers carry the clone's name; vendor-signed framework untouched.
        let frameworks = clone.appendingPathComponent("Contents/Frameworks")
        #expect(FileManager.default.fileExists(atPath: frameworks.appendingPathComponent("Fixture (Work) Helper.app").path))
        #expect(FileManager.default.fileExists(atPath: frameworks.appendingPathComponent("Fixture (Work) Helper (Renderer).app").path))
        #expect(AppInspector.cdhash(of: frameworks.appendingPathComponent("Electron Framework.framework")) == frameworkHash)

        let launchData = try Data(contentsOf: clone.appendingPathComponent("Contents/Resources/mitosis-launch.json"))
        let launch = try JSONDecoder().decode(LaunchConfig.self, from: launchData)
        #expect(launch.kind == .exec)
        #expect(launch.target == "Fixture.mitosis-real")
        #expect(launch.args == ["--user-data-dir=\(entry.manifest.dataPath)"])
        #expect(FileManager.default.fileExists(atPath: entry.manifest.dataPath))
        #expect(URL(fileURLWithPath: entry.manifest.dataPath).lastPathComponent.count == 8)   // A5: short path for socket limits

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
        _ = try builder.create(CloneRequest(source: source, name: "Native (2)", badge: Badge(text: "2", color: "#FF9F0A")))
        let clone = root.appendingPathComponent("Clones/Native (2).app")
        #expect(!FileManager.default.fileExists(atPath: clone.appendingPathComponent("Contents/MacOS/Fixture.mitosis-real").path))
        #expect(!FileManager.default.fileExists(atPath: clone.appendingPathComponent("Contents/Resources/mitosis-launch.json").path))
        try Signer.verify(clone)
    }

    @Test func duplicateNameIsRejectedAndSlugIsDeduplicated() throws {
        let (_, source, builder) = try Self.setUp()
        _ = try builder.create(CloneRequest(source: source, name: "Fixture (Work)", badge: Badge(text: "W", color: "#0A84FF")))
        let err = #expect(throws: CloneError.self) {
            try builder.create(CloneRequest(source: source, name: "Fixture (Work)", badge: Badge(text: "W", color: "#0A84FF")))
        }
        #expect(err?.description == "A clone named \"Fixture (Work)\" already exists.")
        let second = try builder.create(CloneRequest(source: source, name: "Fixture Work", badge: Badge(text: "W", color: "#0A84FF")))
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
        let e = try builder.create(CloneRequest(source: source, name: "Ünïcode (Wörk)", badge: Badge(text: "Ü", color: "#BF5AF2")))
        #expect(e.manifest.cloneBundleID == "com.example.fixture.mitosis.unicode-work")
        try Signer.verify(root.appendingPathComponent("Clones/Ünïcode (Wörk).app"))
    }

    @Test func missingSourceIsReported() throws {
        let (root, _, builder) = try Self.setUp()
        let missing = root.appendingPathComponent("Nope.app")
        #expect(throws: CloneError.sourceMissing(missing.path)) {
            try builder.create(CloneRequest(source: missing, name: "X", badge: Badge(text: "X", color: "#0A84FF")))
        }
    }

    // Final review C1: failures after the bundle reached its final place must not leave it behind.
    @Test(arguments: ["install", "registry"])
    func failureAfterInstallRemovesTheInstalledClone(step: String) throws {
        let (root, source, base) = try Self.setUp()
        var builder = base
        builder.failAfterStep = step
        #expect(throws: CloneError.self) {
            try builder.create(CloneRequest(source: source, name: "Late", badge: Badge(text: "L", color: "#0A84FF")))
        }
        #expect(((try? FileManager.default.contentsOfDirectory(atPath: root.appendingPathComponent("Clones").path)) ?? []).isEmpty)
        #expect(((try? FileManager.default.contentsOfDirectory(atPath: root.appendingPathComponent("Data").path)) ?? []).isEmpty)
        #expect(try CloneRegistry(fileURL: root.appendingPathComponent("clones.json")).load().isEmpty)
    }

    // Final review I6: warn before a full copy when the app is on another drive.
    @Test func preflightReportsFullCopyAcrossVolumes() throws {
        let (_, source, builder) = try Self.setUp()
        let same = try builder.preflight(source: source)
        #expect(!same.willCopyFully)
        #expect(same.copyBytes == 0)
        #expect(same.decision.support == .full)

        let internalDir = URL(fileURLWithPath: "/private/tmp/mitosis-xvol-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: internalDir, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: internalDir) }
        let far = try FixtureFactory.makeApp(in: internalDir)
        let other = try builder.preflight(source: far)
        #expect(other.willCopyFully)
        #expect(other.copyBytes > 0)
    }
}
