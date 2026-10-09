import Foundation

public enum AppFramework: String, Codable, Sendable, CaseIterable {
    case electron, chromium, sparkle, squirrel, catalyst
}

public struct AppInfo: Equatable, Sendable {
    public var url: URL
    public var bundleID: String
    public var name: String
    public var version: String
    public var build: String
    public var executableName: String
    public var isSandboxed: Bool
    public var hasMASReceipt: Bool
    public var isAppleApp: Bool
    public var isMitosisClone: Bool
    public var frameworks: Set<AppFramework>
    public var restrictedEntitlements: [String]
    public var cdhash: String?
}

public enum InspectionError: Error, Equatable, CustomStringConvertible, Sendable {
    case notAnApp(String)
    case missingInfo(key: String, app: String)

    public var description: String {
        switch self {
        case .notAnApp(let path): return "\(path) is not a Mac app."
        case let .missingInfo(key, app): return "\(app) is missing \(key) in its Info.plist."
        }
    }
}

public struct AppInspector: Sendable {
    public init() {}

    /// `includeCDHash: false` skips one `codesign` call (pickers that only need the mode decision).
    public func inspect(_ url: URL, includeCDHash: Bool = true) throws -> AppInfo {
        let info = try Self.readInfoPlist(url)
        guard let bundleID = info["CFBundleIdentifier"] as? String else {
            throw InspectionError.missingInfo(key: "CFBundleIdentifier", app: url.lastPathComponent)
        }
        guard let executable = info["CFBundleExecutable"] as? String else {
            throw InspectionError.missingInfo(key: "CFBundleExecutable", app: url.lastPathComponent)
        }
        let fm = FileManager.default
        let entitlements = try Entitlements.read(from: url)
        let version = info["CFBundleShortVersionString"] as? String ?? "0"
        let resolved = url.resolvingSymlinksInPath().path
        return AppInfo(
            url: url,
            bundleID: bundleID,
            name: (info["CFBundleDisplayName"] as? String) ?? (info["CFBundleName"] as? String) ?? url.deletingPathExtension().lastPathComponent,
            version: version,
            build: info["CFBundleVersion"] as? String ?? version,
            executableName: executable,
            isSandboxed: (entitlements["com.apple.security.app-sandbox"] as? Bool) == true,
            hasMASReceipt: fm.fileExists(atPath: url.appendingPathComponent("Contents/_MASReceipt/receipt").path),
            isAppleApp: resolved.hasPrefix("/System/") || bundleID.hasPrefix("com.apple."),
            isMitosisClone: fm.fileExists(atPath: url.appendingPathComponent("Contents/Resources/mitosis.json").path),
            frameworks: Self.detectFrameworks(app: url, info: info),
            restrictedEntitlements: Entitlements.restricted(in: entitlements),
            cdhash: includeCDHash ? Self.cdhash(of: url) : nil
        )
    }

    public static func readInfoPlist(_ app: URL) throws -> [String: Any] {
        guard app.pathExtension == "app",
              let data = try? Data(contentsOf: app.appendingPathComponent("Contents/Info.plist")),
              let plist = try? PropertyListSerialization.propertyList(from: data, format: nil) as? [String: Any]
        else { throw InspectionError.notAnApp(app.path) }
        return plist
    }

    static func cdhash(of app: URL) -> String? {
        guard let r = try? Shell.run("/usr/bin/codesign", ["-dv", "--verbose=4", app.path], check: false),
              r.status == 0 else { return nil }
        for line in r.stderr.split(separator: "\n") where line.hasPrefix("CDHash=") {
            return String(line.dropFirst("CDHash=".count))
        }
        return nil
    }

    static func detectFrameworks(app: URL, info: [String: Any]) -> Set<AppFramework> {
        let fm = FileManager.default
        let dir = app.appendingPathComponent("Contents/Frameworks")
        let names = (try? fm.contentsOfDirectory(atPath: dir.path)) ?? []
        var result = Set<AppFramework>()
        if names.contains("Electron Framework.framework") { result.insert(.electron) }
        if names.contains("Sparkle.framework") { result.insert(.sparkle) }
        if names.contains("Squirrel.framework") { result.insert(.squirrel) }
        if !result.contains(.electron) {
            for name in names where name.hasSuffix(".framework") {
                let helpers = dir.appendingPathComponent(name).appendingPathComponent("Versions/Current/Helpers")
                let items = (try? fm.contentsOfDirectory(atPath: helpers.path)) ?? []
                if items.contains(where: { $0.hasSuffix(".app") }) { result.insert(.chromium); break }
            }
        }
        if info["UIDeviceFamily"] != nil { result.insert(.catalyst) }
        return result
    }
}
