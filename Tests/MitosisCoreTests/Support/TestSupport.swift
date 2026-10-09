import Foundation
@testable import MitosisCore

enum TestSupport {
    /// Packages/MitosisCore
    static let packageRoot: URL = URL(fileURLWithPath: #filePath)
        .deletingLastPathComponent()   // Support
        .deletingLastPathComponent()   // MitosisCoreTests
        .deletingLastPathComponent()   // Tests
        .deletingLastPathComponent()   // MitosisCore

    /// `swift build`/`swift test` products (the `.build/debug` symlink).
    static let productsDirectory: URL = {
        let dir = packageRoot.appendingPathComponent(".build/debug")
        precondition(FileManager.default.fileExists(atPath: dir.path), "Run tests with `swift test` from Packages/MitosisCore")
        return dir
    }()

    static var stubBinary: URL { productsDirectory.appendingPathComponent("LaunchStub") }
    static var cliBinary: URL { productsDirectory.appendingPathComponent("mitosis") }

    /// Test scratch space inside the package's .build folder (the test runner ignores TMPDIR, and the
    /// system temp folder lives on the internal disk).
    static let tempRoot: URL = {
        let url = packageRoot.appendingPathComponent(".build/test-tmp", isDirectory: true)
        try? FileManager.default.createDirectory(at: url, withIntermediateDirectories: true)
        return url
    }()

    /// True when the test folder and /private/tmp are on different volumes (e.g. the repo on an external drive).
    /// CI machines usually have a single volume, so cross-volume tests only run where one exists.
    static let hasSecondVolume: Bool = !FileCloner.isSameVolume(tempRoot, URL(fileURLWithPath: "/private/tmp"))

    static func environment(root: URL) -> MitosisEnvironment {
        var env = MitosisEnvironment.standard(stubBinary: stubBinary, root: root)
        env.trashOverride = root.appendingPathComponent("Trash")
        env.registerWithLaunchServices = false
        return env
    }

    static func tempDir() throws -> URL {
        let url = tempRoot
            .appendingPathComponent("MitosisTests", isDirectory: true)
            .appendingPathComponent(UUID().uuidString, isDirectory: true)
        try FileManager.default.createDirectory(at: url, withIntermediateDirectories: true)
        return url
    }
}
