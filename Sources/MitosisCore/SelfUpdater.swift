import CryptoKit
import Foundation

public enum SelfUpdaterError: Error, Equatable, CustomStringConvertible, Sendable {
    case checksumMismatch
    case notMitosis
    case badSignature
    case unpackFailed
    case cantReplace(String)

    public var description: String {
        switch self {
        case .checksumMismatch: return "The download didn't match its checksum, so nothing was installed."
        case .notMitosis: return "The download isn't Mitosis, so nothing was installed."
        case .badSignature: return "The new version's signature didn't check out, so nothing was installed."
        case .unpackFailed: return "Couldn't unpack the download."
        case .cantReplace(let reason): return "Couldn't replace Mitosis: \(reason)"
        }
    }
}

/// Installs a downloaded release over the running Mitosis.app: checksum, bundle ID and signature are verified first,
/// and the swap is a single atomic replace, so a failed update leaves the current version in place.
public enum SelfUpdater {
    public static let releasesBase = URL(string: "https://github.com/deadhearth01/Mitosis/releases/download")!

    public static func assetURLs(version: String) -> (zip: URL, checksum: URL) {
        let zip = releasesBase.appendingPathComponent("v\(version)").appendingPathComponent("Mitosis-\(version).zip")
        return (zip, zip.appendingPathExtension("sha256"))
    }

    public static func sha256(of file: URL) throws -> String {
        let data = try Data(contentsOf: file, options: .mappedIfSafe)
        return SHA256.hash(data: data).map { String(format: "%02x", $0) }.joined()
    }

    @discardableResult
    public static func install(zip: URL, checksumFile: URL, replacing installed: URL,
                               registerWithLaunchServices: Bool = true) throws -> URL {
        let fm = FileManager.default
        let expected = (try String(contentsOf: checksumFile, encoding: .utf8)).split(separator: " ").first.map(String.init) ?? ""
        guard !expected.isEmpty, expected.lowercased() == (try sha256(of: zip)) else { throw SelfUpdaterError.checksumMismatch }

        // Unpack next to the installed app (same volume) so the final swap is atomic.
        let work: URL
        do {
            work = try fm.url(for: .itemReplacementDirectory, in: .userDomainMask, appropriateFor: installed, create: true)
        } catch {
            throw SelfUpdaterError.cantReplace(error.localizedDescription)
        }
        defer { try? fm.removeItem(at: work) }
        guard (try? Shell.run("/usr/bin/ditto", ["-x", "-k", zip.path, work.path]))?.status == 0 else { throw SelfUpdaterError.unpackFailed }
        let new = work.appendingPathComponent("Mitosis.app")
        guard let info = try? InfoPlistEditor.read(new.appendingPathComponent("Contents/Info.plist")),
              info["CFBundleIdentifier"] as? String == Mitosis.bundleID else { throw SelfUpdaterError.notMitosis }
        _ = try? Shell.run("/usr/bin/xattr", ["-dr", "com.apple.quarantine", new.path], check: false)
        do { try Signer.verify(new) } catch { throw SelfUpdaterError.badSignature }

        do {
            _ = try fm.replaceItemAt(installed, withItemAt: new)
        } catch {
            throw SelfUpdaterError.cantReplace(error.localizedDescription)
        }
        if registerWithLaunchServices { try? LaunchServices.register(installed) }
        return installed
    }
}
