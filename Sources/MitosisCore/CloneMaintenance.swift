import Foundation

public enum CloneStatus: Equatable, Sendable {
    case upToDate
    case updateAvailable(currentVersion: String)
    case originalMissing
}

public struct CloneMaintenance: Sendable {
    public let builder: CloneBuilder
    public let isRunning: @Sendable (CloneManifest) -> Bool

    public init(builder: CloneBuilder, isRunning: @escaping @Sendable (CloneManifest) -> Bool = RunningMonitor.isRunning(clone:)) {
        self.builder = builder
        self.isRunning = isRunning
    }

    private var registry: CloneRegistry { CloneRegistry(fileURL: builder.environment.registryFile) }

    public func status(of entry: RegistryEntry) -> CloneStatus {
        let source = URL(fileURLWithPath: entry.manifest.source.path)
        guard let info = try? AppInspector().inspect(source), info.bundleID == entry.manifest.source.bundleID else {
            return .originalMissing
        }
        if let old = entry.manifest.source.cdhash, let new = info.cdhash {
            return old == new ? .upToDate : .updateAvailable(currentVersion: info.version)
        }
        return info.version == entry.manifest.source.version ? .upToDate : .updateAvailable(currentVersion: info.version)
    }

    /// Rebuilds the clone from the current original, keeping its ID, bundle ID, name, badge and data.
    public func refresh(_ entry: RegistryEntry) throws -> RegistryEntry {
        try rebuild(entry, badge: entry.manifest.badge)
    }

    /// Gives the clone a new badge (text and color). The name stays: Electron apps tie their saved logins to the
    /// app name, so renaming would sign people out.
    public func restyle(_ entry: RegistryEntry, badge: Badge) throws -> RegistryEntry {
        try rebuild(entry, badge: badge)
    }

    private func rebuild(_ entry: RegistryEntry, badge: Badge) throws -> RegistryEntry {
        let m = entry.manifest
        guard !isRunning(m) else { throw CloneError.cloneRunning(m.name) }
        let source = URL(fileURLWithPath: m.source.path)
        guard FileManager.default.fileExists(atPath: source.path) else { throw CloneError.sourceMissing(source.path) }
        let info = try AppInspector().inspect(source)
        let profile = builder.profiles.profile(forBundleID: info.bundleID)
        let plan = BuildPlan(info: info, mode: m.mode, id: m.id, bundleID: m.cloneBundleID, name: m.name, badge: badge,
                             dataPath: m.dataPath, launch: builder.launchSettings(for: info, mode: m.mode, profile: profile),
                             profile: profile.map { .init(id: $0.id, version: $0.version) },
                             createdAt: m.createdAt, refreshedAt: .mitosisNow)
        let url = try builder.install(plan, replacing: entry.bundleURL)
        let updated = RegistryEntry(manifest: plan.manifest, bundlePath: url.path)
        try registry.upsert(updated)
        return updated
    }

    /// Moves the clone (and optionally its data) to the Trash. Never deletes permanently.
    public func delete(_ entry: RegistryEntry, deleteData: Bool) throws {
        let m = entry.manifest
        guard !isRunning(m) else { throw CloneError.cloneRunning(m.name) }
        let fm = FileManager.default
        let trashDir = builder.environment.trashOverride
        if builder.environment.registerWithLaunchServices { LaunchServices.unregister(entry.bundleURL) }
        if fm.fileExists(atPath: entry.bundlePath) { try Trash.move(entry.bundleURL, overrideDir: trashDir) }
        if deleteData {
            let home = fm.homeDirectoryForCurrentUser
            let candidates = [
                URL(fileURLWithPath: m.dataPath),
                home.appendingPathComponent("Library/Containers/\(m.cloneBundleID)"),
                home.appendingPathComponent("Library/Preferences/\(m.cloneBundleID).plist"),
            ]
            for url in candidates where fm.fileExists(atPath: url.path) {
                try Trash.move(url, overrideDir: trashDir)
            }
        }
        try registry.remove(id: entry.id)
    }

    public func relink(_ entry: RegistryEntry, to newSource: URL) throws -> RegistryEntry {
        let info = try AppInspector().inspect(newSource)
        guard info.bundleID == entry.manifest.source.bundleID else {
            throw CloneError.relinkMismatch(expected: entry.manifest.source.bundleID, found: info.bundleID)
        }
        var updated = entry
        updated.manifest.source.path = newSource.path
        try registry.upsert(updated)
        return updated
    }
}
