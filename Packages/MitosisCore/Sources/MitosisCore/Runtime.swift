import Foundation

public enum RunningMonitor {
    /// True if any process for this bundle ID has checked in with macOS (GUI apps).
    /// Fallback-mode clones run as the original app, so they are never reported as running.
    public static func isRunning(bundleID: String) -> Bool {
        guard let r = try? Shell.run("/usr/bin/lsappinfo", ["find", "bundleid=\(bundleID)"], check: false) else { return false }
        return !r.stdout.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
    }
}

public enum CloneLauncher {
    public static func open(_ app: URL) throws {
        try Shell.run("/usr/bin/open", [app.path])
    }

    /// Opens the app, waits `window`, and reports whether it is still running (false = it quit or crashed).
    public static func openAndCheck(_ app: URL, bundleID: String, window: Duration = .seconds(10)) async throws -> Bool {
        try open(app)
        try await Task.sleep(for: window)
        return RunningMonitor.isRunning(bundleID: bundleID)
    }
}

public enum AppResolver {
    public static let defaultSearchDirs: [URL] = [
        URL(fileURLWithPath: "/Applications"),
        FileManager.default.homeDirectoryForCurrentUser.appendingPathComponent("Applications"),
        URL(fileURLWithPath: "/Applications/Setapp"),
    ]

    /// Accepts a path, an app name ("Slack" / "Slack.app"), or a bundle ID.
    public static func resolve(_ query: String, searchDirs: [URL] = defaultSearchDirs) -> URL? {
        let fm = FileManager.default
        if query.contains("/") {
            let url = URL(fileURLWithPath: (query as NSString).expandingTildeInPath)
            return fm.fileExists(atPath: url.path) ? url : nil
        }
        let lower = query.lowercased()
        let wanted = lower.hasSuffix(".app") ? lower : lower + ".app"
        for dir in searchDirs {
            let items = (try? fm.contentsOfDirectory(atPath: dir.path)) ?? []
            if let hit = items.first(where: { $0.lowercased() == wanted }) { return dir.appendingPathComponent(hit) }
        }
        for dir in searchDirs {
            let items = (try? fm.contentsOfDirectory(atPath: dir.path)) ?? []
            for item in items where item.hasSuffix(".app") {
                let url = dir.appendingPathComponent(item)
                if let info = try? AppInspector.readInfoPlist(url),
                   (info["CFBundleIdentifier"] as? String)?.lowercased() == lower {
                    return url
                }
            }
        }
        return nil
    }
}
