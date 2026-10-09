import Foundation

/// An installed app the user could clone, read from its Info.plist only (cheap enough to list everything).
public struct CatalogApp: Identifiable, Hashable, Sendable {
    public var url: URL
    /// The name Finder shows, e.g. "Visual Studio Code".
    public var name: String
    public var bundleID: String
    public var version: String
    public var lastUsed: Date?
    public var id: String { url.path }

    public init(url: URL, name: String, bundleID: String, version: String, lastUsed: Date?) {
        self.url = url
        self.name = name
        self.bundleID = bundleID
        self.version = version
        self.lastUsed = lastUsed
    }
}

public enum AppCatalog {
    public static let defaultDirs: [URL] = [
        URL(fileURLWithPath: "/Applications"),
        URL(fileURLWithPath: "/Applications/Utilities"),
        FileManager.default.homeDirectoryForCurrentUser.appendingPathComponent("Applications"),
        URL(fileURLWithPath: "/Applications/Setapp"),
    ]

    /// Apps at the top level of each folder and one folder deep (e.g. "/Applications/Adobe Photoshop/…"),
    /// excluding Mitosis clones and Mitosis itself, sorted by name.
    public static func scan(_ dirs: [URL] = defaultDirs) -> [CatalogApp] {
        let fm = FileManager.default
        var seen = Set<String>()
        var result: [CatalogApp] = []
        func consider(_ url: URL) {
            let path = url.standardizedFileURL.path
            guard seen.insert(path).inserted else { return }
            guard !fm.fileExists(atPath: url.appendingPathComponent("Contents/Resources/\(CloneRegistry.manifestFileName)").path),
                  let info = try? AppInspector.readInfoPlist(url),
                  let bundleID = info["CFBundleIdentifier"] as? String, bundleID != Mitosis.bundleID
            else { return }
            let lastUsed = (try? url.resourceValues(forKeys: [.contentAccessDateKey]))?.contentAccessDate
            result.append(CatalogApp(url: url, name: url.deletingPathExtension().lastPathComponent, bundleID: bundleID,
                                     version: info["CFBundleShortVersionString"] as? String ?? "", lastUsed: lastUsed))
        }
        for dir in dirs {
            for item in (try? fm.contentsOfDirectory(at: dir, includingPropertiesForKeys: [.isDirectoryKey])) ?? [] {
                if item.pathExtension == "app" {
                    consider(item)
                } else if (try? item.resourceValues(forKeys: [.isDirectoryKey]))?.isDirectory == true {
                    for inner in (try? fm.contentsOfDirectory(at: item, includingPropertiesForKeys: nil)) ?? [] where inner.pathExtension == "app" {
                        consider(inner)
                    }
                }
            }
        }
        return result.sorted { $0.name.localizedStandardCompare($1.name) == .orderedAscending }
    }
}
