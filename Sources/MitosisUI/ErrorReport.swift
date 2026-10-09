import Foundation
import MitosisCore

/// A failure explained in plain words, plus details to copy and a prefilled GitHub issue (no personal paths).
struct ErrorReport: Equatable, Sendable {
    var message: String
    var details: String
    var issueURL: URL

    static let issuesURL = URL(string: "https://github.com/deadhearth01/Mitosis/issues/new")!

    static func make(error: Error, appName: String?, appVersion: String?, mode: CloneMode?) -> ErrorReport {
        let raw = (error as? CloneError)?.description ?? (error as NSError).localizedDescription
        let message = sanitize(raw)
        let os = ProcessInfo.processInfo.operatingSystemVersion
        let macOS = "\(os.majorVersion).\(os.minorVersion)" + (os.patchVersion > 0 ? ".\(os.patchVersion)" : "")
        let app = [appName, appVersion].compactMap { $0 }.joined(separator: " ")
        var lines = ["Mitosis: \(Mitosis.version)", "macOS: \(macOS)"]
        if !app.isEmpty { lines.append("App: \(app)") }
        if let mode { lines.append("Mode: \(mode.displayName)") }
        lines.append("Error: \(sanitize(String(describing: error)))")

        var components = URLComponents(url: issuesURL, resolvingAgainstBaseURL: false)!
        components.queryItems = [
            URLQueryItem(name: "template", value: "bug_report.yml"),
            URLQueryItem(name: "title", value: app.isEmpty ? "Problem: \(message.prefix(60))" : "Couldn't clone \(appName ?? app)"),
            URLQueryItem(name: "what", value: message + "\n\n```\n" + lines.joined(separator: "\n") + "\n```"),
            URLQueryItem(name: "app", value: app),
            URLQueryItem(name: "macos", value: macOS),
            URLQueryItem(name: "version", value: Mitosis.version),
        ]
        return ErrorReport(message: message, details: lines.joined(separator: "\n"), issueURL: components.url ?? issuesURL)
    }

    /// Replaces the home folder with "~" so reports never include the user's account name.
    static func sanitize(_ text: String) -> String {
        text.replacingOccurrences(of: NSHomeDirectory(), with: "~")
    }
}

extension CloneMode {
    var displayName: String {
        switch self {
        case .identity: return "Full clone"
        case .fallback: return "Compatibility mode"
        }
    }
}

extension SupportLevel {
    var displayName: String {
        switch self {
        case .full: return "Works great"
        case .limited: return "Works with limits"
        case .unsupported: return "Not supported"
        }
    }
}
