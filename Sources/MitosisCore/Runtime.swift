import Darwin
import Foundation

public enum RunningMonitor {
    /// True if any process for this bundle ID has checked in with macOS (GUI apps).
    public static func isRunning(bundleID: String) -> Bool {
        guard let r = try? Shell.run("/usr/bin/lsappinfo", ["find", "bundleid=\(bundleID)"], check: false) else { return false }
        return !r.stdout.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
    }

    /// Whether this clone is running. Identity clones run under their own bundle ID; fallback clones run as the
    /// original app, so they are tracked through the pid their stub records.
    public static func isRunning(clone m: CloneManifest) -> Bool {
        switch m.mode {
        case .identity: return isRunning(bundleID: m.cloneBundleID)
        case .fallback: return instancePID(of: m) != nil
        }
    }

    /// The pid of a fallback clone's live instance, if any (stale pid files are ignored).
    public static func instancePID(of m: CloneManifest) -> pid_t? {
        let file = CloneBuilder.instancePIDFile(dataPath: m.dataPath)
        guard let text = try? String(contentsOfFile: file, encoding: .utf8),
              let pid = pid_t(text.trimmingCharacters(in: .whitespacesAndNewlines)), pid > 0,
              kill(pid, 0) == 0,
              let path = executablePath(of: pid),
              path.hasPrefix(m.source.path + "/") || path.hasPrefix(URL(fileURLWithPath: m.source.path).resolvingSymlinksInPath().path + "/")
        else { return nil }
        return pid
    }

    /// The executable path of a live process (nil if it isn't running), without spawning `ps`.
    public static func executablePath(of pid: pid_t) -> String? {
        var buffer = [CChar](repeating: 0, count: 4 * Int(MAXPATHLEN))
        guard proc_pidpath(pid, &buffer, UInt32(buffer.count)) > 0 else { return nil }
        return String(decoding: buffer.prefix { $0 != 0 }.map { UInt8(bitPattern: $0) }, as: UTF8.self)
    }

    /// pids of processes whose executable lives inside the bundle (main app + helpers).
    static func pids(inside bundle: URL) -> [pid_t] {
        guard let r = try? Shell.run("/bin/ps", ["-Ao", "pid=,comm="], check: false) else { return [] }
        let prefixes = Set([bundle.path, bundle.resolvingSymlinksInPath().path].map { $0 + "/" })
        return r.stdout.split(separator: "\n").compactMap { line in
            let parts = line.split(separator: " ", maxSplits: 1, omittingEmptySubsequences: true)
            guard parts.count == 2, prefixes.contains(where: { String(parts[1]).hasPrefix($0) }) else { return nil }
            return pid_t(parts[0])
        }
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

    /// Opens a clone and reports whether that clone (not merely the original app) is still running after `window`.
    public static func openAndCheck(_ entry: RegistryEntry, window: Duration = .seconds(10)) async throws -> Bool {
        try open(entry.bundleURL)
        try await Task.sleep(for: window)
        return RunningMonitor.isRunning(clone: entry.manifest)
    }

    /// Asks the clone's processes to quit (SIGTERM) — by pid, never by name matching.
    public static func terminate(_ entry: RegistryEntry) {
        var targets = RunningMonitor.pids(inside: entry.bundleURL)
        if let pid = RunningMonitor.instancePID(of: entry.manifest) { targets.append(pid) }
        for pid in targets { kill(pid, SIGTERM) }
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
