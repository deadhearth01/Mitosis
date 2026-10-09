import Foundation

public enum Entitlements {
    /// Keys that only work with the original developer's certificate/team.
    static let restrictedKeys: Set<String> = [
        "keychain-access-groups",
        "com.apple.application-identifier",
        "application-identifier",
        "aps-environment",
        "com.apple.security.application-groups",
        "com.apple.team-identifier",
    ]

    public static func isRestricted(_ key: String) -> Bool {
        key.hasPrefix("com.apple.developer.") || restrictedKeys.contains(key)
    }

    /// Entitlements of the bundle's main executable. Unsigned or unreadable → empty.
    public static func read(from bundle: URL) throws -> [String: Any] {
        let r = try Shell.run("/usr/bin/codesign", ["-d", "--entitlements", "-", "--xml", bundle.path], check: false)
        guard r.status == 0 else { return [:] }
        let data = Data(r.stdout.utf8)
        guard !data.isEmpty else { return [:] }
        let plist = try PropertyListSerialization.propertyList(from: data, format: nil)
        return plist as? [String: Any] ?? [:]
    }

    public static func restricted(in entitlements: [String: Any]) -> [String] {
        entitlements.keys.filter(isRestricted).sorted()
    }

    public static func sanitized(_ entitlements: [String: Any]) -> [String: Any] {
        entitlements.filter { !isRestricted($0.key) }
    }

    public static func xmlData(_ entitlements: [String: Any]) throws -> Data {
        try PropertyListSerialization.data(fromPropertyList: entitlements, format: .xml, options: 0)
    }
}
