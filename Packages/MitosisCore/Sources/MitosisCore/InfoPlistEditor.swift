import Foundation

public struct InfoPlistEdit: Sendable, Equatable {
    public var bundleID: String
    public var name: String
    public var iconFile: String?
    public var disableSparkle: Bool

    public init(bundleID: String, name: String, iconFile: String?, disableSparkle: Bool) {
        self.bundleID = bundleID
        self.name = name
        self.iconFile = iconFile
        self.disableSparkle = disableSparkle
    }
}

public enum InfoPlistEditor {
    public static func read(_ url: URL) throws -> [String: Any] {
        let data = try Data(contentsOf: url)
        return try PropertyListSerialization.propertyList(from: data, format: nil) as? [String: Any] ?? [:]
    }

    public static func write(_ dict: [String: Any], to url: URL) throws {
        let data = try PropertyListSerialization.data(fromPropertyList: dict, format: .xml, options: 0)
        try data.write(to: url, options: .atomic)
    }

    public static func apply(_ edit: InfoPlistEdit, to url: URL) throws {
        var d = try read(url)
        d["CFBundleIdentifier"] = edit.bundleID
        d["CFBundleName"] = edit.name
        d["CFBundleDisplayName"] = edit.name
        if let icon = edit.iconFile {
            d["CFBundleIconFile"] = icon
            d.removeValue(forKey: "CFBundleIconName")   // asset-catalog icon would win over the .icns
            d.removeValue(forKey: "CFBundleIcons")
        }
        if edit.disableSparkle {
            d["SUEnableAutomaticChecks"] = false
            d["SUAutomaticallyUpdate"] = false
            d.removeValue(forKey: "SUFeedURL")
        }
        try write(d, to: url)
    }
}
