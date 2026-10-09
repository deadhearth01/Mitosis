import Foundation

/// Resolved launch instructions stored inside a clone and read by LaunchStub.
public struct LaunchConfig: Codable, Equatable, Sendable {
    public enum Kind: String, Codable, Sendable {
        /// Replace the stub process with Contents/MacOS/<target> (identity mode).
        case exec
        /// Ask macOS to open the app at absolute path <target> as a new instance (legacy).
        case open
        /// Start the executable at absolute path <target> as a child, record its pid in `pidFile`, and stay
        /// alive until it exits (fallback mode) — so Mitosis can tell exactly which instance belongs to a clone.
        case spawn
    }

    public var kind: Kind
    public var target: String
    public var args: [String]
    public var env: [String: String]
    public var pidFile: String?
    /// Folders inside the clone's data folder to create before launching (some apps refuse a missing home folder).
    public var dirs: [String]

    public static let fileName = "mitosis-launch.json"

    public init(kind: Kind, target: String, args: [String], env: [String: String], pidFile: String? = nil, dirs: [String] = []) {
        self.kind = kind
        self.target = target
        self.args = args
        self.env = env
        self.pidFile = pidFile
        self.dirs = dirs
    }

    public init(from decoder: any Decoder) throws {
        let c = try decoder.container(keyedBy: CodingKeys.self)
        kind = try c.decode(Kind.self, forKey: .kind)
        target = try c.decode(String.self, forKey: .target)
        args = try c.decode([String].self, forKey: .args)
        env = try c.decode([String: String].self, forKey: .env)
        pidFile = try c.decodeIfPresent(String.self, forKey: .pidFile)
        dirs = try c.decodeIfPresent([String].self, forKey: .dirs) ?? []   // clones made before 0.2.1
    }

    public static func resolve(_ settings: LaunchSettings, kind: Kind, target: String, dataPath: String,
                               pidFile: String? = nil) -> LaunchConfig {
        func fill(_ s: String) -> String { s.replacingOccurrences(of: "{dataPath}", with: dataPath) }
        let dirs = settings.env.values.filter { $0.hasPrefix("{dataPath}") }.map(fill)
        return LaunchConfig(kind: kind, target: target, args: settings.args.map(fill), env: settings.env.mapValues(fill),
                            pidFile: pidFile, dirs: Array(Set(dirs)).sorted())
    }

    public func write(toResources resources: URL) throws {
        try FileManager.default.createDirectory(at: resources, withIntermediateDirectories: true)
        let e = JSONEncoder()
        e.outputFormatting = [.prettyPrinted, .sortedKeys]
        try e.encode(self).write(to: resources.appendingPathComponent(Self.fileName), options: .atomic)
    }
}
