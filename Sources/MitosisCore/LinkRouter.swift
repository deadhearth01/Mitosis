import Foundation

/// Custom URL schemes an app registers (for example `slack`), as used by browser sign-in callbacks.
public enum URLSchemes {
    /// Web and system schemes Mitosis never takes over.
    public static let ignored: Set<String> = [
        "http", "https", "file", "mailto", "ftp", "sftp", "ssh", "tel", "sms", "facetime", "data", "about", "javascript",
    ]

    public static func declared(byAppAt app: URL) -> [String] {
        guard let info = try? AppInspector.readInfoPlist(app),
              let types = info["CFBundleURLTypes"] as? [[String: Any]] else { return [] }
        let schemes = types.flatMap { ($0["CFBundleURLSchemes"] as? [String]) ?? [] }.map { $0.lowercased() }
        return Array(Set(schemes).subtracting(ignored)).sorted()
    }
}

/// Which original apps have sign-in link routing turned on, and who handled their links before.
public struct LinkRouterState: Codable, Equatable, Sendable {
    public struct App: Codable, Equatable, Sendable {
        public var sourcePath: String
        public var schemes: [String]
        /// Scheme → bundle ID of the app that handled it before Mitosis, restored when routing is turned off.
        public var previousHandlers: [String: String]

        public init(sourcePath: String, schemes: [String], previousHandlers: [String: String]) {
            self.sourcePath = sourcePath
            self.schemes = schemes
            self.previousHandlers = previousHandlers
        }
    }

    /// Keyed by the original app's bundle ID.
    public var apps: [String: App] = [:]

    public init() {}

    public var allSchemes: [String] { Array(Set(apps.values.flatMap(\.schemes))).sorted() }

    public static func load(from file: URL) -> LinkRouterState {
        guard let data = try? Data(contentsOf: file) else { return LinkRouterState() }
        return (try? JSONDecoder().decode(LinkRouterState.self, from: data)) ?? LinkRouterState()
    }

    public func save(to file: URL) throws {
        try FileManager.default.createDirectory(at: file.deletingLastPathComponent(), withIntermediateDirectories: true)
        let encoder = JSONEncoder()
        encoder.outputFormatting = [.prettyPrinted, .sortedKeys]
        try encoder.encode(self).write(to: file, options: .atomic)
    }
}

/// An app a sign-in link can be delivered to: the original or one of its full clones.
public struct LinkTarget: Equatable, Sendable {
    public var name: String
    public var bundleURL: URL
    public var bundleID: String
    public var isClone: Bool
    public var isRunning: Bool

    public init(name: String, bundleURL: URL, bundleID: String, isClone: Bool, isRunning: Bool) {
        self.name = name
        self.bundleURL = bundleURL
        self.bundleID = bundleID
        self.isClone = isClone
        self.isRunning = isRunning
    }
}

public enum LinkRouter {
    /// The original app (if it's still there) and its full clones, for the app that registered this link's scheme.
    /// Compatibility-mode clones run as the original app, so they can't receive their own links.
    public static func targets(for url: URL, entries: [RegistryEntry], state: LinkRouterState,
                               isRunning: (String) -> Bool) -> [LinkTarget] {
        guard let scheme = url.scheme?.lowercased(),
              let (sourceID, app) = state.apps.first(where: { $0.value.schemes.contains(scheme) }) else { return [] }
        var targets: [LinkTarget] = []
        let original = URL(fileURLWithPath: app.sourcePath)
        if FileManager.default.fileExists(atPath: original.path) {
            targets.append(LinkTarget(name: original.deletingPathExtension().lastPathComponent, bundleURL: original,
                                      bundleID: sourceID, isClone: false, isRunning: isRunning(sourceID)))
        }
        let clones = entries
            .filter { $0.manifest.source.bundleID == sourceID && $0.manifest.mode == .identity }
            .sorted { $0.manifest.name.localizedStandardCompare($1.manifest.name) == .orderedAscending }
        for e in clones {
            targets.append(LinkTarget(name: e.manifest.name, bundleURL: e.bundleURL, bundleID: e.manifest.cloneBundleID,
                                      isClone: true, isRunning: isRunning(e.manifest.cloneBundleID)))
        }
        return targets
    }

    /// Delivers without asking when only one app could have started the sign-in; nil means ask.
    public static func autoTarget(_ targets: [LinkTarget]) -> LinkTarget? {
        let running = targets.filter(\.isRunning)
        if running.count == 1 { return running[0] }
        if running.isEmpty && targets.count == 1 { return targets[0] }
        return nil
    }
}
