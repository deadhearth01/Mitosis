import Foundation
import Testing
@testable import MitosisCore

@Suite struct CloneMaintenanceTests {
    static func setUp() throws -> (root: URL, source: URL, builder: CloneBuilder, entry: RegistryEntry) {
        let (root, source, builder) = try CloneBuilderIdentityTests.setUp()
        let entry = try builder.create(CloneRequest(source: source, name: "Fixture (Work)", badge: Badge(text: "W", color: "#0A84FF")))
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

    @Test func restyleChangesOnlyTheBadge() throws {
        let (_, _, builder, entry) = try Self.setUp()
        let icon = entry.bundleURL.appendingPathComponent("Contents/Resources/MitosisIcon.icns")
        let before = try Data(contentsOf: icon)
        let m = CloneMaintenance(builder: builder, isRunning: { _ in false })
        let styled = try m.restyle(entry, badge: Badge(text: "Z", color: "#FF453A"))
        #expect(styled.manifest.badge == Badge(text: "Z", color: "#FF453A"))
        #expect(styled.id == entry.id)
        #expect(styled.manifest.cloneBundleID == entry.manifest.cloneBundleID)
        #expect(styled.manifest.dataPath == entry.manifest.dataPath)
        #expect(styled.manifest.name == entry.manifest.name)
        #expect(styled.bundlePath == entry.bundlePath)
        #expect(CloneRegistry.embeddedManifest(in: entry.bundleURL)?.badge.text == "Z")
        #expect(try Data(contentsOf: icon) != before)
        #expect(try CloneRegistry(fileURL: builder.environment.registryFile).load().first?.manifest.badge.color == "#FF453A")
        try Signer.verify(entry.bundleURL)
    }

    @Test func restyleRefusesWhileRunning() throws {
        let (_, _, builder, entry) = try Self.setUp()
        let m = CloneMaintenance(builder: builder, isRunning: { _ in true })
        #expect(throws: CloneError.cloneRunning("Fixture (Work)")) { try m.restyle(entry, badge: Badge(text: "Z", color: "#FF453A")) }
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
        #expect(names == ["Fixture (Work).app"])   // no temp leftovers
    }

    // Review Focus: running clones are protected.
    @Test func refreshAndDeleteRefuseWhileRunning() throws {
        let (_, _, builder, entry) = try Self.setUp()
        let m = CloneMaintenance(builder: builder, isRunning: { _ in true })
        #expect(throws: CloneError.cloneRunning("Fixture (Work)")) { try m.refresh(entry) }
        #expect(throws: CloneError.cloneRunning("Fixture (Work)")) { try m.delete(entry, deleteData: true) }
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
