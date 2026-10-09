import Foundation

/// Electron derives both its helper-app paths ("<CFBundleName> Helper*.app") and its keychain item
/// ("<CFBundleName> Safe Storage") from CFBundleName. Renaming the helpers lets a clone use its own name
/// everywhere, so it never touches the original app's keychain item or data.
public enum HelperRenamer {
    /// Renames Contents/Frameworks/"<oldName> Helper*.app" → "<newName> Helper*.app", renaming each helper's
    /// executable and setting its CFBundleExecutable/CFBundleName. Returns the renamed bundle URLs.
    public static func rename(in bundle: URL, from oldName: String, to newName: String) throws -> [URL] {
        let fm = FileManager.default
        let frameworks = bundle.appendingPathComponent("Contents/Frameworks")
        let prefix = "\(oldName) Helper"
        let items = (try? fm.contentsOfDirectory(atPath: frameworks.path)) ?? []
        var renamed: [URL] = []
        for item in items.sorted() where item.hasSuffix(".app") && item.hasPrefix(prefix) {
            let oldBase = String(item.dropLast(".app".count))
            let suffix = String(oldBase.dropFirst(prefix.count))
            guard suffix.isEmpty || suffix.hasPrefix(" ") else { continue }   // e.g. "Fixture Helpers.app" isn't a helper
            let newBase = "\(newName) Helper\(suffix)"
            let oldURL = frameworks.appendingPathComponent(item)
            let plistURL = oldURL.appendingPathComponent("Contents/Info.plist")
            var info = try InfoPlistEditor.read(plistURL)
            let oldExe = info["CFBundleExecutable"] as? String ?? oldBase
            let macOS = oldURL.appendingPathComponent("Contents/MacOS")
            try fm.moveItem(at: macOS.appendingPathComponent(oldExe), to: macOS.appendingPathComponent(newBase))
            info["CFBundleExecutable"] = newBase
            info["CFBundleName"] = newBase
            try InfoPlistEditor.write(info, to: plistURL)
            let newURL = frameworks.appendingPathComponent("\(newBase).app")
            try fm.moveItem(at: oldURL, to: newURL)
            renamed.append(newURL)
        }
        return renamed
    }
}
