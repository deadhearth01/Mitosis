import Foundation

/// Links the `mitosis` command inside Mitosis.app into a bin folder (default ~/.local/bin).
enum CommandLineTool {
    enum InstallResult: Equatable {
        case installed
        case alreadyInstalled
    }

    struct InstallError: Error, CustomStringConvertible {
        var description: String
    }

    static var defaultBinDir: URL {
        FileManager.default.homeDirectoryForCurrentUser.appendingPathComponent(".local/bin")
    }

    static var bundledTool: URL {
        Bundle.main.bundleURL.appendingPathComponent("Contents/Helpers/mitosis")
    }

    /// Replaces only files Mitosis made: a link to some Mitosis.app, or the wrapper script of the 0.1.0-alpha installer.
    static func install(target: URL, binDir: URL = defaultBinDir) throws -> InstallResult {
        let fm = FileManager.default
        let link = binDir.appendingPathComponent("mitosis")
        if let existing = try? fm.destinationOfSymbolicLink(atPath: link.path) {
            if existing == target.path { return .alreadyInstalled }
            guard existing.hasSuffix("Contents/Helpers/mitosis") else {
                throw InstallError(description: "\(display(link)) already points to another tool, so Mitosis left it alone.")
            }
            try fm.removeItem(at: link)
        } else if fm.fileExists(atPath: link.path) {
            let text = (try? String(contentsOf: link, encoding: .utf8)) ?? ""
            guard text.hasPrefix("#!/bin/sh"), text.contains("/share/mitosis/mitosis") else {
                throw InstallError(description: "\(display(link)) already exists and isn't from Mitosis, so Mitosis left it alone.")
            }
            try fm.removeItem(at: link)
        }
        try fm.createDirectory(at: binDir, withIntermediateDirectories: true)
        try fm.createSymbolicLink(at: link, withDestinationURL: target)
        return .installed
    }

    static func isOnPath(_ dir: URL, path: String = ProcessInfo.processInfo.environment["PATH"] ?? "") -> Bool {
        path.split(separator: ":").contains { URL(fileURLWithPath: String($0)).standardizedFileURL.path == dir.standardizedFileURL.path }
    }

    static func display(_ url: URL) -> String {
        url.path.replacingOccurrences(of: NSHomeDirectory(), with: "~")
    }
}
