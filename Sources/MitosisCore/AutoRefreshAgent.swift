import Foundation

/// A per-user LaunchAgent that refreshes clones when their original app changes, even while Mitosis is closed.
/// launchd watches the originals' folders (no CPU while waiting) and runs `mitosis refresh --outdated --quiet`,
/// plus every 6 hours as a fallback.
public enum AutoRefreshAgent {
    public static let label = "com.mitosis-mac.autorefresh"

    public static var defaultAgentsDir: URL {
        FileManager.default.homeDirectoryForCurrentUser.appendingPathComponent("Library/LaunchAgents")
    }

    public static var defaultLogFile: URL {
        FileManager.default.homeDirectoryForCurrentUser.appendingPathComponent("Library/Logs/Mitosis/autorefresh.log")
    }

    /// Each original app's folder (updaters usually swap the whole bundle) and its Info.plist, once each.
    public static func watchPaths(for entries: [RegistryEntry]) -> [String] {
        var paths = Set<String>()
        for e in entries {
            let app = URL(fileURLWithPath: e.manifest.source.path)
            paths.insert(app.deletingLastPathComponent().path)
            paths.insert(app.appendingPathComponent("Contents/Info.plist").path)
        }
        return paths.sorted()
    }

    public static func plist(cli: URL, watchPaths: [String], logFile: URL = defaultLogFile) -> Data {
        let dict: [String: Any] = [
            "Label": label,
            "ProgramArguments": [cli.path, "refresh", "--outdated", "--quiet"],
            "WatchPaths": watchPaths,
            "StartInterval": 21_600,
            "ThrottleInterval": 30,
            "RunAtLoad": false,
            "ProcessType": "Background",
            "LowPriorityIO": true,
            "Nice": 10,
            "StandardErrorPath": logFile.path,
            "StandardOutPath": logFile.path,
        ]
        return (try? PropertyListSerialization.data(fromPropertyList: dict, format: .xml, options: 0)) ?? Data()
    }

    /// Installs, updates or removes the agent to match the current clones and setting. Returns true if it changed.
    /// `load: false` (tests) skips launchctl.
    @discardableResult
    public static func sync(cli: URL, entries: [RegistryEntry], enabled: Bool,
                            agentsDir: URL = defaultAgentsDir, load: Bool = true) throws -> Bool {
        let fm = FileManager.default
        let file = agentsDir.appendingPathComponent("\(label).plist")
        guard enabled, !entries.isEmpty else {
            guard fm.fileExists(atPath: file.path) else { return false }
            if load { unload() }
            try fm.removeItem(at: file)
            return true
        }
        let data = plist(cli: cli, watchPaths: watchPaths(for: entries))
        if (try? Data(contentsOf: file)) == data { return false }
        try fm.createDirectory(at: agentsDir, withIntermediateDirectories: true)
        try fm.createDirectory(at: defaultLogFile.deletingLastPathComponent(), withIntermediateDirectories: true)
        try data.write(to: file, options: .atomic)
        if load {
            unload()
            try Shell.run("/bin/launchctl", ["bootstrap", "gui/\(getuid())", file.path])
        }
        return true
    }

    static func unload() {
        _ = try? Shell.run("/bin/launchctl", ["bootout", "gui/\(getuid())/\(label)"], check: false)
    }
}
