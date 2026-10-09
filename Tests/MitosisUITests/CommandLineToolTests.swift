import Foundation
import Testing
@testable import MitosisUI

@Suite struct CommandLineToolTests {
    @Test func installsASymlinkAndReplacesOnlyItsOwnFiles() throws {
        let dir = try AppServicesTests.tempDir()
        let target = dir.appendingPathComponent("Mitosis.app/Contents/Helpers/mitosis")
        try FileManager.default.createDirectory(at: target.deletingLastPathComponent(), withIntermediateDirectories: true)
        try Data("#!/bin/sh\n".utf8).write(to: target)
        let bin = dir.appendingPathComponent("bin")
        let link = bin.appendingPathComponent("mitosis")

        #expect(try CommandLineTool.install(target: target, binDir: bin) == .installed)
        #expect(try FileManager.default.destinationOfSymbolicLink(atPath: link.path) == target.path)
        #expect(try CommandLineTool.install(target: target, binDir: bin) == .alreadyInstalled)

        // The wrapper the 0.1.0-alpha installer wrote is ours too, so it gets replaced.
        try FileManager.default.removeItem(at: link)
        try Data("#!/bin/sh\nexec \"/Users/x/.local/share/mitosis/mitosis\" \"$@\"\n".utf8).write(to: link)
        #expect(try CommandLineTool.install(target: target, binDir: bin) == .installed)
        #expect(try FileManager.default.destinationOfSymbolicLink(atPath: link.path) == target.path)

        // Someone else's file stays untouched.
        try FileManager.default.removeItem(at: link)
        try Data("#!/bin/sh\necho other tool\n".utf8).write(to: link)
        #expect(throws: CommandLineTool.InstallError.self) { try CommandLineTool.install(target: target, binDir: bin) }
        #expect(try String(contentsOf: link, encoding: .utf8).contains("other tool"))
    }

    @Test func knowsWhetherAFolderIsOnPath() {
        #expect(CommandLineTool.isOnPath(URL(fileURLWithPath: "/Users/x/.local/bin"), path: "/usr/bin:/Users/x/.local/bin"))
        #expect(!CommandLineTool.isOnPath(URL(fileURLWithPath: "/Users/x/.local/bin"), path: "/usr/bin:/bin"))
    }
}
