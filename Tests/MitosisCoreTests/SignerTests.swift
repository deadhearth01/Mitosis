import Foundation
import Testing
@testable import MitosisCore

@Suite struct SignerTests {
    static func electronLikeApp(in dir: URL) throws -> URL {
        var o = FixtureOptions()
        o.name = "Electronish"
        o.frameworks = ["Electron Framework"]
        o.helperApps = ["Electronish Helper"]
        o.nativeModule = true
        o.entitlements = ["com.apple.security.app-sandbox": true, "aps-environment": "production"]
        return try FixtureFactory.makeApp(in: dir, o)
    }

    static func changeBundleID(_ app: URL, to id: String) throws {
        let plist = app.appendingPathComponent("Contents/Info.plist")
        var info = try InfoPlistEditor.read(plist)
        info["CFBundleIdentifier"] = id
        try InfoPlistEditor.write(info, to: plist)
    }

    @Test func resignsModifiedBundleWithSanitizedEntitlements() throws {
        let dir = try TestSupport.tempDir()
        let app = try Self.electronLikeApp(in: dir)
        try Self.changeBundleID(app, to: "com.example.fixture.mitosis.work")
        #expect(throws: ShellError.self) { try Signer.verify(app) }   // modification broke the seal

        try Signer.signModified(bundle: app, entitlements: ["com.apple.security.app-sandbox": true],
                                identifier: "com.example.fixture.mitosis.work")
        try Signer.verify(app)
        let ents = try Entitlements.read(from: app)
        #expect(Set(ents.keys) == ["com.apple.security.app-sandbox"])
    }

    // A3 (spike): unmodified nested code keeps the vendor signature, so APFS keeps sharing its blocks.
    @Test func unmodifiedNestedCodeKeepsItsSignature() throws {
        let dir = try TestSupport.tempDir()
        let app = try Self.electronLikeApp(in: dir)
        let framework = app.appendingPathComponent("Contents/Frameworks/Electron Framework.framework")
        let helper = app.appendingPathComponent("Contents/Frameworks/Electronish Helper.app")
        let before = (AppInspector.cdhash(of: framework), AppInspector.cdhash(of: helper))
        try Self.changeBundleID(app, to: "com.example.fixture.mitosis.work")
        try Signer.signModified(bundle: app, entitlements: [:], identifier: "com.example.fixture.mitosis.work")
        try Signer.verify(app)
        #expect(before.0 != nil && AppInspector.cdhash(of: framework) == before.0)
        #expect(before.1 != nil && AppInspector.cdhash(of: helper) == before.1)
    }

    @Test func signsHelperBundlesAndExtraExecutablesWithIdentifier() throws {
        let dir = try TestSupport.tempDir()
        var o = FixtureOptions(); o.signed = false; o.helperApps = ["Fixture Helper"]
        let app = try FixtureFactory.makeApp(in: dir, o)
        let macOS = app.appendingPathComponent("Contents/MacOS")
        let real = macOS.appendingPathComponent("Fixture.mitosis-real")
        try FileManager.default.moveItem(at: macOS.appendingPathComponent("Fixture"), to: real)
        try FileManager.default.copyItem(at: TestSupport.stubBinary, to: macOS.appendingPathComponent("Fixture"))
        let helper = app.appendingPathComponent("Contents/Frameworks/Fixture Helper.app")
        try Signer.signModified(bundle: app, entitlements: [:], identifier: "com.example.x",
                                helperBundles: [helper], extraExecutables: [real])
        try Signer.verify(app)
        let r = try Shell.run("/usr/bin/codesign", ["-dv", real.path], check: false)
        #expect(r.stderr.contains("Identifier=com.example.x"))
    }
}
