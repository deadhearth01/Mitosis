import Foundation
import Synchronization
import Testing
@testable import MitosisCore

/// Records default-handler changes instead of touching the real LaunchServices database.
final class FakeHandlers: URLHandlerRegistry {
    private let map: Mutex<[String: String]>
    init(_ initial: [String: String] = [:]) { map = Mutex(initial) }
    func defaultHandler(forScheme scheme: String) -> String? { map.withLock { $0[scheme] } }
    func setDefaultHandler(bundleID: String, forScheme scheme: String) throws { map.withLock { $0[scheme] = bundleID } }
}

@Suite struct LinkRouterSetupTests {
    static func app(_ name: String, id: String, schemes: [String], in dir: URL) throws -> URL {
        var o = FixtureOptions()
        o.name = name; o.executable = name; o.bundleID = id; o.signed = false
        o.extraInfo = schemes.isEmpty ? [:] : ["CFBundleURLTypes": [["CFBundleURLSchemes": schemes]]]
        return try FixtureFactory.makeApp(in: dir, o)
    }

    static func setup(_ handlers: FakeHandlers) throws -> (LinkRouterSetup, URL) {
        let root = try TestSupport.tempDir()
        let setup = LinkRouterSetup(environment: TestSupport.environment(root: root), handlers: handlers,
                                    routerExecutable: TestSupport.stubBinary)
        return (setup, root)
    }

    static func routerSchemes(_ setup: LinkRouterSetup) throws -> [String] {
        let info = try InfoPlistEditor.read(setup.routerBundle.appendingPathComponent("Contents/Info.plist"))
        let types = try #require(info["CFBundleURLTypes"] as? [[String: Any]])
        return (types.first?["CFBundleURLSchemes"] as? [String]) ?? []
    }

    @Test func enableBuildsTheRouterAndTakesOverTheSchemes() throws {
        let handlers = FakeHandlers(["slack": "com.tinyspeck.slackmacgap"])
        let (setup, root) = try Self.setup(handlers)
        let slack = try Self.app("Slack", id: "com.tinyspeck.slackmacgap", schemes: ["slack", "https"], in: root)
        try setup.enable(sourceApp: slack)

        #expect(try Self.routerSchemes(setup) == ["slack"])
        #expect(handlers.defaultHandler(forScheme: "slack") == LinkRouterSetup.bundleID)
        #expect(handlers.defaultHandler(forScheme: "https") == nil)
        #expect(setup.state.apps["com.tinyspeck.slackmacgap"]?.previousHandlers == ["slack": "com.tinyspeck.slackmacgap"])
        #expect(setup.isEnabled(sourceBundleID: "com.tinyspeck.slackmacgap"))
        let info = try InfoPlistEditor.read(setup.routerBundle.appendingPathComponent("Contents/Info.plist"))
        #expect(info["LSUIElement"] as? Bool == true)
        try Signer.verify(setup.routerBundle)

        // Enabling again keeps the original previous handler instead of recording the router itself.
        try setup.enable(sourceApp: slack)
        #expect(setup.state.apps["com.tinyspeck.slackmacgap"]?.previousHandlers["slack"] == "com.tinyspeck.slackmacgap")
    }

    @Test func schemesFromSeveralAppsAreCombinedAndRestoredOneByOne() throws {
        let handlers = FakeHandlers(["slack": "com.tinyspeck.slackmacgap", "zoommtg": "us.zoom.xos"])
        let (setup, root) = try Self.setup(handlers)
        let slack = try Self.app("Slack", id: "com.tinyspeck.slackmacgap", schemes: ["slack"], in: root)
        let zoom = try Self.app("Zoom", id: "us.zoom.xos", schemes: ["zoommtg"], in: root)
        try setup.enable(sourceApp: slack)
        try setup.enable(sourceApp: zoom)
        #expect(try Self.routerSchemes(setup) == ["slack", "zoommtg"])

        try setup.disable(sourceBundleID: "com.tinyspeck.slackmacgap")
        #expect(handlers.defaultHandler(forScheme: "slack") == "com.tinyspeck.slackmacgap")
        #expect(handlers.defaultHandler(forScheme: "zoommtg") == LinkRouterSetup.bundleID)
        #expect(try Self.routerSchemes(setup) == ["zoommtg"])

        try setup.disable(sourceBundleID: "us.zoom.xos")
        #expect(handlers.defaultHandler(forScheme: "zoommtg") == "us.zoom.xos")
        #expect(!FileManager.default.fileExists(atPath: setup.routerBundle.path))
        #expect(setup.state.apps.isEmpty)
    }

    @Test func appsWithoutLinkSchemesCantBeEnabled() throws {
        let (setup, root) = try Self.setup(FakeHandlers())
        let plain = try Self.app("Plain", id: "com.example.plain", schemes: [], in: root)
        #expect(throws: LinkRouterError.noSchemes("Plain")) { try setup.enable(sourceApp: plain) }
    }

    @Test func syncDropsAppsWithoutFullClonesAndRebuildsAMissingRouter() throws {
        let handlers = FakeHandlers(["slack": "com.tinyspeck.slackmacgap"])
        let (setup, root) = try Self.setup(handlers)
        let slack = try Self.app("Slack", id: "com.tinyspeck.slackmacgap", schemes: ["slack"], in: root)
        try setup.enable(sourceApp: slack)
        let clone = LinkRouterTests.entry("Slack (Work)", source: slack.path, sourceID: "com.tinyspeck.slackmacgap")

        try FileManager.default.removeItem(at: setup.routerBundle)
        handlers.setDefaultHandlerUnchecked("com.tinyspeck.slackmacgap", "slack")   // another app took the scheme back
        try setup.sync(entries: [clone])
        #expect(FileManager.default.fileExists(atPath: setup.routerBundle.path))
        #expect(handlers.defaultHandler(forScheme: "slack") == LinkRouterSetup.bundleID)

        try setup.sync(entries: [])   // last clone deleted
        #expect(handlers.defaultHandler(forScheme: "slack") == "com.tinyspeck.slackmacgap")
        #expect(!setup.isEnabled(sourceBundleID: "com.tinyspeck.slackmacgap"))
        #expect(!FileManager.default.fileExists(atPath: setup.routerBundle.path))
    }
}

extension FakeHandlers {
    func setDefaultHandlerUnchecked(_ bundleID: String, _ scheme: String) { try? setDefaultHandler(bundleID: bundleID, forScheme: scheme) }
}
