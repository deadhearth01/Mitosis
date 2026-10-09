import Foundation

public enum Signer {
    static let codesign = "/usr/bin/codesign"

    /// Ad-hoc signs only what Mitosis modified: renamed helper bundles, then extra executables (with the clone's
    /// identifier and entitlements), then the bundle itself. Unmodified nested code keeps its vendor signature,
    /// so APFS keeps sharing its blocks with the original (a clone costs ~1 MB instead of a full copy).
    /// No hardened runtime: the ad-hoc main executable may load vendor-signed frameworks.
    public static func signModified(bundle: URL, entitlements: [String: Any], identifier: String,
                                    helperBundles: [URL] = [], extraExecutables: [URL] = []) throws {
        var entArgs: [String] = []
        var entFile: URL?
        if !entitlements.isEmpty {
            let f = FileManager.default.temporaryDirectory.appendingPathComponent("mitosis-\(UUID().uuidString).entitlements")
            try Entitlements.xmlData(entitlements).write(to: f)
            entArgs = ["--entitlements", f.path]
            entFile = f
        }
        defer { if let entFile { try? FileManager.default.removeItem(at: entFile) } }

        let base = ["--force", "--sign", "-", "--timestamp=none"]
        for helper in helperBundles.sorted(by: { $0.pathComponents.count > $1.pathComponents.count }) {
            try Shell.run(codesign, base + [helper.path])
        }
        for exe in extraExecutables {
            try Shell.run(codesign, base + ["--identifier", identifier] + entArgs + [exe.path])
        }
        try Shell.run(codesign, base + ["--identifier", identifier] + entArgs + [bundle.path])
    }

    public static func verify(_ bundle: URL) throws {
        try Shell.run(codesign, ["--verify", "--deep", "--strict", bundle.path])
    }
}
