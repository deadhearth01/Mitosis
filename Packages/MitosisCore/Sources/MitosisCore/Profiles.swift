import Foundation

public struct LaunchSettings: Codable, Equatable, Sendable {
    public var args: [String]
    public var env: [String: String]

    public init(args: [String], env: [String: String]) {
        self.args = args
        self.env = env
    }

    public static let none = LaunchSettings(args: [], env: [:])
    public var isEmpty: Bool { args.isEmpty && env.isEmpty }
}

public struct AppProfile: Codable, Equatable, Sendable {
    public var id: String
    public var bundleIDs: [String]
    public var version: Int
    public var mode: CloneMode
    public var support: SupportLevel
    public var launch: LaunchSettings
    public var notes: String

    public init(id: String, bundleIDs: [String], version: Int, mode: CloneMode, support: SupportLevel, launch: LaunchSettings, notes: String) {
        self.id = id
        self.bundleIDs = bundleIDs
        self.version = version
        self.mode = mode
        self.support = support
        self.launch = launch
        self.notes = notes
    }
}

public enum ProfileValidationError: Error, Equatable, CustomStringConvertible, Sendable {
    case unknownSchema(Int)
    case argNotAllowed(profile: String, arg: String)
    case envNotAllowed(profile: String, key: String)

    public var description: String {
        switch self {
        case .unknownSchema(let s): return "Unsupported profiles schema \(s)."
        case let .argNotAllowed(p, a): return "Profile \(p) uses a launch argument that isn't allowed: \(a)"
        case let .envNotAllowed(p, k): return "Profile \(p) sets an environment variable that isn't allowed: \(k)"
        }
    }
}

public struct ProfileStore: Sendable {
    /// Only these launch arguments may appear in profiles (protects against e.g. remote-debugging flags).
    public static let allowedArgs: Set<String> = ["--user-data-dir={dataPath}"]
    /// Allowed environment variables and their only allowed values.
    public static let allowedEnv: [String: Set<String>] = ["HOME": ["{dataPath}", "{dataPath}/home"]]

    private struct File: Codable {
        var schema: Int
        var profiles: [AppProfile]
    }

    private let byBundleID: [String: AppProfile]

    public init(profiles: [AppProfile]) throws {
        var map: [String: AppProfile] = [:]
        for p in profiles {
            for arg in p.launch.args where !Self.allowedArgs.contains(arg) {
                throw ProfileValidationError.argNotAllowed(profile: p.id, arg: arg)
            }
            for (key, value) in p.launch.env where Self.allowedEnv[key]?.contains(value) != true {
                throw ProfileValidationError.envNotAllowed(profile: p.id, key: key)
            }
            for id in p.bundleIDs { map[id] = p }
        }
        byBundleID = map
    }

    public static func decode(_ data: Data) throws -> ProfileStore {
        let file = try JSONDecoder().decode(File.self, from: data)
        guard file.schema == 1 else { throw ProfileValidationError.unknownSchema(file.schema) }
        return try ProfileStore(profiles: file.profiles)
    }

    public static func bundled() throws -> ProfileStore {
        guard let url = Bundle.module.url(forResource: "profiles", withExtension: "json") else {
            return try ProfileStore(profiles: [])
        }
        return try decode(Data(contentsOf: url))
    }

    public func profile(forBundleID bundleID: String) -> AppProfile? {
        byBundleID[bundleID]
    }
}

public enum DefaultLaunchSettings {
    /// How identity-mode clones keep their data separate when no profile exists (spec §14 decision 5).
    public static func forFrameworks(_ frameworks: Set<AppFramework>) -> LaunchSettings {
        if frameworks.contains(.electron) { return LaunchSettings(args: ["--user-data-dir={dataPath}"], env: [:]) }
        if frameworks.contains(.chromium) { return LaunchSettings(args: ["--user-data-dir={dataPath}"], env: [:]) }
        return .none
    }

    /// Fallback-mode shortcuts separate data by giving the original app its own HOME.
    public static let fallback = LaunchSettings(args: [], env: ["HOME": "{dataPath}"])
}
