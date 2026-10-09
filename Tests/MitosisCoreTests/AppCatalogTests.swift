import Foundation
import Testing
@testable import MitosisCore

@Suite struct AppCatalogTests {
    @Test func listsClonableAppsOneFolderDeepSortedByName() throws {
        let dir = try TestSupport.tempDir()
        func make(_ name: String, in folder: URL, bundleID: String? = nil) throws -> URL {
            try FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true)
            var o = FixtureOptions(); o.name = name; o.executable = name.replacingOccurrences(of: " ", with: "")
            o.bundleID = bundleID ?? "com.example.\(o.executable.lowercased())"
            return try FixtureFactory.makeApp(in: folder, o)
        }
        _ = try make("Zeta", in: dir)
        _ = try make("alpha", in: dir)
        _ = try make("Mid", in: dir.appendingPathComponent("Tools"))
        let clone = try make("Already Clone", in: dir)
        try Data("{}".utf8).write(to: clone.appendingPathComponent("Contents/Resources/mitosis.json"))
        _ = try make("Mitosis", in: dir, bundleID: Mitosis.bundleID)
        _ = try make("Hidden", in: dir.appendingPathComponent("Deep/Deeper"))
        try Data("x".utf8).write(to: dir.appendingPathComponent("notes.txt"))

        let apps = AppCatalog.scan([dir, dir.appendingPathComponent("Missing")])
        #expect(apps.map(\.name) == ["alpha", "Mid", "Zeta"])
        #expect(apps.first?.bundleID == "com.example.alpha")
        #expect(apps.first?.version == "1.0")
    }

    @Test func inspectionCanSkipTheCDHash() throws {
        let dir = try TestSupport.tempDir()
        let app = try FixtureFactory.makeApp(in: dir)
        #expect(try AppInspector().inspect(app, includeCDHash: false).cdhash == nil)
        #expect(try AppInspector().inspect(app).cdhash != nil)
    }
}
