import Foundation
import Testing
@testable import MitosisCore

@Suite struct AppInspectorTests {
    @Test func readsBasicFields() throws {
        let dir = try TestSupport.tempDir()
        var o = FixtureOptions()
        o.version = "4.47.0"; o.build = "447"
        let info = try AppInspector().inspect(try FixtureFactory.makeApp(in: dir, o))
        #expect(info.bundleID == "com.example.fixture")
        #expect(info.name == "Fixture")
        #expect(info.version == "4.47.0")
        #expect(info.build == "447")
        #expect(info.executableName == "Fixture")
        #expect(!info.isSandboxed)
        #expect(!info.hasMASReceipt)
        #expect(!info.isAppleApp)
        #expect(!info.isMitosisClone)
        #expect(info.frameworks.isEmpty)
        #expect(info.restrictedEntitlements.isEmpty)
        #expect(info.cdhash?.count == 40)
    }

    @Test func detectsSandboxReceiptAndRestrictedEntitlements() throws {
        let dir = try TestSupport.tempDir()
        var o = FixtureOptions()
        o.entitlements = ["com.apple.security.app-sandbox": true, "aps-environment": "production"]
        o.masReceipt = true
        let info = try AppInspector().inspect(try FixtureFactory.makeApp(in: dir, o))
        #expect(info.isSandboxed)
        #expect(info.hasMASReceipt)
        #expect(info.restrictedEntitlements == ["aps-environment"])
    }

    @Test func detectsFrameworks() throws {
        let dir = try TestSupport.tempDir()
        var electron = FixtureOptions()
        electron.name = "ElectronApp"
        electron.frameworks = ["Electron Framework", "Squirrel"]
        electron.helperApps = ["ElectronApp Helper"]
        #expect(try AppInspector().inspect(try FixtureFactory.makeApp(in: dir, electron)).frameworks == [.electron, .squirrel])

        var chromium = FixtureOptions()
        chromium.name = "ChromeApp"
        chromium.chromiumFramework = "ChromeApp"
        chromium.frameworks = ["Sparkle"]
        #expect(try AppInspector().inspect(try FixtureFactory.makeApp(in: dir, chromium)).frameworks == [.chromium, .sparkle])

        var catalyst = FixtureOptions()
        catalyst.name = "CatalystApp"
        catalyst.extraInfo = ["UIDeviceFamily": [2, 6]]
        #expect(try AppInspector().inspect(try FixtureFactory.makeApp(in: dir, catalyst)).frameworks == [.catalyst])
    }

    @Test func flagsAppleAppsAndMitosisClones() throws {
        let dir = try TestSupport.tempDir()
        var apple = FixtureOptions()
        apple.name = "AppleLike"; apple.bundleID = "com.apple.fake"
        #expect(try AppInspector().inspect(try FixtureFactory.makeApp(in: dir, apple)).isAppleApp)

        var clone = FixtureOptions()
        clone.name = "AlreadyClone"; clone.signed = false
        let cloneURL = try FixtureFactory.makeApp(in: dir, clone)
        try Data("{}".utf8).write(to: cloneURL.appendingPathComponent("Contents/Resources/mitosis.json"))
        #expect(try AppInspector().inspect(cloneURL).isMitosisClone)
    }

    // Review Focus: unsigned source still inspectable. (On Apple silicon the linker signs every binary,
    // so an app without a bundle signature may still report the executable's cdhash.)
    @Test func unsignedAppIsStillInspectable() throws {
        let dir = try TestSupport.tempDir()
        var o = FixtureOptions(); o.signed = false
        let info = try AppInspector().inspect(try FixtureFactory.makeApp(in: dir, o))
        #expect(info.bundleID == "com.example.fixture")
        #expect(info.restrictedEntitlements.isEmpty)
        #expect(!info.isSandboxed)
    }

    // Muse ships without CFBundleExecutable; macOS then starts Contents/MacOS/<bundle name>.
    @Test func missingExecutableKeyFallsBackLikeMacOS() throws {
        let dir = try TestSupport.tempDir()
        var o = FixtureOptions()
        o.name = "Muse"; o.executable = "Muse"; o.omitInfoKeys = ["CFBundleExecutable"]
        #expect(try AppInspector().inspect(try FixtureFactory.makeApp(in: dir, o)).executableName == "Muse")
    }

    @Test func missingExecutableKeyFallsBackToTheOnlyProgram() throws {
        let dir = try TestSupport.tempDir()
        var o = FixtureOptions()
        o.name = "Muse"; o.executable = "EndoMain"; o.omitInfoKeys = ["CFBundleExecutable"]
        #expect(try AppInspector().inspect(try FixtureFactory.makeApp(in: dir, o)).executableName == "EndoMain")
    }

    @Test func noProgramToStartIsExplainedInPlainWords() throws {
        let dir = try TestSupport.tempDir()
        var o = FixtureOptions()
        o.name = "Broken"; o.omitInfoKeys = ["CFBundleExecutable"]
        let app = try FixtureFactory.makeApp(in: dir, o)
        try FileManager.default.removeItem(at: app.appendingPathComponent("Contents/MacOS/Fixture"))
        let error = #expect(throws: InspectionError.self) { try AppInspector().inspect(app) }
        #expect(error?.localizedDescription.contains("Broken.app") == true)
        #expect(error?.localizedDescription.contains("MitosisCore") == false)
    }

    @Test func errorsReadWellInTheApp() {
        let e: Error = InspectionError.missingInfo(key: "CFBundleIdentifier", app: "Odd.app")
        #expect(e.localizedDescription == InspectionError.missingInfo(key: "CFBundleIdentifier", app: "Odd.app").description)
    }

    @Test func rejectsNonApps() throws {
        let dir = try TestSupport.tempDir()
        #expect(throws: InspectionError.notAnApp(dir.path)) { try AppInspector().inspect(dir) }
    }
}
