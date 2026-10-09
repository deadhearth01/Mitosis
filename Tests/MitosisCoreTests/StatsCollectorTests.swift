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

    // Final review I5: a full copy on another drive is not "nearly free".
    @Test(.enabled(if: TestSupport.hasSecondVolume, "needs the repo on a different volume than /private/tmp"))
    func crossVolumeCopyCountsAsFullSize() throws {
        let internalDir = URL(fileURLWithPath: "/private/tmp/mitosis-xvol-\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: internalDir, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: internalDir) }
        var o = FixtureOptions(); o.signed = false
        let src = try FixtureFactory.makeApp(in: internalDir, o)
        let dst = try TestSupport.tempDir().appendingPathComponent("Copy.app")
        #expect(!(try FileCloner.cloneOrCopy(from: src, to: dst)))   // different volume → real copy
        let extra = StatsCollector.extraDiskBytes(clone: dst, source: src)
        let fullSize = try FileManager.default.subpathsOfDirectory(atPath: dst.path).reduce(Int64(0)) { total, sub in
            let attrs = try FileManager.default.attributesOfItem(atPath: dst.appendingPathComponent(sub).path)
            return (attrs[.type] as? FileAttributeType) == .typeRegular ? total + ((attrs[.size] as? Int64) ?? 0) : total
        }
        #expect(extra == fullSize, "extra \(extra) vs full \(fullSize)")
    }

    @Test func usageOfAProcessTreeIncludesChildren() throws {
        let child = Process()
        child.executableURL = URL(fileURLWithPath: "/bin/sleep")
        child.arguments = ["5"]
        try child.run()
        defer { child.terminate() }
        let usage = try #require(StatsCollector.usage(rootPID: getpid()))
        #expect(usage.processCount >= 2)
        #expect(usage.memoryBytes > 0)
        #expect(StatsCollector.usage(rootPID: 999_999) == nil)
    }
}
