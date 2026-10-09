import Foundation
import Testing
@testable import MitosisCore

@Suite struct LinkRouterTests {
    static func entry(_ name: String, source: String, sourceID: String, mode: CloneMode = .identity) -> RegistryEntry {
        let m = CloneManifest(id: UUID(), name: name, badge: Badge(text: "W", color: "#0A84FF"), mode: mode,
                              cloneBundleID: "\(sourceID).mitosis.\(Slug.make(from: name))",
                              source: .init(path: source, bundleID: sourceID, version: "1", cdhash: nil),
                              profile: nil, dataPath: "/tmp/x", createdAt: Date(), refreshedAt: Date())
        return RegistryEntry(manifest: m, bundlePath: "/tmp/\(name).app")
    }

    @Test func declaredSchemesSkipWebAndSystemOnes() throws {
        let dir = try TestSupport.tempDir()
        var o = FixtureOptions()
        o.extraInfo = ["CFBundleURLTypes": [["CFBundleURLSchemes": ["slack", "HTTPS"]], ["CFBundleURLSchemes": ["Slack-Beta", "mailto"]]]]
        o.signed = false
        let app = try FixtureFactory.makeApp(in: dir, o)
        #expect(URLSchemes.declared(byAppAt: app) == ["slack", "slack-beta"])
        #expect(URLSchemes.declared(byAppAt: dir.appendingPathComponent("Missing.app")).isEmpty)
    }

    @Test func stateRoundTrips() throws {
        let dir = try TestSupport.tempDir()
        let file = dir.appendingPathComponent("link-router.json")
        #expect(LinkRouterState.load(from: file) == LinkRouterState())
        var state = LinkRouterState()
        state.apps["com.tinyspeck.slackmacgap"] = .init(sourcePath: "/Applications/Slack.app", schemes: ["slack"],
                                                        previousHandlers: ["slack": "com.tinyspeck.slackmacgap"])
        try state.save(to: file)
        #expect(LinkRouterState.load(from: file) == state)
        #expect(state.allSchemes == ["slack"])
    }

    @Test func targetsAreTheOriginalAndItsFullClones() throws {
        let dir = try TestSupport.tempDir()
        let original = dir.appendingPathComponent("Slack.app")
        try FileManager.default.createDirectory(at: original, withIntermediateDirectories: true)
        let id = "com.tinyspeck.slackmacgap"
        let work = Self.entry("Slack (Work)", source: original.path, sourceID: id)
        let safe = Self.entry("Slack (Safe)", source: original.path, sourceID: id, mode: .fallback)
        let other = Self.entry("Notion (A)", source: "/Applications/Notion.app", sourceID: "notion.id")
        var state = LinkRouterState()
        state.apps[id] = .init(sourcePath: original.path, schemes: ["slack"], previousHandlers: [:])

        let targets = LinkRouter.targets(for: URL(string: "slack://login?code=1")!, entries: [work, safe, other], state: state,
                                         isRunning: { $0 == work.manifest.cloneBundleID })
        #expect(targets.map(\.name) == ["Slack", "Slack (Work)"])
        #expect(targets.map(\.isRunning) == [false, true])
        #expect(targets.first?.bundleID == id)
        #expect(LinkRouter.targets(for: URL(string: "zoom://x")!, entries: [work], state: state, isRunning: { _ in false }).isEmpty)

        try FileManager.default.removeItem(at: original)   // original moved away: clones still get links
        #expect(LinkRouter.targets(for: URL(string: "SLACK://x")!, entries: [work], state: state, isRunning: { _ in false })
            .map(\.name) == ["Slack (Work)"])
    }

    @Test func autoTargetOnlyWhenUnambiguous() {
        func t(_ name: String, _ running: Bool) -> LinkTarget {
            LinkTarget(name: name, bundleURL: URL(fileURLWithPath: "/tmp/\(name).app"), bundleID: name, isClone: true, isRunning: running)
        }
        #expect(LinkRouter.autoTarget([t("A", false), t("B", true)])?.name == "B")
        #expect(LinkRouter.autoTarget([t("A", false)])?.name == "A")
        #expect(LinkRouter.autoTarget([t("A", true), t("B", true)]) == nil)
        #expect(LinkRouter.autoTarget([t("A", false), t("B", false)]) == nil)
        #expect(LinkRouter.autoTarget([]) == nil)
    }
}
