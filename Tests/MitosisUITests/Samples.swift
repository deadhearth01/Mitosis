import Foundation
import MitosisCore

enum Samples {
    static func entry(_ name: String, app: String, bundleID: String, mode: CloneMode = .identity, id: UUID = UUID()) -> RegistryEntry {
        let manifest = CloneManifest(
            id: id, name: name, badge: Badge(text: String(name.prefix(1)), color: "#0A84FF"), mode: mode,
            cloneBundleID: "\(bundleID).mitosis.\(Slug.make(from: name))",
            source: .init(path: "/Applications/\(app).app", bundleID: bundleID, version: "1.0", cdhash: nil),
            profile: nil, dataPath: "/tmp/mitosis-sample/\(id.uuidString.prefix(8))",
            createdAt: Date(timeIntervalSince1970: 1_791_500_000), refreshedAt: Date(timeIntervalSince1970: 1_791_500_000))
        return RegistryEntry(manifest: manifest, bundlePath: "/tmp/mitosis-sample/Clones/\(name).app")
    }

    static let slackWork = entry("Slack (Work)", app: "Slack", bundleID: "com.tinyspeck.slackmacgap")
    static let slackClient = entry("Slack (Client)", app: "Slack", bundleID: "com.tinyspeck.slackmacgap")
    static let discordFriends = entry("Discord (Friends)", app: "Discord", bundleID: "com.hnc.Discord")
}
