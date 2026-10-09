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

    // Final review I4 (plan defect): a missing registry must be rebuilt, not treated as "no clones".
    @Test func missingFileIsRebuiltFromEmbeddedManifests() throws {
        let dir = try TestSupport.tempDir()
        let clones = dir.appendingPathComponent("Clones")
        let e = Self.entry(name: "Survivor", in: clones)
        let resources = e.bundleURL.appendingPathComponent("Contents/Resources")
        try FileManager.default.createDirectory(at: resources, withIntermediateDirectories: true)
        try CloneManifest.encoder().encode(e.manifest).write(to: resources.appendingPathComponent(CloneRegistry.manifestFileName))
        let reg = CloneRegistry(fileURL: dir.appendingPathComponent("clones.json"))
        #expect(try reg.loadOrRebuild(clonesDir: clones).map(\.id) == [e.id])
    }

    /// A clone put back from the Trash reappears; one trashed in Finder disappears from the list.
    @Test func loadOrRebuildSyncsWithTheClonesFolder() throws {
        let dir = try TestSupport.tempDir()
        let clones = dir.appendingPathComponent("Clones")
        func install(_ e: RegistryEntry) throws {
            let resources = e.bundleURL.appendingPathComponent("Contents/Resources")
            try FileManager.default.createDirectory(at: resources, withIntermediateDirectories: true)
            try CloneManifest.encoder().encode(e.manifest).write(to: resources.appendingPathComponent(CloneRegistry.manifestFileName))
        }
        let kept = Self.entry(name: "Kept", in: clones)
        let trashed = Self.entry(name: "Trashed In Finder", in: clones)
        let putBack = Self.entry(name: "Put Back", in: clones)
        try install(kept)
        try install(putBack)
        let reg = CloneRegistry(fileURL: dir.appendingPathComponent("clones.json"))
        try reg.save([kept, trashed])

        let loaded = try reg.loadOrRebuild(clonesDir: clones)
        #expect(Set(loaded.map(\.id)) == [kept.id, putBack.id])
        #expect(Set(try reg.load().map(\.id)) == [kept.id, putBack.id])   // saved
    }
}
