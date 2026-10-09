import Foundation

public enum RunningMonitor {
    /// True if any process for this bundle ID has checked in with macOS (GUI apps).
    public static func isRunning(bundleID: String) -> Bool {
        guard let r = try? Shell.run("/usr/bin/lsappinfo", ["find", "bundleid=\(bundleID)"], check: false) else { return false }
        return !r.stdout.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
    }
}
