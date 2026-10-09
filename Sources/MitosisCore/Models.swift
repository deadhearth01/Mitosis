import Foundation

public enum CloneMode: String, Codable, Sendable, CaseIterable {
    case identity
    case fallback
}

public enum SupportLevel: String, Codable, Sendable {
    case full
    case limited
    case unsupported
}

public struct ModeDecision: Equatable, Sendable {
    public let mode: CloneMode?
    public let support: SupportLevel
    public let reasons: [String]

    public init(mode: CloneMode?, support: SupportLevel, reasons: [String]) {
        self.mode = mode
        self.support = support
        self.reasons = reasons
    }
}

public struct Badge: Codable, Equatable, Sendable {
    public var text: String
    /// "#RRGGBB"
    public var color: String

    public init(text: String, color: String) {
        self.text = text
        self.color = color
    }

    public static let defaultColor = "#0A84FF"
    public static let presets: [String: String] = [
        "blue": "#0A84FF", "green": "#30D158", "orange": "#FF9F0A", "red": "#FF453A",
        "purple": "#BF5AF2", "pink": "#FF375F", "yellow": "#FFD60A", "gray": "#8E8E93",
    ]

    /// A preset name ("blue") or "#RRGGBB" (any case) as uppercase "#RRGGBB"; nil for anything else.
    public static func normalizedColor(_ input: String) -> String? {
        if let preset = presets[input.lowercased()] { return preset }
        let digits = input.dropFirst()
        guard input.hasPrefix("#"), digits.count == 6, digits.allSatisfy(\.isHexDigit) else { return nil }
        return input.uppercased()
    }
}

public struct CloneManifest: Codable, Equatable, Sendable, Identifiable {
    public struct Source: Codable, Equatable, Sendable {
        public var path: String
        public var bundleID: String
        public var version: String
        public var cdhash: String?

        public init(path: String, bundleID: String, version: String, cdhash: String?) {
            self.path = path
            self.bundleID = bundleID
            self.version = version
            self.cdhash = cdhash
        }
    }

    public struct ProfileRef: Codable, Equatable, Sendable {
        public var id: String
        public var version: Int

        public init(id: String, version: Int) {
            self.id = id
            self.version = version
        }
    }

    public var schema: Int
    public var id: UUID
    public var name: String
    public var badge: Badge
    public var mode: CloneMode
    public var cloneBundleID: String
    public var source: Source
    public var profile: ProfileRef?
    public var dataPath: String
    public var createdAt: Date
    public var refreshedAt: Date
    public var mitosisVersion: String

    public init(
        id: UUID, name: String, badge: Badge, mode: CloneMode, cloneBundleID: String,
        source: Source, profile: ProfileRef?, dataPath: String,
        createdAt: Date, refreshedAt: Date, mitosisVersion: String = Mitosis.version
    ) {
        self.schema = 1
        self.id = id
        self.name = name
        self.badge = badge
        self.mode = mode
        self.cloneBundleID = cloneBundleID
        self.source = source
        self.profile = profile
        self.dataPath = dataPath
        self.createdAt = createdAt
        self.refreshedAt = refreshedAt
        self.mitosisVersion = mitosisVersion
    }

    public static func encoder() -> JSONEncoder {
        let e = JSONEncoder()
        e.outputFormatting = [.prettyPrinted, .sortedKeys]
        e.dateEncodingStrategy = .iso8601
        return e
    }

    public static func decoder() -> JSONDecoder {
        let d = JSONDecoder()
        d.dateDecodingStrategy = .iso8601
        return d
    }
}

public struct RegistryEntry: Codable, Equatable, Sendable, Identifiable {
    public var manifest: CloneManifest
    public var bundlePath: String

    public init(manifest: CloneManifest, bundlePath: String) {
        self.manifest = manifest
        self.bundlePath = bundlePath
    }

    public var id: UUID { manifest.id }
    public var bundleURL: URL { URL(fileURLWithPath: bundlePath) }
}
