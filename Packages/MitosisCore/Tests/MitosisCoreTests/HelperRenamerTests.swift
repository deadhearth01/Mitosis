import Foundation
import Testing
@testable import MitosisCore

@Suite struct HelperRenamerTests {
    static func app(helpers: [String]) throws -> URL {
        let dir = try TestSupport.tempDir()
        var o = FixtureOptions(); o.signed = false; o.helperApps = helpers
        return try FixtureFactory.makeApp(in: dir, o)
    }

    static func names(in app: URL) throws -> [String] {
        try FileManager.default.contentsOfDirectory(atPath: app.appendingPathComponent("Contents/Frameworks").path).sorted()
    }

    @Test func renamesHelperBundlesExecutablesAndPlists() throws {
        let app = try Self.app(helpers: ["Fixture Helper", "Fixture Helper (Renderer)", "Other Tool"])
        let renamed = try HelperRenamer.rename(in: app, from: "Fixture", to: "Work")
        #expect(renamed.map(\.lastPathComponent).sorted() == ["Work Helper (Renderer).app", "Work Helper.app"])
        #expect(try Self.names(in: app) == ["Other Tool.app", "Work Helper (Renderer).app", "Work Helper.app"])

        let renderer = app.appendingPathComponent("Contents/Frameworks/Work Helper (Renderer).app")
        #expect(FileManager.default.fileExists(atPath: renderer.appendingPathComponent("Contents/MacOS/Work Helper (Renderer)").path))
        let info = try InfoPlistEditor.read(renderer.appendingPathComponent("Contents/Info.plist"))
        #expect(info["CFBundleExecutable"] as? String == "Work Helper (Renderer)")
        #expect(info["CFBundleName"] as? String == "Work Helper (Renderer)")
    }

    // A8: names like "Fixture (Work)" contain parentheses and spaces.
    @Test func handlesParenthesizedCloneNames() throws {
        let app = try Self.app(helpers: ["Fixture Helper (Renderer)"])
        let renamed = try HelperRenamer.rename(in: app, from: "Fixture", to: "Fixture (Work)")
        #expect(renamed.map(\.lastPathComponent) == ["Fixture (Work) Helper (Renderer).app"])
        #expect(FileManager.default.fileExists(atPath: renamed[0].appendingPathComponent("Contents/MacOS/Fixture (Work) Helper (Renderer)").path))
    }

    @Test func noHelpersMeansNothingRenamed() throws {
        let app = try Self.app(helpers: [])
        #expect(try HelperRenamer.rename(in: app, from: "Fixture", to: "Work").isEmpty)
    }
}
