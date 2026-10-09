import Foundation
import Testing
@testable import MitosisCore

@Suite struct StatsCollectorTests {
    @Test func extraDiskIsOnlyWhatChanged() throws {
        let (_, source, builder) = try CloneBuilderIdentityTests.setUp()
        let entry = try builder.create(CloneRequest(source: source, name: "Fixture (Work)", badge: Badge(text: "W", color: "#0A84FF")))
        let stats = StatsCollector.stats(for: entry)
        #expect(stats.extraDiskBytes > 0)
        #expect(stats.extraDiskBytes < stats.appBytes)   // unchanged files (framework, resources) are shared with the original
        #expect(StatsCollector.extraDiskBytes(clone: source, source: source) == 0)
    }

    @Test func dataBytesCountsFilesAndIgnoresMissingPaths() throws {
        let dir = try TestSupport.tempDir()
        let data = dir.appendingPathComponent("data")
        try FileManager.default.createDirectory(at: data, withIntermediateDirectories: true)
        try Data(count: 50_000).write(to: data.appendingPathComponent("db.sqlite"))
        #expect(StatsCollector.dataBytes([data, dir.appendingPathComponent("missing")]) >= 50_000)
    }

    @Test func notRunningCloneHasNoUsage() throws {
        let dir = try TestSupport.tempDir()
        #expect(StatsCollector.usage(bundle: try FixtureFactory.makeApp(in: dir)) == nil)
    }

    @Test func cleanCachesRemovesOnlyCacheFolders() throws {
        let dir = try TestSupport.tempDir()
        let fm = FileManager.default
        for folder in ["Cache", "update-cache", "Local Storage"] {
            try fm.createDirectory(at: dir.appendingPathComponent(folder), withIntermediateDirectories: true)
            try Data(count: 20_000).write(to: dir.appendingPathComponent(folder).appendingPathComponent("blob"))
        }
        let freed = try StatsCollector.cleanCaches(dataPath: dir)
        #expect(freed >= 40_000)
        #expect(!fm.fileExists(atPath: dir.appendingPathComponent("Cache").path))
        #expect(!fm.fileExists(atPath: dir.appendingPathComponent("update-cache").path))
        #expect(fm.fileExists(atPath: dir.appendingPathComponent("Local Storage/blob").path))   // user data kept
    }
}
