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
        #expect(r.stdout.trimmingCharacters(in: .whitespacesAndNewlines) == "0.1.1")
    }

    @Test func doctorCloneListStatsCleanDeleteFlow() throws {
        let root = try TestSupport.tempDir()
        var o = FixtureOptions(); o.frameworks = ["Electron Framework"]; o.helperApps = ["Fixture Helper"]
        let app = try FixtureFactory.makeApp(in: root, o)

        let doctor = try Self.run(["doctor", app.path], root: root)
        #expect(doctor.status == 0)
        #expect(doctor.stdout.contains("Fixture 1.0 (com.example.fixture)"))
        #expect(doctor.stdout.contains("Mode: identity · Support: full"))
        #expect(doctor.stdout.contains("Frameworks: electron"))

        let clone = try Self.run(["clone", app.path, "--label", "Work", "--color", "green", "--no-verify"], root: root)
        #expect(clone.status == 0, "\(clone.stderr)")
        #expect(clone.stdout.contains("Created \"Fixture (Work)\""))
        let entries = try CloneRegistry(fileURL: root.appendingPathComponent("clones.json")).load()
        #expect(entries.first?.manifest.badge == Badge(text: "W", color: "#30D158"))

        let list = try Self.run(["list"], root: root)
        #expect(list.stdout.contains("Fixture (Work)\tcom.example.fixture\tidentity\tok"))

        let stats = try Self.run(["stats", "fixture (work)"], root: root)
        #expect(stats.status == 0, "\(stats.stderr)")
        #expect(stats.stdout.contains("Extra disk:"))
        #expect(stats.stdout.contains("shared with Fixture"))
        #expect(stats.stdout.contains("Not running"))

        let dataPath = try #require(entries.first?.manifest.dataPath)
        try FileManager.default.createDirectory(atPath: dataPath + "/Cache", withIntermediateDirectories: true)
        try Data(count: 10_000).write(to: URL(fileURLWithPath: dataPath + "/Cache/blob"))
        let clean = try Self.run(["clean", "Fixture (Work)"], root: root)
        #expect(clean.stdout.contains("Freed"))
        #expect(!FileManager.default.fileExists(atPath: dataPath + "/Cache"))

        let delete = try Self.run(["delete", "fixture (work)", "--delete-data"], root: root)
        #expect(delete.status == 0, "\(delete.stderr)")
        #expect(try Self.run(["list"], root: root).stdout.contains("No clones yet"))
    }

    @Test func refreshOutdatedRefreshesChangedClones() throws {
        let root = try TestSupport.tempDir()
        let app = try FixtureFactory.makeApp(in: root)
        #expect(try Self.run(["clone", app.path, "--label", "Work", "--no-verify"], root: root).status == 0)
        let quiet = try Self.run(["refresh", "--outdated", "--quiet"], root: root)
        #expect(quiet.status == 0)
        #expect(quiet.stdout.isEmpty)
        try CloneMaintenanceTests.bumpVersion(of: app, to: "2.0")
        let r = try Self.run(["refresh", "--outdated"], root: root)
        #expect(r.status == 0, "\(r.stderr)")
        #expect(r.stdout.contains("Refreshed \"Fixture (Work)\""))
    }

    @Test func nameOptionStillWorks() throws {
        let root = try TestSupport.tempDir()
        let app = try FixtureFactory.makeApp(in: root)
        let r = try Self.run(["clone", app.path, "--name", "Custom Name", "--no-verify"], root: root)
        #expect(r.status == 0, "\(r.stderr)")
        #expect(r.stdout.contains("Created \"Custom Name\""))
    }

    @Test func friendlyErrors() throws {
        let root = try TestSupport.tempDir()
        let app = try FixtureFactory.makeApp(in: root)
        let bad = try Self.run(["clone", app.path, "--name", "a/b", "--no-verify"], root: root)
        #expect(bad.status != 0)
        #expect(bad.stderr.contains("Clone names can't contain \"/\"."))
        let neither = try Self.run(["clone", app.path, "--no-verify"], root: root)
        #expect(neither.status != 0)
        #expect(neither.stderr.contains("Use --label"))
        let missing = try Self.run(["clone", "DefinitelyNotAnApp", "--label", "X", "--no-verify"], root: root)
        #expect(missing.status != 0)
        #expect(missing.stderr.contains("Couldn't find an app called \"DefinitelyNotAnApp\"."))
        let color = try Self.run(["clone", app.path, "--label", "X", "--color", "#GGGGGG", "--no-verify"], root: root)
        #expect(color.status != 0)
        #expect(color.stderr.contains("Unknown color \"#GGGGGG\""))
        let noClone = try Self.run(["delete", "ghost"], root: root)
        #expect(noClone.stderr.contains("No clone matches \"ghost\"."))
    }
}
