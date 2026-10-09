import Foundation
import Testing
@testable import MitosisUI

@Suite struct NewCloneModelTests {
    @Test func rejectsThingsThatAreNotClonableApps() throws {
        let dir = try AppServicesTests.tempDir()
        let folder = dir.appendingPathComponent("Folder")
        let app = dir.appendingPathComponent("Real.app")
        let fakeApp = dir.appendingPathComponent("Fake.app")
        try FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true)
        try FileManager.default.createDirectory(at: app, withIntermediateDirectories: true)
        try Data("x".utf8).write(to: fakeApp)
        let text = dir.appendingPathComponent("notes.txt")
        try Data("x".utf8).write(to: text)

        #expect(NewCloneModel.rejection(for: folder) == "That's not an app.")
        #expect(NewCloneModel.rejection(for: text) == "That's not an app.")
        #expect(NewCloneModel.rejection(for: fakeApp) == "That's not an app.")
        #expect(NewCloneModel.rejection(for: dir.appendingPathComponent("Missing.app")) == "That's not an app.")
        #expect(NewCloneModel.rejection(for: URL(fileURLWithPath: "/System/Applications/Calculator.app"))
                == "Apple's own apps are protected by macOS and can't be cloned.")
        #expect(NewCloneModel.rejection(for: app) == nil)
    }
}
