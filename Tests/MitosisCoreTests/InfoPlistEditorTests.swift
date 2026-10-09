import Foundation
import Testing
@testable import MitosisCore

@Suite struct InfoPlistEditorTests {
    @Test func appliesIdentityIconAndSparkleEdits() throws {
        let dir = try TestSupport.tempDir()
        let url = dir.appendingPathComponent("Info.plist")
        try InfoPlistEditor.write([
            "CFBundleIdentifier": "com.example.app", "CFBundleName": "App", "CFBundleIconName": "AppIcon",
            "CFBundleIcons": ["x": 1], "SUFeedURL": "https://example.com/appcast.xml",
            "ElectronAsarIntegrity": ["Resources/app.asar": ["hash": "abc"]],
        ], to: url)

        try InfoPlistEditor.apply(InfoPlistEdit(bundleID: "com.example.app.mitosis.work", name: "App (Work)",
                                                iconFile: "MitosisIcon", disableSparkle: true), to: url)

        let d = try InfoPlistEditor.read(url)
        #expect(d["CFBundleIdentifier"] as? String == "com.example.app.mitosis.work")
        #expect(d["CFBundleName"] as? String == "App (Work)")
        #expect(d["CFBundleDisplayName"] as? String == "App (Work)")
        #expect(d["CFBundleIconFile"] as? String == "MitosisIcon")
        #expect(d["CFBundleIconName"] == nil)
        #expect(d["CFBundleIcons"] == nil)
        #expect(d["SUFeedURL"] == nil)
        #expect(d["SUEnableAutomaticChecks"] as? Bool == false)
        #expect(d["SUAutomaticallyUpdate"] as? Bool == false)
        #expect(d["ElectronAsarIntegrity"] != nil)   // must be preserved
    }

    @Test func leavesSparkleKeysAloneWhenNotRequested() throws {
        let dir = try TestSupport.tempDir()
        let url = dir.appendingPathComponent("Info.plist")
        try InfoPlistEditor.write(["CFBundleIdentifier": "a", "SUFeedURL": "https://x"], to: url)
        try InfoPlistEditor.apply(InfoPlistEdit(bundleID: "b", name: "B", iconFile: nil, disableSparkle: false), to: url)
        let d = try InfoPlistEditor.read(url)
        #expect(d["SUFeedURL"] as? String == "https://x")
        #expect(d["CFBundleIconFile"] == nil)
    }
}
