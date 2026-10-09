import Foundation

public struct CloneUsage: Equatable, Sendable {
    public var processCount: Int
    public var cpuPercent: Double
    public var memoryBytes: Int64
}

public struct CloneStats: Equatable, Sendable {
    /// Bytes the clone really adds on disk (everything else is APFS-shared with the original app).
    public var extraDiskBytes: Int64
    /// Size of the clone bundle as Finder would show it.
    public var appBytes: Int64
    /// Logins, settings and caches stored for this clone.
    public var dataBytes: Int64
    /// Live usage while running; nil when not running.
    public var usage: CloneUsage?
}

public enum StatsCollector {
    public static let cacheFolderNames: Set<String> = [
        "Cache", "Code Cache", "GPUCache", "DawnGraphiteCache", "DawnWebGPUCache", "update-cache", "CachedExtensionVSIXs",
    ]

    /// Estimated bytes the clone really adds: files that are new or differ (size or mtime) from the source, since
    /// unchanged files are APFS clones sharing the original's blocks. Clones on another drive count in full.
    public static func extraDiskBytes(clone: URL, source: URL) -> Int64 {
        let fm = FileManager.default
        // A clone on another drive is a full copy, so nothing is shared.
        let shared = fm.fileExists(atPath: source.path) && FileCloner.isSameVolume(clone, source)
        let keys: [URLResourceKey] = [.isRegularFileKey, .isSymbolicLinkKey, .fileSizeKey, .contentModificationDateKey]
        guard let e = fm.enumerator(at: clone, includingPropertiesForKeys: keys) else { return 0 }
        let cloneRoot = clone.standardizedFileURL.path
        var total: Int64 = 0
        for case let url as URL in e {
            guard let v = try? url.resourceValues(forKeys: Set(keys)), v.isRegularFile == true, v.isSymbolicLink != true else { continue }
            let rel = String(url.standardizedFileURL.path.dropFirst(cloneRoot.count))
            let sourceFile = URL(fileURLWithPath: source.path + rel)
            let size = Int64(v.fileSize ?? 0)
            guard shared else { total += size; continue }
            guard let s = try? sourceFile.resourceValues(forKeys: [.fileSizeKey, .contentModificationDateKey]) else {
                total += size
                continue
            }
            let sameSize = Int64(s.fileSize ?? -1) == size
            let sameDate = abs((s.contentModificationDate?.timeIntervalSince1970 ?? 0) - (v.contentModificationDate?.timeIntervalSince1970 ?? -1)) < 1
            if !(sameSize && sameDate) { total += size }
        }
        return total
    }

    public static func dataBytes(_ paths: [URL]) -> Int64 {
        paths.filter { FileManager.default.fileExists(atPath: $0.path) }.reduce(0) { $0 + FileCloner.allocatedSize(of: $1) }
    }

    /// Processes whose executable lives inside the bundle (main app + helpers); nil if none are running.
    public static func usage(bundle: URL) -> CloneUsage? {
        guard let r = try? Shell.run("/bin/ps", ["-Ao", "pid=,pcpu=,rss=,comm="], check: false) else { return nil }
        let prefixes = Set([bundle.path, bundle.resolvingSymlinksInPath().path].map { $0 + "/" })
        var count = 0
        var cpu = 0.0
        var rssKB: Int64 = 0
        for line in r.stdout.split(separator: "\n") {
            let parts = line.split(separator: " ", maxSplits: 3, omittingEmptySubsequences: true)
            guard parts.count == 4 else { continue }
            let command = String(parts[3])
            guard prefixes.contains(where: { command.hasPrefix($0) }) else { continue }
            count += 1
            cpu += Double(parts[1]) ?? 0
            rssKB += Int64(parts[2]) ?? 0
        }
        return count == 0 ? nil : CloneUsage(processCount: count, cpuPercent: cpu, memoryBytes: rssKB * 1024)
    }

    public static func stats(for entry: RegistryEntry) -> CloneStats {
        let m = entry.manifest
        let container = FileManager.default.homeDirectoryForCurrentUser.appendingPathComponent("Library/Containers/\(m.cloneBundleID)")
        return CloneStats(
            extraDiskBytes: extraDiskBytes(clone: entry.bundleURL, source: URL(fileURLWithPath: m.source.path)),
            appBytes: FileCloner.allocatedSize(of: entry.bundleURL),
            dataBytes: dataBytes([URL(fileURLWithPath: m.dataPath), container]),
            usage: usage(bundle: entry.bundleURL)
        )
    }

    /// Deletes only the known, regenerable cache folders directly inside the data path. Returns bytes freed.
    @discardableResult
    public static func cleanCaches(dataPath: URL) throws -> Int64 {
        let fm = FileManager.default
        var freed: Int64 = 0
        for name in cacheFolderNames.sorted() {
            let url = dataPath.appendingPathComponent(name)
            guard fm.fileExists(atPath: url.path) else { continue }
            freed += FileCloner.allocatedSize(of: url)
            try fm.removeItem(at: url)
        }
        return freed
    }
}
