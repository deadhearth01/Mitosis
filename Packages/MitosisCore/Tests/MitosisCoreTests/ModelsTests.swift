import Foundation
import Testing
@testable import MitosisCore

@Suite struct ModelsTests {
    static func sampleManifest() -> CloneManifest {
        CloneManifest(
            id: UUID(uuidString: "6F1C2D3E-4A5B-4C6D-8E9F-0A1B2C3D4E5F")!,
            name: "Slack (Work)",
            badge: Badge(text: "W", color: "#0A84FF"),
            mode: .identity,
            cloneBundleID: "com.tinyspeck.slackmacgap.mitosis.slack-work",
            source: .init(path: "/Applications/Slack.app", bundleID: "com.tinyspeck.slackmacgap", version: "4.47.0", cdhash: "abc123"),
            profile: .init(id: "slack", version: 3),
            dataPath: "/Users/me/Library/Mitosis/Data/6f1c2d3e",
            createdAt: Date(timeIntervalSince1970: 1_791_500_000),
            refreshedAt: Date(timeIntervalSince1970: 1_791_500_000)
        )
    }

    @Test func manifestRoundTripsThroughJSON() throws {
        let m = Self.sampleManifest()
        let data = try CloneManifest.encoder().encode(m)
        let back = try CloneManifest.decoder().decode(CloneManifest.self, from: data)
        #expect(back == m)
        let text = String(decoding: data, as: UTF8.self)
        #expect(text.contains("\"schema\" : 1"))
        #expect(text.contains("\"mitosisVersion\" : \"0.1.0\""))
        #expect(text.contains("2026-10-08T"))   // ISO-8601 dates
    }

    @Test func registryEntryExposesIDAndURL() {
        let e = RegistryEntry(manifest: Self.sampleManifest(), bundlePath: "/tmp/Slack (Work).app")
        #expect(e.id == Self.sampleManifest().id)
        #expect(e.bundleURL.lastPathComponent == "Slack (Work).app")
    }

    @Test func badgePresetsCoverEightSystemColors() {
        #expect(Badge.presets.count == 8)
        #expect(Badge.presets["blue"] == "#0A84FF")
        #expect(Badge.defaultColor == "#0A84FF")
    }
}
