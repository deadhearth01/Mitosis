import Foundation

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

    static func tempDir() throws -> URL {
        let url = FileManager.default.temporaryDirectory
            .appendingPathComponent("MitosisTests", isDirectory: true)
            .appendingPathComponent(UUID().uuidString, isDirectory: true)
        try FileManager.default.createDirectory(at: url, withIntermediateDirectories: true)
        return url
    }
}
