import Foundation
import Testing
@testable import MitosisCore

@Suite struct FileClonerTests {
    @Test func clonesADirectoryTreeOnTheSameVolume() throws {
        let dir = try TestSupport.tempDir()
        let src = try FixtureFactory.makeApp(in: dir)
        let dst = dir.appendingPathComponent("Copy.app")
        let cloned = try FileCloner.cloneOrCopy(from: src, to: dst)
        #expect(cloned)   // temp dir and copy are on the same APFS volume
        #expect(FileManager.default.fileExists(atPath: dst.appendingPathComponent("Contents/MacOS/Fixture").path))
        #expect(FileManager.default.isWritableFile(atPath: dst.appendingPathComponent("Contents/Info.plist").path))
    }

    @Test func preservesModificationDates() throws {
        // StatsCollector (A6) relies on unchanged files keeping the source's mtime.
        let dir = try TestSupport.tempDir()
        let src = try FixtureFactory.makeApp(in: dir)
        let dst = dir.appendingPathComponent("Copy.app")
        try FileCloner.cloneOrCopy(from: src, to: dst)
        let rel = "Contents/MacOS/Fixture"
        let a = try FileManager.default.attributesOfItem(atPath: src.appendingPathComponent(rel).path)[.modificationDate] as? Date
        let b = try FileManager.default.attributesOfItem(atPath: dst.appendingPathComponent(rel).path)[.modificationDate] as? Date
        #expect(a != nil && a == b)
    }

    @Test func failsWhenDestinationExists() throws {
        let dir = try TestSupport.tempDir()
        let src = try FixtureFactory.makeApp(in: dir)
        #expect(throws: (any Error).self) { try FileCloner.cloneOrCopy(from: src, to: src) }
    }

    @Test func sameVolumeDetection() throws {
        let dir = try TestSupport.tempDir()
        #expect(FileCloner.isSameVolume(dir, dir.appendingPathComponent("not/yet/created")))
    }

    @Test(.enabled(if: TestSupport.hasSecondVolume, "needs the repo on a different volume than /private/tmp"))
    func differentVolumesAreDetected() throws {
        #expect(!FileCloner.isSameVolume(try TestSupport.tempDir(), URL(fileURLWithPath: "/private/tmp")))
    }

    @Test func sizeIsPositive() throws {
        let dir = try TestSupport.tempDir()
        #expect(FileCloner.allocatedSize(of: try FixtureFactory.makeApp(in: dir)) > 0)
    }
}
