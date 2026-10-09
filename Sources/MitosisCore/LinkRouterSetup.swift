import AppKit
import Synchronization

/// Reads and changes which app handles a URL scheme (the system's default handler).
public protocol URLHandlerRegistry: Sendable {
    func defaultHandler(forScheme scheme: String) -> String?
    func setDefaultHandler(bundleID: String, forScheme scheme: String) throws
}

public enum LinkRouterError: Error, Equatable, CustomStringConvertible, Sendable {
    case noSchemes(String)
    case appNotFound(String)
    case timedOut(String)

    public var description: String {
        switch self {
        case .noSchemes(let app): return "\(app) doesn't use sign-in links, so there's nothing to route."
        case .appNotFound(let id): return "macOS can't find the app \(id)."
        case .timedOut(let scheme): return "macOS didn't respond while changing who opens \(scheme) links."
        }
    }
}

/// The real registry: NSWorkspace (no prompt for custom schemes; confirmed by the 2026-10-09 spike).
public struct WorkspaceURLHandlers: URLHandlerRegistry {
    public init() {}

    public func defaultHandler(forScheme scheme: String) -> String? {
        guard let probe = URL(string: "\(scheme)://"), let app = NSWorkspace.shared.urlForApplication(toOpen: probe) else { return nil }
        return Bundle(url: app)?.bundleIdentifier
    }

    public func setDefaultHandler(bundleID: String, forScheme scheme: String) throws {
        guard let app = NSWorkspace.shared.urlForApplication(withBundleIdentifier: bundleID) else {
            throw LinkRouterError.appNotFound(bundleID)
        }
        let done = DispatchSemaphore(value: 0)
        let failure = Mutex<(any Error)?>(nil)
        NSWorkspace.shared.setDefaultApplication(at: app, toOpenURLsWithScheme: scheme) { error in
            failure.withLock { $0 = error }
            done.signal()
        }
        guard done.wait(timeout: .now() + 15) == .success else { throw LinkRouterError.timedOut(scheme) }
        if let error = failure.withLock({ $0 }) { throw error }
    }
}

/// Builds the small "Mitosis Link Router" app and makes it the handler for opted-in apps' sign-in link schemes.
public struct LinkRouterSetup: Sendable {
    public static let bundleID = "com.mitosis-mac.LinkRouter"
    public static let appName = "Mitosis Link Router"
    public static let registryKey = "MitosisRegistryFile"

    public let environment: MitosisEnvironment
    public let handlers: any URLHandlerRegistry
    /// The `MitosisRouter` executable shipped inside Mitosis.app.
    public let routerExecutable: URL

    public init(environment: MitosisEnvironment, handlers: any URLHandlerRegistry, routerExecutable: URL) {
        self.environment = environment
        self.handlers = handlers
        self.routerExecutable = routerExecutable
    }

    public var stateFile: URL { environment.registryFile.deletingLastPathComponent().appendingPathComponent("link-router.json") }
    /// Inside the clones folder: macOS only accepts link handlers from an Applications folder.
    public var routerBundle: URL { environment.clonesDir.appendingPathComponent(".router/\(Self.appName).app") }
    public var state: LinkRouterState { LinkRouterState.load(from: stateFile) }

    public func isEnabled(sourceBundleID: String) -> Bool { state.apps[sourceBundleID] != nil }

    /// Sends the original app's sign-in links through the router from now on.
    public func enable(sourceApp: URL) throws {
        let info = try AppInspector.readInfoPlist(sourceApp)
        guard let sourceID = info["CFBundleIdentifier"] as? String else { throw InspectionError.notAnApp(sourceApp.path) }
        let schemes = URLSchemes.declared(byAppAt: sourceApp)
        guard !schemes.isEmpty else { throw LinkRouterError.noSchemes(sourceApp.deletingPathExtension().lastPathComponent) }

        var state = self.state
        let known = state.apps[sourceID]?.previousHandlers ?? [:]
        var previous: [String: String] = [:]
        for scheme in schemes {
            let current = handlers.defaultHandler(forScheme: scheme)
            // Never remember the router or a clone as the "previous" handler; the original app is the safe fallback.
            if let current, current != Self.bundleID, !current.contains(".mitosis.") {
                previous[scheme] = known[scheme] ?? current
            } else {
                previous[scheme] = known[scheme] ?? sourceID
            }
        }
        state.apps[sourceID] = .init(sourcePath: sourceApp.path, schemes: schemes, previousHandlers: previous)
        try buildRouter(schemes: state.allSchemes)
        try state.save(to: stateFile)
        for scheme in schemes where handlers.defaultHandler(forScheme: scheme) != Self.bundleID {
            try handlers.setDefaultHandler(bundleID: Self.bundleID, forScheme: scheme)
        }
    }

    /// Gives the app's links back to whoever handled them before, and removes the router when nothing is left.
    public func disable(sourceBundleID: String) throws {
        var state = self.state
        guard let app = state.apps.removeValue(forKey: sourceBundleID) else { return }
        let stillRouted = Set(state.allSchemes)
        for scheme in app.schemes where !stillRouted.contains(scheme) {
            let previous = app.previousHandlers[scheme] ?? sourceBundleID
            if (try? handlers.setDefaultHandler(bundleID: previous, forScheme: scheme)) == nil, previous != sourceBundleID {
                try? handlers.setDefaultHandler(bundleID: sourceBundleID, forScheme: scheme)
            }
        }
        if state.apps.isEmpty {
            removeRouter()
            try? FileManager.default.removeItem(at: stateFile)
        } else {
            try buildRouter(schemes: state.allSchemes)
            try state.save(to: stateFile)
        }
    }

    /// Keeps routing consistent: drops apps that no longer have a full clone, rebuilds a missing or outdated router,
    /// and takes back schemes another app claimed.
    public func sync(entries: [RegistryEntry]) throws {
        let withClones = Set(entries.filter { $0.manifest.mode == .identity }.map(\.manifest.source.bundleID))
        for sourceID in state.apps.keys where !withClones.contains(sourceID) {
            try disable(sourceBundleID: sourceID)
        }
        let state = self.state
        guard !state.apps.isEmpty else { return }
        if routerNeedsRebuild(schemes: state.allSchemes) { try buildRouter(schemes: state.allSchemes) }
        for scheme in state.allSchemes where handlers.defaultHandler(forScheme: scheme) != Self.bundleID {
            try handlers.setDefaultHandler(bundleID: Self.bundleID, forScheme: scheme)
        }
    }

    // MARK: Router bundle

    func routerNeedsRebuild(schemes: [String]) -> Bool {
        let exe = routerBundle.appendingPathComponent("Contents/MacOS/\(Self.appName)")
        guard let info = try? InfoPlistEditor.read(routerBundle.appendingPathComponent("Contents/Info.plist")),
              let types = info["CFBundleURLTypes"] as? [[String: Any]],
              (types.first?["CFBundleURLSchemes"] as? [String]) == schemes,
              info["CFBundleShortVersionString"] as? String == Mitosis.version,
              FileManager.default.contentsEqual(atPath: exe.path, andPath: routerExecutable.path)
        else { return true }
        return false
    }

    func buildRouter(schemes: [String]) throws {
        let fm = FileManager.default
        let parent = routerBundle.deletingLastPathComponent()
        try fm.createDirectory(at: parent, withIntermediateDirectories: true)
        let temp = parent.appendingPathComponent(".building-\(UUID().uuidString).app")
        defer { try? fm.removeItem(at: temp) }
        let macOS = temp.appendingPathComponent("Contents/MacOS")
        try fm.createDirectory(at: macOS, withIntermediateDirectories: true)
        try fm.copyItem(at: routerExecutable, to: macOS.appendingPathComponent(Self.appName))
        try InfoPlistEditor.write([
            "CFBundleIdentifier": Self.bundleID,
            "CFBundleName": Self.appName,
            "CFBundleDisplayName": Self.appName,
            "CFBundleExecutable": Self.appName,
            "CFBundlePackageType": "APPL",
            "CFBundleInfoDictionaryVersion": "6.0",
            "CFBundleShortVersionString": Mitosis.version,
            "CFBundleVersion": Mitosis.version,
            "LSMinimumSystemVersion": "15.0",
            "LSUIElement": true,
            "CFBundleURLTypes": [["CFBundleURLName": "Mitosis sign-in links", "CFBundleURLSchemes": schemes]],
            // Where the router finds the clones (macOS doesn't pass environment variables to apps it opens).
            Self.registryKey: environment.registryFile.path,
        ], to: temp.appendingPathComponent("Contents/Info.plist"))
        try Shell.run("/usr/bin/codesign", ["--force", "--sign", "-", temp.path])
        if fm.fileExists(atPath: routerBundle.path) {
            _ = try fm.replaceItemAt(routerBundle, withItemAt: temp)
        } else {
            try fm.moveItem(at: temp, to: routerBundle)
        }
        if environment.registerWithLaunchServices { try? LaunchServices.register(routerBundle) }
    }

    func removeRouter() {
        if environment.registerWithLaunchServices { LaunchServices.unregister(routerBundle) }
        try? FileManager.default.removeItem(at: routerBundle)
    }
}
