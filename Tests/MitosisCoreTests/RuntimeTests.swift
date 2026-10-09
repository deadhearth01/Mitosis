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

@Suite struct ProcessPathTests {
    @Test func executablePathOfLiveAndDeadProcess() throws {
        let mine = try #require(RunningMonitor.executablePath(of: getpid()))
        #expect(FileManager.default.isExecutableFile(atPath: mine))
        let p = Process()
        p.executableURL = URL(fileURLWithPath: "/usr/bin/true")
        try p.run()
        p.waitUntilExit()
        #expect(RunningMonitor.executablePath(of: p.processIdentifier) == nil)
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

    static func setUpSource(_ o: FixtureOptions) throws -> (URL, CloneBuilder) {
        let root = try TestSupport.tempDir()
        let apps = root.appendingPathComponent("Applications")
        try FileManager.default.createDirectory(at: apps, withIntermediateDirectories: true)
        var opts = o
        opts.bundleID = "com.example.launchfixture.\(UUID().uuidString.prefix(8).lowercased())"
        let source = try FixtureFactory.makeApp(in: apps, opts)
        var env = TestSupport.environment(root: root)
        env.registerWithLaunchServices = true
        return (source, CloneBuilder(environment: env, profiles: try ProfileStore(profiles: [])))
    }

    // Final review C2/I3: a running fallback clone is detected through its own instance, and protected.
    @Test func runningFallbackCloneIsDetectedAndProtected() async throws {
        // Fallback is forced explicitly: an ad-hoc fixture claiming a restricted entitlement would be killed by macOS.
        var o = FixtureOptions(); o.behavior = .stayOpen(seconds: 20)
        let (source, ssdBuilder) = try Self.setUpSource(o)
        // Real clones keep data in ~/Library on the internal disk. A clone opened through LaunchServices is its own
        // "responsible" app, so macOS privacy controls (Removable Volumes) block it from writing its instance file
        // to the external drive the other tests use. Keep this clone's (tiny) data folder on the internal disk.
        let internalData = FileManager.default.temporaryDirectory.appendingPathComponent("mitosis-fallback-\(UUID().uuidString)")
        defer { try? FileManager.default.removeItem(at: internalData) }
        var env = ssdBuilder.environment
        env.dataRoot = internalData
        let builder = CloneBuilder(environment: env, profiles: ssdBuilder.profiles)
        let entry = try builder.create(CloneRequest(source: source, name: "Fixture (Safe)", badge: Badge(text: "S", color: "#30D158"),
                                                    modeOverride: .fallback))
        defer { CloneLauncher.terminate(entry); LaunchServices.unregister(entry.bundleURL) }
        #expect(entry.manifest.mode == .fallback)
        #expect(!RunningMonitor.isRunning(clone: entry.manifest))
        try CloneLauncher.open(entry.bundleURL)
        // Shortcut → stub → app takes longer when the machine is busy (e.g. the whole suite running in parallel).
        for _ in 0..<60 where !RunningMonitor.isRunning(clone: entry.manifest) { try await Task.sleep(for: .milliseconds(250)) }
        #expect(RunningMonitor.isRunning(clone: entry.manifest))
        let maintenance = CloneMaintenance(builder: builder)
        #expect(throws: CloneError.cloneRunning("Fixture (Safe)")) { try maintenance.refresh(entry) }
        CloneLauncher.terminate(entry)
        try await Task.sleep(for: .seconds(1))
        #expect(!RunningMonitor.isRunning(clone: entry.manifest))
    }

    // Final review I2: a clone that doesn't start is removed by the core workflow, with an accurate error.
    @Test func createVerifiedRemovesCloneThatDoesNotStart() async throws {
        var o = FixtureOptions(); o.behavior = .exit(code: 1)
        var (source, builder) = try Self.setUpSource(o)
        let recorder = StepRecorder()
        builder.progress = recorder.callback
        let err = await #expect(throws: CloneError.self) {
            _ = try await CloneWorkflow(builder: builder).createVerified(
                CloneRequest(source: source, name: "Broken (Start)", badge: Badge(text: "B", color: "#FF453A")), window: .seconds(3))
        }
        if case .failed(let step, _)? = err { #expect(step == "launch check") } else { Issue.record("unexpected error \(String(describing: err))") }
        #expect(recorder.steps.last == "launch check")
        let clones = (try? FileManager.default.contentsOfDirectory(atPath: builder.environment.clonesDir.path)) ?? []
        #expect(clones.filter { $0.hasSuffix(".app") }.isEmpty)
        #expect(((try? FileManager.default.contentsOfDirectory(atPath: builder.environment.dataRoot.path)) ?? []).isEmpty)
        #expect(try CloneRegistry(fileURL: builder.environment.registryFile).load().isEmpty)
    }

    // A compatibility-mode clone that doesn't start must not suggest compatibility mode again.
    @Test func failedFallbackCloneDoesNotSuggestFallback() async throws {
        var o = FixtureOptions(); o.behavior = .exit(code: 1)
        let (source, builder) = try Self.setUpSource(o)
        let err = await #expect(throws: CloneError.self) {
            _ = try await CloneWorkflow(builder: builder).createVerified(
                CloneRequest(source: source, name: "Broken (Safe)", badge: Badge(text: "B", color: "#FF453A"), modeOverride: .fallback),
                window: .seconds(2))
        }
        let text = err.map { String(describing: $0) } ?? ""
        #expect(text.contains("didn't stay open"))
        #expect(!text.contains("compatibility"))
    }

    // Final review I1: refresh keeps the previous version if the new one doesn't start.
    @Test func refreshVerifiedRestoresPreviousVersionWhenNewOneFails() async throws {
        var o = FixtureOptions(); o.behavior = .stayOpen(seconds: 20)
        let (source, builder) = try Self.setUpSource(o)
        let workflow = CloneWorkflow(builder: builder)
        let entry = try await workflow.createVerified(CloneRequest(source: source, name: "Fixture (Refresh)", badge: Badge(text: "R", color: "#0A84FF")),
                                                      window: .seconds(3))
        defer { CloneLauncher.terminate(entry); LaunchServices.unregister(entry.bundleURL) }
        CloneLauncher.terminate(entry)
        try await Task.sleep(for: .seconds(1))

        // The "new version" of the original app crashes at launch.
        try "exit=1\n".write(to: source.appendingPathComponent("Contents/Resources/fixture.conf"), atomically: true, encoding: .utf8)
        try Shell.run("/usr/bin/codesign", ["--force", "--sign", "-", source.path])
        let err = await #expect(throws: CloneError.self) { _ = try await workflow.refreshVerified(entry, window: .seconds(3)) }
        if case .failed(let step, _)? = err { #expect(step == "launch check") } else { Issue.record("unexpected error \(String(describing: err))") }
        let conf = try String(contentsOf: entry.bundleURL.appendingPathComponent("Contents/Resources/fixture.conf"), encoding: .utf8)
        #expect(conf.contains("stay="))   // previous version restored
        #expect(try CloneRegistry(fileURL: builder.environment.registryFile).load().first?.manifest.source.cdhash == entry.manifest.source.cdhash)
        let leftovers = try FileManager.default.contentsOfDirectory(atPath: builder.environment.clonesDir.path).filter { $0.hasPrefix(".mitosis-") }
        #expect(leftovers.isEmpty)
    }
}
