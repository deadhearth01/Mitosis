import Foundation

public struct ShellResult: Sendable, Equatable {
    public let status: Int32
    public let stdout: String
    public let stderr: String
}

public enum ShellError: Error, CustomStringConvertible, Sendable {
    case failed(command: String, status: Int32, stderr: String)

    public var description: String {
        switch self {
        case let .failed(command, status, stderr):
            let detail = stderr.trimmingCharacters(in: .whitespacesAndNewlines)
            return "\(command) failed (\(status))\(detail.isEmpty ? "" : ": \(detail)")"
        }
    }
}

public enum Shell {
    /// Runs a tool and waits for it. Output goes through temp files so large outputs can't deadlock.
    @discardableResult
    public static func run(
        _ executable: String,
        _ arguments: [String],
        environment: [String: String]? = nil,
        check: Bool = true
    ) throws -> ShellResult {
        let fm = FileManager.default
        let tmp = fm.temporaryDirectory
        let outURL = tmp.appendingPathComponent("mitosis-shell-\(UUID().uuidString).out")
        let errURL = tmp.appendingPathComponent("mitosis-shell-\(UUID().uuidString).err")
        fm.createFile(atPath: outURL.path, contents: nil)
        fm.createFile(atPath: errURL.path, contents: nil)
        defer {
            try? fm.removeItem(at: outURL)
            try? fm.removeItem(at: errURL)
        }
        let outHandle = try FileHandle(forWritingTo: outURL)
        let errHandle = try FileHandle(forWritingTo: errURL)

        let process = Process()
        process.executableURL = URL(fileURLWithPath: executable)
        process.arguments = arguments
        if let environment { process.environment = environment }
        process.standardOutput = outHandle
        process.standardError = errHandle
        try process.run()
        process.waitUntilExit()
        try outHandle.close()
        try errHandle.close()

        let result = ShellResult(
            status: process.terminationStatus,
            stdout: String(decoding: try Data(contentsOf: outURL), as: UTF8.self),
            stderr: String(decoding: try Data(contentsOf: errURL), as: UTF8.self)
        )
        if check && result.status != 0 {
            throw ShellError.failed(
                command: ([executable] + arguments).joined(separator: " "),
                status: result.status,
                stderr: result.stderr
            )
        }
        return result
    }
}
