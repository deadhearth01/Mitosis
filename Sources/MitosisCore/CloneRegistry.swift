import Foundation

public struct CloneRegistry: Sendable {
    public static let manifestFileName = "mitosis.json"
    public let fileURL: URL

    public init(fileURL: URL) {
        self.fileURL = fileURL
    }

    public func load() throws -> [RegistryEntry] {
        guard FileManager.default.fileExists(atPath: fileURL.path) else { return [] }
        return try CloneManifest.decoder().decode([RegistryEntry].self, from: Data(contentsOf: fileURL))
    }

    public func save(_ entries: [RegistryEntry]) throws {
        try FileManager.default.createDirectory(at: fileURL.deletingLastPathComponent(), withIntermediateDirectories: true)
        try CloneManifest.encoder().encode(entries).write(to: fileURL, options: .atomic)
    }

    public func upsert(_ entry: RegistryEntry) throws {
        var entries = try load()
        if let i = entries.firstIndex(where: { $0.id == entry.id }) { entries[i] = entry } else { entries.append(entry) }
        try save(entries)
    }

    public func remove(id: UUID) throws {
        try save(try load().filter { $0.id != id })
    }

    public func rebuild(scanning clonesDir: URL) throws -> [RegistryEntry] {
        let entries = Self.scan(clonesDir)
        try save(entries)
        return entries
    }

    /// Clones found in the folder, read from the manifests embedded in them (hidden temp/backup bundles skipped).
    static func scan(_ clonesDir: URL) -> [RegistryEntry] {
        let names = (try? FileManager.default.contentsOfDirectory(atPath: clonesDir.path)) ?? []
        return names.sorted().filter { $0.hasSuffix(".app") && !$0.hasPrefix(".") }.compactMap { name in
            let bundle = clonesDir.appendingPathComponent(name)
            return Self.embeddedManifest(in: bundle).map { RegistryEntry(manifest: $0, bundlePath: bundle.path) }
        }
    }

    /// Loads the registry and keeps it in step with the clones folder: rebuilt if missing or corrupt, clones whose
    /// bundle is gone (trashed in Finder) are dropped, and clones that reappear (put back from the Trash) are added.
    public func loadOrRebuild(clonesDir: URL) throws -> [RegistryEntry] {
        guard FileManager.default.fileExists(atPath: fileURL.path) else { return try rebuild(scanning: clonesDir) }
        let stored: [RegistryEntry]
        do { stored = try load() } catch { return try rebuild(scanning: clonesDir) }
        var entries = stored.filter { FileManager.default.fileExists(atPath: $0.bundlePath) }
        let known = Set(entries.map(\.id))
        entries += Self.scan(clonesDir).filter { !known.contains($0.id) }
        if entries != stored { try save(entries) }
        return entries
    }

    public static func embeddedManifest(in bundle: URL) -> CloneManifest? {
        let url = bundle.appendingPathComponent("Contents/Resources").appendingPathComponent(manifestFileName)
        guard let data = try? Data(contentsOf: url) else { return nil }
        return try? CloneManifest.decoder().decode(CloneManifest.self, from: data)
    }
}

public enum Trash {
    /// Moves to the user's Trash, or into `overrideDir` (tests / MITOSIS_TRASH_DIR). Never deletes permanently.
    @discardableResult
    public static func move(_ url: URL, overrideDir: URL?) throws -> URL? {
        let fm = FileManager.default
        if let overrideDir {
            try fm.createDirectory(at: overrideDir, withIntermediateDirectories: true)
            let dest = overrideDir.appendingPathComponent("\(UUID().uuidString)-\(url.lastPathComponent)")
            try fm.moveItem(at: url, to: dest)
            return dest
        }
        var result: NSURL?
        try fm.trashItem(at: url, resultingItemURL: &result)
        return result as URL?
    }
}
