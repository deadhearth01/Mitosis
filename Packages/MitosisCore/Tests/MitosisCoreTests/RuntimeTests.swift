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

/// Launches real (tiny, invisible) GUI apps through macOS. Skip with MITOSIS_SKIP_LAUNCH_TESTS=1 (e.g. in CI).
@Suite(.serialized, .enabled(if: ProcessInfo.processInfo.environment["MITOSIS_SKIP_LAUNCH_TESTS"] == nil))
struct LaunchTests {
    static func makeClone(behavior: FixtureBehavior, name: String) throws -> RegistryEntry {
        let root = try TestSupport.tempDir()
        let apps = root.appendingPathComponent("Applications")
        try FileManager.default.createDirectory(at: apps, withIntermediateDirectories: true)
        var o = FixtureOptions(); o.behavior = behavior; o.bundleID = "com.example.launchfixture.\(UUID().uuidString.prefix(8).lowercased())"
        let source = try FixtureFactory.makeApp(in: apps, o)
        var env = TestSupport.environment(root: root)
        env.registerWithLaunchServices = true
        let builder = CloneBuilder(environment: env, profiles: try ProfileStore(profiles: []))
        return try builder.create(CloneRequest(source: source, name: name, badge: Badge(text: "T", color: "#0A84FF")))
    }

    @Test func runningCloneIsDetectedWithUsage() async throws {
        let entry = try Self.makeClone(behavior: .stayOpen(seconds: 20), name: "Stays Open")
        defer {
            _ = try? Shell.run("/usr/bin/pkill", ["-f", entry.bundlePath], check: false)
            LaunchServices.unregister(entry.bundleURL)
        }
        let alive = try await CloneLauncher.openAndCheck(entry.bundleURL, bundleID: entry.manifest.cloneBundleID, window: .seconds(3))
        #expect(alive)
        #expect(RunningMonitor.isRunning(bundleID: entry.manifest.cloneBundleID))
        let usage = try #require(StatsCollector.usage(bundle: entry.bundleURL))   // A6
        #expect(usage.processCount >= 1)
        #expect(usage.memoryBytes > 0)
    }

    @Test func cloneThatExitsImmediatelyIsReported() async throws {
        let entry = try Self.makeClone(behavior: .exit(code: 1), name: "Exits")
        defer { LaunchServices.unregister(entry.bundleURL) }
        let alive = try await CloneLauncher.openAndCheck(entry.bundleURL, bundleID: entry.manifest.cloneBundleID, window: .seconds(3))
        #expect(!alive)
    }
}
