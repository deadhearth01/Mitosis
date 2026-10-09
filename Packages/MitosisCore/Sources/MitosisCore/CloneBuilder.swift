import Foundation

public struct CloneRequest: Sendable {
    public var source: URL
    public var name: String
    public var badge: Badge
    public var modeOverride: CloneMode?

    public init(source: URL, name: String, badge: Badge, modeOverride: CloneMode? = nil) {
        self.source = source
        self.name = name
        self.badge = badge
        self.modeOverride = modeOverride
    }
}

public enum CloneError: Error, Equatable, CustomStringConvertible, Sendable {
    case invalidName(CloneNameError)
    case unsupported([String])
    case nameTaken(String)
    case sourceMissing(String)
    case cloneRunning(String)
    case notFound(String)
    case relinkMismatch(expected: String, found: String)
    case failed(step: String, message: String)

    public var description: String {
        switch self {
        case .invalidName(let e): return e.description
        case .unsupported(let reasons): return "This app can't be cloned. " + reasons.joined(separator: " ")
        case .nameTaken(let n): return "A clone named \"\(n)\" already exists."
        case .sourceMissing(let p): return "The original app isn't at \(p) anymore."
        case .cloneRunning(let n): return "\"\(n)\" is running. Quit it first."
        case .notFound(let q): return "No clone matches \"\(q)\"."
        case let .relinkMismatch(expected, found): return "That app is \(found), but this clone was made from \(expected)."
        case let .failed(step, message): return "Cloning failed while \(Self.stepLabel(step)): \(message)"
        }
    }

    static func stepLabel(_ step: String) -> String {
        switch step {
        case "copy": return "copying the app"
        case "helpers": return "renaming the app's helpers"
        case "stub": return "setting up separate data"
        case "icon": return "drawing the icon"
        case "plist": return "giving the clone its own identity"
        case "manifest": return "saving clone details"
        case "sign": return "signing the clone"
        case "verify": return "checking the clone"
        default: return step
        }
    }
}

struct BuildPlan: Sendable {
    var info: AppInfo
    var mode: CloneMode
    var id: UUID
    var bundleID: String
    var name: String
    var badge: Badge
    var dataPath: String
    var launch: LaunchSettings
    var profile: CloneManifest.ProfileRef?
    var createdAt: Date
    var refreshedAt: Date

    var manifest: CloneManifest {
        CloneManifest(id: id, name: name, badge: badge, mode: mode, cloneBundleID: bundleID,
                      source: .init(path: info.url.path, bundleID: info.bundleID, version: info.version, cdhash: info.cdhash),
                      profile: profile, dataPath: dataPath, createdAt: createdAt, refreshedAt: refreshedAt)
    }
}

public struct CloneBuilder: Sendable {
    public let environment: MitosisEnvironment
    public let profiles: ProfileStore
    /// Test hook: throw right after the named step succeeds.
    var failAfterStep: String?

    public init(environment: MitosisEnvironment, profiles: ProfileStore) {
        self.environment = environment
        self.profiles = profiles
    }

    public func create(_ request: CloneRequest) throws -> RegistryEntry {
        let name: String
        do { name = try CloneName.validate(request.name) } catch let e as CloneNameError { throw CloneError.invalidName(e) }
        let fm = FileManager.default
        guard fm.fileExists(atPath: request.source.path) else { throw CloneError.sourceMissing(request.source.path) }

        let info = try AppInspector().inspect(request.source)
        let decision = ModeDecider(profiles: profiles).decide(for: info)
        guard decision.support != .unsupported, let decided = decision.mode else { throw CloneError.unsupported(decision.reasons) }
        let mode = request.modeOverride ?? decided

        let registry = CloneRegistry(fileURL: environment.registryFile)
        let entries = try registry.loadOrRebuild(clonesDir: environment.clonesDir)
        guard !fm.fileExists(atPath: environment.clonesDir.appendingPathComponent("\(name).app").path) else {
            throw CloneError.nameTaken(name)
        }
        let prefix = info.bundleID + ".mitosis."
        let usedSlugs = Set(entries.map(\.manifest.cloneBundleID).filter { $0.hasPrefix(prefix) }.map { String($0.dropFirst(prefix.count)) })
        let slug = Slug.make(from: name, existing: usedSlugs)

        let id = UUID()
        let dataURL = dataFolder(for: id)
        let profile = profiles.profile(forBundleID: info.bundleID)
        let now = Date.mitosisNow
        let plan = BuildPlan(info: info, mode: mode, id: id, bundleID: prefix + slug, name: name, badge: request.badge,
                             dataPath: dataURL.path, launch: launchSettings(for: info, mode: mode, profile: profile),
                             profile: profile.map { .init(id: $0.id, version: $0.version) }, createdAt: now, refreshedAt: now)

        try fm.createDirectory(at: dataURL, withIntermediateDirectories: true)
        do {
            let url = try install(plan, replacing: nil)
            let entry = RegistryEntry(manifest: plan.manifest, bundlePath: url.path)
            try registry.upsert(entry)
            return entry
        } catch {
            try? fm.removeItem(at: dataURL)
            throw error
        }
    }

    /// Short (8 hex chars) so Unix socket paths inside it stay under macOS's 103-character limit.
    func dataFolder(for id: UUID) -> URL {
        let short = environment.dataRoot.appendingPathComponent(String(id.uuidString.prefix(8)).lowercased())
        if !FileManager.default.fileExists(atPath: short.path) { return short }
        return environment.dataRoot.appendingPathComponent(id.uuidString.lowercased())
    }

    func launchSettings(for info: AppInfo, mode: CloneMode, profile: AppProfile?) -> LaunchSettings {
        let settings = profile?.launch ?? DefaultLaunchSettings.forFrameworks(info.frameworks)
        if mode == .fallback && settings.isEmpty { return DefaultLaunchSettings.fallback }
        return settings
    }

    /// Builds into a hidden temp bundle next to the destination, verifies it, then moves (or replaces) atomically.
    func install(_ plan: BuildPlan, replacing existing: URL?) throws -> URL {
        let fm = FileManager.default
        try fm.createDirectory(at: environment.clonesDir, withIntermediateDirectories: true)
        let temp = environment.clonesDir.appendingPathComponent(".mitosis-tmp-\(UUID().uuidString).app")
        do {
            switch plan.mode {
            case .identity: try buildIdentity(plan, at: temp)
            case .fallback: try buildFallback(plan, at: temp)
            }
            try step("verify") { try Signer.verify(temp) }
            let final: URL
            if let existing {
                _ = try fm.replaceItemAt(existing, withItemAt: temp)
                final = existing
            } else {
                final = environment.clonesDir.appendingPathComponent("\(plan.name).app")
                try fm.moveItem(at: temp, to: final)
            }
            if environment.registerWithLaunchServices { try LaunchServices.register(final) }
            return final
        } catch {
            try? fm.removeItem(at: temp)
            throw error
        }
    }

    func step(_ name: String, _ body: () throws -> Void) throws {
        do {
            try body()
        } catch let e as CloneError {
            throw e
        } catch {
            throw CloneError.failed(step: name, message: String(describing: error))
        }
        if failAfterStep == name { throw CloneError.failed(step: name, message: "injected test failure") }
    }

    func buildIdentity(_ p: BuildPlan, at temp: URL) throws {
        let fm = FileManager.default
        let contents = temp.appendingPathComponent("Contents")
        let macOS = contents.appendingPathComponent("MacOS")
        let resources = contents.appendingPathComponent("Resources")
        let infoPlist = contents.appendingPathComponent("Info.plist")
        var extraExecutables: [URL] = []
        var renamedHelpers: [URL] = []

        try step("copy") {
            try FileCloner.cloneOrCopy(from: p.info.url, to: temp)
            try Shell.run("/bin/chmod", ["-R", "u+w", temp.path])
        }
        try step("helpers") {
            guard p.info.frameworks.contains(.electron) else { return }
            let oldName = (try InfoPlistEditor.read(infoPlist)["CFBundleName"] as? String) ?? p.info.executableName
            renamedHelpers = try HelperRenamer.rename(in: temp, from: oldName, to: p.name)
        }
        try step("stub") {
            guard !p.launch.isEmpty else { return }
            let exe = macOS.appendingPathComponent(p.info.executableName)
            let real = macOS.appendingPathComponent(p.info.executableName + ".mitosis-real")
            try fm.moveItem(at: exe, to: real)
            try fm.copyItem(at: environment.stubBinary, to: exe)
            try LaunchConfig.resolve(p.launch, kind: .exec, target: real.lastPathComponent, dataPath: p.dataPath)
                .write(toResources: resources)
            extraExecutables = [real]
        }
        try step("icon") { try writeIcon(p, resources: resources) }
        try step("plist") {
            try InfoPlistEditor.apply(InfoPlistEdit(bundleID: p.bundleID, name: p.name, iconFile: IconRenderer.iconFileBaseName,
                                                    disableSparkle: p.info.frameworks.contains(.sparkle)),
                                      to: infoPlist)
        }
        try step("manifest") { try writeManifest(p, resources: resources) }
        try step("sign") {
            let entitlements = Entitlements.sanitized(try Entitlements.read(from: p.info.url))
            try Signer.signModified(bundle: temp, entitlements: entitlements, identifier: p.bundleID,
                                    helperBundles: renamedHelpers, extraExecutables: extraExecutables)
        }
    }

    /// A small shortcut app (own name, badge icon and bundle ID) whose stub opens the original app as a new
    /// instance with the clone's data settings. Safe for any app, but keeps Parall-style limitations.
    func buildFallback(_ p: BuildPlan, at temp: URL) throws {
        let fm = FileManager.default
        let contents = temp.appendingPathComponent("Contents")
        let macOS = contents.appendingPathComponent("MacOS")
        let resources = contents.appendingPathComponent("Resources")
        let executable = "MitosisShortcut"

        try step("copy") {
            try fm.createDirectory(at: macOS, withIntermediateDirectories: true)
            try fm.createDirectory(at: resources, withIntermediateDirectories: true)
            try fm.copyItem(at: environment.stubBinary, to: macOS.appendingPathComponent(executable))
        }
        try step("stub") {
            try LaunchConfig.resolve(p.launch, kind: .open, target: p.info.url.path, dataPath: p.dataPath)
                .write(toResources: resources)
        }
        try step("icon") { try writeIcon(p, resources: resources) }
        try step("plist") {
            try InfoPlistEditor.write([
                "CFBundleIdentifier": p.bundleID,
                "CFBundleName": p.name,
                "CFBundleDisplayName": p.name,
                "CFBundleExecutable": executable,
                "CFBundlePackageType": "APPL",
                "CFBundleInfoDictionaryVersion": "6.0",
                "CFBundleIconFile": IconRenderer.iconFileBaseName,
                "CFBundleShortVersionString": p.info.version,
                "CFBundleVersion": p.info.build,
                "LSMinimumSystemVersion": "15.0",
                "LSUIElement": true,
            ], to: contents.appendingPathComponent("Info.plist"))
        }
        try step("manifest") { try writeManifest(p, resources: resources) }
        try step("sign") { try Signer.signModified(bundle: temp, entitlements: [:], identifier: p.bundleID) }
    }

    func writeIcon(_ p: BuildPlan, resources: URL) throws {
        guard let base = IconRenderer.baseIcon(forApp: p.info.url) else { throw IconError.noBaseIcon(p.info.name) }
        try FileManager.default.createDirectory(at: resources, withIntermediateDirectories: true)
        try IconRenderer.icnsData(base: base, badge: p.badge)
            .write(to: resources.appendingPathComponent("\(IconRenderer.iconFileBaseName).icns"))
    }

    func writeManifest(_ p: BuildPlan, resources: URL) throws {
        try FileManager.default.createDirectory(at: resources, withIntermediateDirectories: true)
        try CloneManifest.encoder().encode(p.manifest)
            .write(to: resources.appendingPathComponent(CloneRegistry.manifestFileName))
    }
}
