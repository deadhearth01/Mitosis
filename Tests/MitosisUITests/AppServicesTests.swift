import Foundation
import Testing
@testable import MitosisUI

@Suite struct AppServicesTests {
    static func tempDir() throws -> URL {
        let url = URL(fileURLWithPath: #filePath).deletingLastPathComponent().deletingLastPathComponent().deletingLastPathComponent()
            .appendingPathComponent(".build/test-tmp/MitosisUITests/\(UUID().uuidString)")
        try FileManager.default.createDirectory(at: url, withIntermediateDirectories: true)
        return url
    }

    @Test func prefersTheStubInsideTheAppBundle() throws {
        let dir = try Self.tempDir()
        let app = dir.appendingPathComponent("Mitosis.app")
        let helpers = app.appendingPathComponent("Contents/Helpers")
        let bin = dir.appendingPathComponent("bin")
        try FileManager.default.createDirectory(at: helpers, withIntermediateDirectories: true)
        try FileManager.default.createDirectory(at: bin, withIntermediateDirectories: true)
        let exe = bin.appendingPathComponent("MitosisApp")
        try Data().write(to: bin.appendingPathComponent("LaunchStub"))
        #expect(AppServices.stubURL(bundle: app, executable: exe) == bin.appendingPathComponent("LaunchStub"))
        try Data().write(to: helpers.appendingPathComponent("LaunchStub"))
        #expect(AppServices.stubURL(bundle: app, executable: exe) == helpers.appendingPathComponent("LaunchStub"))
    }
}
