import Foundation
import Testing
@testable import MitosisCore

@Suite struct AutoRefreshAgentTests {
    static func entry(_ name: String, source: String) -> RegistryEntry {
        let m = CloneManifest(id: UUID(), name: name, badge: Badge(text: "W", color: "#0A84FF"), mode: .identity,
                              cloneBundleID: "com.example.x.mitosis.\(Slug.make(from: name))",
                              source: .init(path: source, bundleID: "com.example.x", version: "1", cdhash: nil),
                              profile: nil, dataPath: "/tmp/x", createdAt: Date(), refreshedAt: Date())
        return RegistryEntry(manifest: m, bundlePath: "/tmp/\(name).app")
    }

    @Test func watchesEachOriginalOnceWithItsFolder() {
        let entries = [Self.entry("Slack (Work)", source: "/Applications/Slack.app"),
                       Self.entry("Slack (Home)", source: "/Applications/Slack.app"),
                       Self.entry("Notes (A)", source: "/Users/x/Applications/Notes.app")]
        #expect(AutoRefreshAgent.watchPaths(for: entries) == [
            "/Applications", "/Applications/Slack.app/Contents/Info.plist",
            "/Users/x/Applications", "/Users/x/Applications/Notes.app/Contents/Info.plist",
        ])
    }

    @Test func plistRunsTheQuietOutdatedRefresh() throws {
        let data = AutoRefreshAgent.plist(cli: URL(fileURLWithPath: "/Applications/Mitosis.app/Contents/Helpers/mitosis"),
                                          watchPaths: ["/Applications"], logFile: URL(fileURLWithPath: "/tmp/m.log"))
        let p = try #require(try PropertyListSerialization.propertyList(from: data, format: nil) as? [String: Any])
        #expect(p["Label"] as? String == "com.mitosis-mac.autorefresh")
        #expect(p["ProgramArguments"] as? [String] == ["/Applications/Mitosis.app/Contents/Helpers/mitosis", "refresh", "--outdated", "--quiet"])
        #expect(p["WatchPaths"] as? [String] == ["/Applications"])
        #expect(p["StartInterval"] as? Int == 21_600)
        #expect(p["ThrottleInterval"] as? Int == 30)
        #expect(p["RunAtLoad"] as? Bool == false)
        #expect(p["ProcessType"] as? String == "Background")
        #expect(p["StandardErrorPath"] as? String == "/tmp/m.log")
    }

    @Test func syncWritesOnceAndRemovesWhenOffOrEmpty() throws {
        let dir = try TestSupport.tempDir()
        let cli = URL(fileURLWithPath: "/Applications/Mitosis.app/Contents/Helpers/mitosis")
        let entries = [Self.entry("Slack (Work)", source: "/Applications/Slack.app")]
        let file = dir.appendingPathComponent("com.mitosis-mac.autorefresh.plist")
        #expect(try AutoRefreshAgent.sync(cli: cli, entries: entries, enabled: true, agentsDir: dir, load: false))
        #expect(FileManager.default.fileExists(atPath: file.path))
        #expect(try !AutoRefreshAgent.sync(cli: cli, entries: entries, enabled: true, agentsDir: dir, load: false))
        #expect(try AutoRefreshAgent.sync(cli: cli, entries: entries, enabled: false, agentsDir: dir, load: false))
        #expect(!FileManager.default.fileExists(atPath: file.path))
        #expect(try !AutoRefreshAgent.sync(cli: cli, entries: [], enabled: true, agentsDir: dir, load: false))
        #expect(try AutoRefreshAgent.sync(cli: cli, entries: entries, enabled: true, agentsDir: dir, load: false))
        #expect(try AutoRefreshAgent.sync(cli: cli, entries: [], enabled: true, agentsDir: dir, load: false))
        #expect(!FileManager.default.fileExists(atPath: file.path))
    }
}
