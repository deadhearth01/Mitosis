import Darwin
import Foundation

public enum FileCloner {
    /// APFS-clones `from` to `to` (instant, no extra disk). Falls back to a full copy across volumes.
    /// Ownership is not copied (CLONE_NOOWNERCOPY), so the current user owns the result.
    @discardableResult
    public static func cloneOrCopy(from: URL, to: URL) throws -> Bool {
        if clonefile(from.path, to.path, UInt32(CLONE_NOOWNERCOPY)) == 0 {
            return true
        }
        let err = errno
        if err == EXDEV || err == ENOTSUP {
            try FileManager.default.copyItem(at: from, to: to)
            return false
        }
        throw POSIXError(POSIXErrorCode(rawValue: err) ?? .EIO)
    }

    public static func isSameVolume(_ a: URL, _ b: URL) -> Bool {
        guard let va = volumeID(of: nearestExisting(a)), let vb = volumeID(of: nearestExisting(b)) else { return false }
        return va.isEqual(vb)
    }

    public static func allocatedSize(of url: URL) -> Int64 {
        let keys: [URLResourceKey] = [.totalFileAllocatedSizeKey, .isRegularFileKey]
        guard let e = FileManager.default.enumerator(at: url, includingPropertiesForKeys: keys) else { return 0 }
        var total: Int64 = 0
        for case let item as URL in e {
            let v = try? item.resourceValues(forKeys: Set(keys))
            if v?.isRegularFile == true { total += Int64(v?.totalFileAllocatedSize ?? 0) }
        }
        return total
    }

    private static func volumeID(of url: URL) -> NSObject? {
        (try? url.resourceValues(forKeys: [.volumeIdentifierKey]))?.volumeIdentifier as? NSObject
    }

    private static func nearestExisting(_ url: URL) -> URL {
        var u = url
        while !FileManager.default.fileExists(atPath: u.path) && u.path != "/" {
            u = u.deletingLastPathComponent()
        }
        return u
    }
}
